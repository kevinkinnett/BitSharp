using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Database;
using BitSharp.Network;
using BitSharp.Node.ExtendionMethods;
using BitSharp.Script;
using BitSharp.Storage;
using BitSharp.WireProtocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Node
{
    public class LocalClient : IDisposable
    {
        private static readonly int SERVER_BACKLOG = 100;
        private static readonly int CONNECTED_MAX = 100;
        private static readonly int PENDING_MAX = 100;
        private static readonly int HANDSHAKE_TIMEOUT_MS = 15000;
        private static readonly Random random = new Random();

        private readonly CancellationTokenSource shutdownToken;

        private Thread workerThread;

        private readonly BlockchainDaemon blockchainDaemon;

        private readonly IDataStorage<KnownAddressKey, NetworkAddressWithTime> knownAddressStorage;
        private readonly StorageCache<KnownAddressKey, NetworkAddressWithTime> knownAddressCache;

        private Stopwatch messageStopwatch = new Stopwatch();
        private int messageCount;

        private int incomingCount;
        private ConcurrentSet<IPEndPoint> unconnectedPeers = new ConcurrentSet<IPEndPoint>();
        private ConcurrentSet<IPEndPoint> badPeers = new ConcurrentSet<IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> pendingPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> connectedPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();

        private readonly ConcurrentDictionary<UInt256, DateTime> requestedBlocks = new ConcurrentDictionary<UInt256, DateTime>();

        private Socket listenSocket;

        public LocalClient(BlockchainDaemon blockchainDaemon)
        {
            this.blockchainDaemon = blockchainDaemon;
            this.shutdownToken = new CancellationTokenSource();

            this.knownAddressStorage = new KnownAddressStorage();
            this.knownAddressCache = new StorageCache<KnownAddressKey, NetworkAddressWithTime>
            (
                "KnownAddressCache",
                dataStorage: this.knownAddressStorage,
                maxFlushMemorySize: 5.THOUSAND(),
                maxCacheMemorySize: 500.THOUSAND(),
                sizeEstimator: knownAddress => 40
            );
        }

        public void Start()
        {
            this.workerThread = new Thread(Worker);
            this.workerThread.Start();
        }

        public void Dispose()
        {
            this.shutdownToken.Cancel();

            this.knownAddressCache.Dispose();
            this.workerThread.Join();
            
            this.shutdownToken.Dispose();
        }

        private bool NeedsToConnect()
        {
            return (this.connectedPeers.Count + this.pendingPeers.Count) < CONNECTED_MAX && this.pendingPeers.Count < PENDING_MAX && this.unconnectedPeers.Count > 0;
        }

        private void Worker()
        {
            Startup();

            this.messageStopwatch.Start();
            this.messageCount = 0;
            var i = 0;

            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    //Debug.WriteLine("-----------------------------");

                    // if there aren't enough peers connected and there is a pending connection slot available, make another connection
                    if (NeedsToConnect())
                    {
                        var connectTasks = new List<Task>();
                        while (NeedsToConnect())
                        {
                            var remoteEndpoint = this.unconnectedPeers.SafeToList().RandomOrDefault();
                            connectTasks.Add(ConnectToPeer(remoteEndpoint));
                        }
                    }

                    //Debug.WriteLine("1: {0}".Format2(i));

                    var overConnected = this.connectedPeers.Count - CONNECTED_MAX;
                    if (overConnected > 0)
                    {
                        foreach (var remoteEndpoint in this.connectedPeers.Keys.Take(overConnected))
                        {
                            //Debug.WriteLine(string.Format("Too many peers connected ({0}), disconnecting {1}", overConnected, remoteEndpoint));
                            DisconnectPeer(remoteEndpoint, null);
                        }
                    }

                    //Debug.WriteLine("2: {0}".Format2(i));

                    i++;
                    if (i % 100 == 0)
                    {
                        //Debug.WriteLine("-----------------------");
                        Debug.WriteLine(string.Format("UNCONNECTED: {0,3}, PENDING: {1,3}, CONNECTED: {2,3}, BAD: {3,3}, INCOMING: {4,3}, MESSAGES/SEC: {5,6}", this.unconnectedPeers.Count, this.pendingPeers.Count, this.connectedPeers.Count, this.badPeers.Count, this.incomingCount, ((float)this.messageCount / ((float)this.messageStopwatch.ElapsedMilliseconds / 1000)).ToString("0")));

                        this.messageStopwatch.Restart();
                        this.messageCount = 0;
                    }

                    //Debug.WriteLine("3: {0}".Format2(i));

                    // periodically ask for new blocks
                    if (i % 5 == 0)
                    {
                        // send out requests for any missing blocks
                        foreach (var missingBlock in this.blockchainDaemon.MissingBlocks)
                            RequestBlock(missingBlock);

                        // send out request for unknown blocks
                        var remoteNode = this.connectedPeers.Values.SafeToList().RandomOrDefault();
                        if (remoteNode != null)
                            SendGetBlocks(remoteNode).Forget();
                    }

                    //Debug.WriteLine("4: {0}".Format2(i));

                    Thread.Sleep(100);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.WriteLine(string.Format("LocalClient encountered fatal exception: {0}\n\n{1}", e.Message, e));
                Debugger.Break();
                Environment.Exit(-2);
            }

            Shutdown();
        }

        private void Startup()
        {
            Debug.WriteLine("LocalClients starting up");
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // start listening for incoming peers
            StartListening();

            // add seed peers
            AddSeedPeers();

            // add known peers
            AddKnownPeers();

            stopwatch.Stop();
            Debug.WriteLine("LocalClients finished starting up: {0} ms".Format2(stopwatch.ElapsedMilliseconds));
        }

        private void Shutdown()
        {
            Debug.WriteLine("LocalClient shutting down");

            try
            {
                foreach (var remoteNode in this.connectedPeers.Values)
                {
                    try
                    {
                        remoteNode.Disconnect();
                    }
                    catch (Exception) { } // swallow any exceptions at the peer disconnect level to try and process everyone
                }
            }
            catch (Exception) { } // swallow any looping exceptions

            Debug.WriteLine("LocalClient shutdown finished");
        }

        private async void StartListening()
        {
            var externalIPAddress = Messaging.GetExternalIPAddress();
            var localhost = Dns.GetHostEntry(Dns.GetHostName());

            this.listenSocket = new Socket(externalIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.Bind(new IPEndPoint(localhost.AddressList.Where(x => x.AddressFamily == externalIPAddress.AddressFamily).First(), Messaging.Port));
            this.listenSocket.Listen(SERVER_BACKLOG);

            try
            {
                while (true)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    try
                    {
                        var newSocket = await Task.Factory.FromAsync<Socket>(this.listenSocket.BeginAccept(null, null), this.listenSocket.EndAccept);

                        Task.Run(async () =>
                        {
                            var remoteNode = new RemoteNode(newSocket);
                            try
                            {
                                this.pendingPeers.CheckedAdd(remoteNode.RemoteEndPoint, remoteNode);

                                WireNode(remoteNode);

                                var versionMessageTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "version", HANDSHAKE_TIMEOUT_MS);

                                remoteNode.Receiver.Listen();

                                //wait for version
                                var versionMessage = await versionMessageTask;

                                await remoteNode.Sender.SendVersionAcknowledge();

                                this.incomingCount++;

                                this.pendingPeers.CheckedRemove(remoteNode.RemoteEndPoint, remoteNode);
                                this.connectedPeers.CheckedAdd(remoteNode.RemoteEndPoint, remoteNode);

                                await PeerStartup(remoteNode);
                            }
                            catch (Exception e)
                            {
                                if (remoteNode.RemoteEndPoint != null)
                                    DisconnectPeer(remoteNode.RemoteEndPoint, e);
                            }
                        }).Forget();
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                }
            }
            catch (OperationCanceledException) { }

            this.listenSocket.Dispose();
        }

        private async Task PeerStartup(RemoteNode remoteNode)
        {
            await remoteNode.Sender.RequestKnownAddressesAsync();

            await SendGetBlocks(remoteNode);
        }

        private async Task SendGetBlocks(RemoteNode remoteNode)
        {
            var blockLocatorHashes = CalculateBlockLocatorHashes(this.blockchainDaemon.CurrentBlockchain.BlockList);

            await remoteNode.Sender.SendGetBlocks(blockLocatorHashes, hashStop: 0);
        }

        private void AddSeedPeers()
        {
            Action<string> addSeed =
                hostNameOrAddress =>
                {
                    try
                    {
                        var ipAddress = Dns.GetHostEntry(hostNameOrAddress).AddressList.First();
                        this.unconnectedPeers.TryAdd(new IPEndPoint(ipAddress, Messaging.Port));
                    }
                    catch (SocketException e)
                    {
                        Debug.WriteLine("Failed to add seed peer {0}: {1}".Format2(hostNameOrAddress, e.Message));
                    }
                };

            var isMainNet = true;
            if (isMainNet)
            {
                // mainnet
                Messaging.Port = 8333;
                Messaging.Magic = Messaging.MAGIC_MAIN;

                addSeed("archivum.info");
                addSeed("62.75.216.13");
                addSeed("69.64.34.118");
                addSeed("79.160.221.140");
                addSeed("netzbasis.de");
                addSeed("btc.turboadmin.com");
                addSeed("fallback.bitcoin.zhoutong.com");
                addSeed("bauhaus.csail.mit.edu");
                addSeed("jun.dashjr.org");
                addSeed("cheaperinbitcoins.com");
                addSeed("django.webflows.fr");
                addSeed("204.9.55.71");
                addSeed("btcnode.novit.ro");
                //No such host is known: addSeed("porgressbar.sk");
                addSeed("faucet.bitcoin.st");
                addSeed("bitcoin.securepayment.cc");
                addSeed("x.jine.se");
                addSeed("www.dcscdn.com");
                addSeed("ns2.dcscdn.com");
                //No such host is known: addSeed("coin.soul-dev.com");
                addSeed("messier.bzfx.net");
            }
            else
            {
                // testnet3
                Messaging.Port = 18333;
                Messaging.Magic = Messaging.MAGIC_TESTNET3;

                addSeed("testnet-seed.bitcoin.petertodd.org");
                addSeed("testnet-seed.bluematt.me");
            }
        }

        private void AddKnownPeers()
        {
            var count = 0;
            foreach (var knownAddress in this.knownAddressCache.StreamAllValues().Select(x => x.Value))
            {
                this.unconnectedPeers.TryAdd(knownAddress.NetworkAddress.ToIPEndPoint());
                count++;
            }

            Debug.WriteLine("LocalClients loaded {0} known peers from database".Format2(count));
        }

        private async Task<RemoteNode> ConnectToPeer(IPEndPoint remoteEndPoint)
        {
            try
            {
                var remoteNode = new RemoteNode(remoteEndPoint);

                this.unconnectedPeers.CheckedRemove(remoteEndPoint);

                WireNode(remoteNode);

                var success = await ConnectAndHandshake(remoteNode);
                if (success)
                {
                    await PeerStartup(remoteNode);

                    return remoteNode;
                }
                else
                {
                    DisconnectPeer(remoteEndPoint, null);
                    return null;
                }
            }
            catch (Exception e)
            {
                //Debug.WriteLine(string.Format("Could not connect to {0}: {1}", remoteEndpoint, e.Message));
                DisconnectPeer(remoteEndPoint, e);
                return null;
            }
        }

        private void WireNode(RemoteNode remoteNode)
        {
            remoteNode.Receiver.OnMessage += OnMessage;
            remoteNode.Receiver.OnInventoryVectors += OnInventoryVectors;
            remoteNode.Receiver.OnBlock += OnBlock;
            remoteNode.Receiver.OnReceivedAddresses += OnReceivedAddresses;
            remoteNode.OnDisconnect += OnDisconnect;
        }

        private void UnwireNode(RemoteNode remoteNode)
        {
            remoteNode.Receiver.OnMessage -= OnMessage;
            remoteNode.Receiver.OnInventoryVectors -= OnInventoryVectors;
            remoteNode.Receiver.OnBlock += OnBlock;
            remoteNode.Receiver.OnReceivedAddresses -= OnReceivedAddresses;
            remoteNode.OnDisconnect -= OnDisconnect;
        }

        private void OnMessage(Message message)
        {
            Interlocked.Increment(ref this.messageCount);
        }

        private void OnInventoryVectors(ImmutableArray<InventoryVector> invVectors)
        {
            foreach (var invVector in invVectors)
            {
                if (
                    invVector.Type == InventoryVector.TYPE_MESSAGE_BLOCK
                    && !this.blockchainDaemon.StorageManager.BlockDataCache.ContainsKey(invVector.Hash))
                {
                    RequestBlock(invVector.Hash);
                }
            }
        }

        private void RequestBlock(UInt256 blockHash)
        {
            var remoteNode = this.connectedPeers.Values.SafeToList().RandomOrDefault();
            if (remoteNode == null)
                return;

            var invVectors = ImmutableArray.Create<InventoryVector>(new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, blockHash));
            var now = DateTime.UtcNow;

            // check if block has already been requested
            if (this.requestedBlocks.TryAdd(blockHash, now))
            {
                remoteNode.Sender.SendGetData(invVectors).Forget();
            }
            else
            {
                // if block has already been requested, check if the request is old enough to send again
                DateTime lastRequestTime;
                if (this.requestedBlocks.TryGetValue(blockHash, out lastRequestTime))
                {
                    if ((now - lastRequestTime) > TimeSpan.FromSeconds(15))
                    {
                        this.requestedBlocks.AddOrUpdate(blockHash, now, (existingKey, existingValue) => now);
                        remoteNode.Sender.SendGetData(invVectors).Forget();
                    }
                }
            }
        }

        private void OnBlock(Block block)
        {
            // remove block from pending request list
            DateTime ignore;
            this.requestedBlocks.TryRemove(block.Hash, out ignore);

            //Debug.WriteLine("Received block {0}".Format2(block.Hash.ToHexNumberString());
            this.blockchainDaemon.StorageManager.BlockDataCache.CreateValue(block.Hash, block);
        }

        private void OnReceivedAddresses(ImmutableArray<NetworkAddressWithTime> addresses)
        {
            var ipEndpoints = new List<IPEndPoint>(addresses.Length);
            foreach (var address in addresses)
            {
                var ipEndpoint = address.NetworkAddress.ToIPEndPoint();
                ipEndpoints.Add(ipEndpoint);
            }

            this.unconnectedPeers.UnionWith(ipEndpoints);
            this.unconnectedPeers.ExceptWith(this.badPeers);
            this.unconnectedPeers.ExceptWith(this.connectedPeers.Keys);
            this.unconnectedPeers.ExceptWith(this.pendingPeers.Keys);

            // queue up addresses to be flushed to the database
            foreach (var address in addresses)
            {
                //TODO enable this once i start filtering out garbage
                //this.knownAddressCache.UpdateValue(address.GetKey(), address);
            }
        }

        private void OnDisconnect(RemoteNode remoteNode)
        {
            DisconnectPeer(remoteNode.RemoteEndPoint, null);
        }

        private async Task<bool> ConnectAndHandshake(RemoteNode remoteNode)
        {
            // connect
            await remoteNode.ConnectAsync();

            if (remoteNode.IsConnected)
            {
                //TODO
                RemoteNode ignore;
                this.pendingPeers.TryRemove(remoteNode.RemoteEndPoint, out ignore);
                this.connectedPeers.CheckedAdd(remoteNode.RemoteEndPoint, remoteNode);

                // setup task to wait for verack
                var verAckTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "verack", HANDSHAKE_TIMEOUT_MS);

                // setup task to wait for version
                var versionTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "version", HANDSHAKE_TIMEOUT_MS);

                // send our local version
                var nodeId = (((UInt64)random.Next()) << 32) + (UInt64)random.Next(); //TODO should be generated and verified on version message
                var currentHeight = (UInt32)this.blockchainDaemon.CurrentBlockchain.Height;
                await remoteNode.Sender.SendVersion(Messaging.GetExternalIPEndPoint(), remoteNode.RemoteEndPoint, nodeId, currentHeight);

                // wait for our local version to be acknowledged by the remote peer
                // wait for remote peer to send their version
                await Task.WhenAll(verAckTask, versionTask);

                //TODO shouldn't have to decode again
                var versionMessage = versionTask.Result;
                var versionPayload = VersionPayload.FromRawBytes(versionMessage.Payload.ToArray());

                var remoteAddressWithTime = new NetworkAddressWithTime
                (
                    Time: DateTime.UtcNow.ToUnixTime(),
                    NetworkAddress: new NetworkAddress
                    (
                        Services: versionPayload.LocalAddress.Services,
                        IPv6Address: versionPayload.LocalAddress.IPv6Address,
                        Port: versionPayload.LocalAddress.Port
                    )
                );

                this.knownAddressCache.UpdateValue(remoteAddressWithTime.GetKey(), remoteAddressWithTime);

                // acknowledge their version
                await remoteNode.Sender.SendVersionAcknowledge();

                return true;
            }
            else
            {
                return false;
            }
        }

        private void DisconnectPeer(IPEndPoint remoteEndpoint, Exception e)
        {
            this.badPeers.Add(remoteEndpoint); //TODO
            this.unconnectedPeers.Remove(remoteEndpoint);

            RemoteNode pendingPeer;
            this.pendingPeers.TryRemove(remoteEndpoint, out pendingPeer);

            RemoteNode connectedPeer;
            this.connectedPeers.TryRemove(remoteEndpoint, out connectedPeer);

            if (this.connectedPeers.Count <= 10 && e != null)
            {
                Debug.WriteLine("Remote peer {0} failed, disconnecting: {1}".Format2(remoteEndpoint, e != null ? e.Message : null));
            }

            if (pendingPeer != null)
            {
                UnwireNode(pendingPeer);
                pendingPeer.Disconnect();
            }

            if (connectedPeer != null)
            {
                UnwireNode(connectedPeer);
                connectedPeer.Disconnect();
            }
        }

        //TODO move into p2p node
        private static ImmutableArray<UInt256> CalculateBlockLocatorHashes(IImmutableList<BlockMetadata> blockHashes)
        {
            var blockLocatorHashes = new List<UInt256>();

            if (blockHashes.Count > 0)
            {
                var step = 1;
                var start = 0;
                for (var i = blockHashes.Count - 1; i > 0; i -= step, start++)
                {
                    if (start >= 10)
                        step *= 2;

                    blockLocatorHashes.Add(blockHashes[i].BlockHash);
                }
                blockLocatorHashes.Add(blockHashes[0].BlockHash);
            }

            return blockLocatorHashes.ToImmutableArray();
        }
    }

    namespace ExtendionMethods
    {
        internal static class LocalClientExtensionMethods
        {
            public static void CheckedAdd<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
            {
                if (!dictionary.TryAdd(key, value))
                    throw new Exception();
            }

            public static void CheckedRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
            {
                TValue removedValue;
                if (!dictionary.TryRemove(key, out removedValue))
                    throw new Exception();

                if (!object.ReferenceEquals(value, removedValue))
                    throw new Exception();
            }

            public static void CheckedAdd<T>(this ConcurrentSet<T> set, T item)
            {
                if (!set.TryAdd(item))
                    throw new Exception();
            }

            public static void CheckedRemove<T>(this ConcurrentSet<T> set, T item)
            {
                if (!set.TryRemove(item))
                    throw new Exception();
            }

            public static List<T> SafeToList<T>(this ICollection<T> collection)
            {
                var list = new List<T>(collection.Count);
                foreach (var item in collection)
                    list.Add(item);

                return list;
            }

            public static KnownAddressKey GetKey(this NetworkAddressWithTime knownAddress)
            {
                return new KnownAddressKey(knownAddress.NetworkAddress.IPv6Address, knownAddress.NetworkAddress.Port);
            }
        }
    }
}
