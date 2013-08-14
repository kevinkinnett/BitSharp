using BitSharp.Blockchain;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Daemon;
using BitSharp.Network;
using BitSharp.Network.ExtensionMethods;
using BitSharp.Node.ExtensionMethods;
using BitSharp.Script;
using BitSharp.Storage;
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
using BitSharp.Data;
using System.IO;
using System.Globalization;

namespace BitSharp.Node
{
    public enum LocalClientType
    {
        MainNet,
        TestNet3,
        ComparisonToolTestNet
    }

    public class LocalClient : IDisposable
    {
        private static readonly int SERVER_BACKLOG = 100;
        private static readonly int CONNECTED_MAX = 100;
        private static readonly int PENDING_MAX = 100;
        private static readonly int HANDSHAKE_TIMEOUT_MS = 15000;
        private static readonly Random random = new Random();

        private readonly CancellationTokenSource shutdownToken;

        private readonly LocalClientType _type;

        private readonly Worker connectWorker;
        private readonly Worker requestBlocksWorker;
        private readonly Worker requestHeadersWorker;
        private readonly Worker statsWorker;

        private readonly BlockchainDaemon blockchainDaemon;

        private readonly BoundedCache<NetworkAddressKey, NetworkAddressWithTime> knownAddressCache;

        private Stopwatch messageStopwatch = new Stopwatch();
        private int messageCount;

        private int incomingCount;
        private ConcurrentSet<IPEndPoint> unconnectedPeers = new ConcurrentSet<IPEndPoint>();
        private ConcurrentSet<IPEndPoint> badPeers = new ConcurrentSet<IPEndPoint>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> pendingPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();
        private ConcurrentDictionary<IPEndPoint, RemoteNode> connectedPeers = new ConcurrentDictionary<IPEndPoint, RemoteNode>();

        private readonly ConcurrentQueue<UInt256> requestBlocks = new ConcurrentQueue<UInt256>();
        private readonly ConcurrentSet<UInt256> requestedBlocks = new ConcurrentSet<UInt256>();
        private readonly ConcurrentDictionary<UInt256, Tuple<DateTime, DateTime?>> requestedBlockTimes = new ConcurrentDictionary<UInt256, Tuple<DateTime, DateTime?>>();
        private List<UInt256> newChainBlockList;
        private UInt256 lastCurrentBlockchain;
        private UInt256 lastTargetChainedBlock;

        private Socket listenSocket;

        public LocalClient(LocalClientType type, BlockchainDaemon blockchainDaemon, IBoundedStorage<NetworkAddressKey, NetworkAddressWithTime> knownAddressStorage)
        {
            this._type = type;

            this.blockchainDaemon = blockchainDaemon;
            this.shutdownToken = new CancellationTokenSource();

            this.knownAddressCache = new BoundedCache<NetworkAddressKey, NetworkAddressWithTime>
            (
                "KnownAddressCache",
                dataStorage: knownAddressStorage,
                maxFlushMemorySize: 5.THOUSAND(),
                maxCacheMemorySize: 500.THOUSAND(),
                sizeEstimator: knownAddress => 40
            );

            this.connectWorker = new Worker("LocalClient.ConnectWorker", ConnectWorker, true, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            this.requestBlocksWorker = new Worker("LocalClient.RequestBlocksWorker", RequestBlocksWorker, true, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(5000));
            this.requestHeadersWorker = new Worker("LocalClient.RequestHeadersWorker", RequestHeadersWorker, true, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(5000));
            this.statsWorker = new Worker("LocalClient.StatsWorker", StatsWorker, true, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            this.blockchainDaemon.OnWinningBlockChanged += OnWinningBlockChanged;

            switch (this.Type)
            {
                case LocalClientType.MainNet:
                    Messaging.Port = 8333;
                    Messaging.Magic = Messaging.MAGIC_MAIN;
                    break;

                case LocalClientType.TestNet3:
                    Messaging.Port = 18333;
                    Messaging.Magic = Messaging.MAGIC_TESTNET3;
                    break;

                case LocalClientType.ComparisonToolTestNet:
                    Messaging.Port = 18444;
                    Messaging.Magic = Messaging.MAGIC_COMPARISON_TOOL;
                    break;
            }
        }

        public LocalClientType Type { get { return this._type; } }

        public void Start()
        {
            Startup();

            this.connectWorker.Start();

            this.requestBlocksWorker.Start();
            this.requestHeadersWorker.Start();

            this.messageStopwatch.Start();
            this.messageCount = 0;
            this.statsWorker.Start();
        }

        public void Dispose()
        {
            this.blockchainDaemon.OnWinningBlockChanged -= OnWinningBlockChanged;

            this.shutdownToken.Cancel();

            Shutdown();

            new IDisposable[]
            {
                this.requestBlocksWorker,
                this.requestHeadersWorker,
                this.connectWorker,
                this.statsWorker,
                this.knownAddressCache,
                this.shutdownToken
            }.DisposeList();
        }

        private void OnWinningBlockChanged(object sender, ChainedBlock winningChainedBlock)
        {
            this.requestHeadersWorker.NotifyWork();
        }

        private void ConnectWorker()
        {
            // get peer counts
            var connectedCount = this.connectedPeers.Count;
            var pendingCount = this.pendingPeers.Count;
            var unconnectedCount = this.unconnectedPeers.Count;
            var maxConnections = Math.Max(CONNECTED_MAX + 20, PENDING_MAX);

            // if there aren't enough peers connected and there is a pending connection slot available, make another connection
            if (connectedCount < CONNECTED_MAX
                 && pendingCount < PENDING_MAX
                 && (connectedCount + pendingCount) < maxConnections
                 && unconnectedCount > 0)
            {
                // grab a snapshot of unconnected peers
                var unconnectedPeersLocal = this.unconnectedPeers.SafeToList();

                // get number of connections to attempt
                var connectCount = Math.Min(unconnectedCount, maxConnections - (connectedCount + pendingCount));

                var connectTasks = new Task[connectCount];
                for (var i = 0; i < connectCount; i++)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    // get a random peer to connect to
                    var remoteEndpoint = unconnectedPeersLocal.RandomOrDefault();
                    connectTasks[i] = ConnectToPeer(remoteEndpoint);
                }

                // wait for pending connection attempts to complete
                Task.WaitAll(connectTasks.ToArray());
            }

            // check if there are too many peers connected
            var overConnected = this.connectedPeers.Count - CONNECTED_MAX;
            if (overConnected > 0)
            {
                foreach (var remoteEndpoint in this.connectedPeers.Keys.Take(overConnected))
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    //Debug.WriteLine(string.Format("Too many peers connected ({0}), disconnecting {1}", overConnected, remoteEndpoint));
                    DisconnectPeer(remoteEndpoint, null);
                }
            }
        }

        private void RequestBlocksWorker()
        {
            //var stopwatch = new Stopwatch();
            //stopwatch.Start();

            var now = DateTime.UtcNow;

            //Debug.WriteLine("a: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));

            foreach (var keyPair in this.requestedBlockTimes)
            {
                if ((keyPair.Value.Item2 != null && now - keyPair.Value.Item2 > TimeSpan.FromSeconds(60))
                    || now - keyPair.Value.Item1 > TimeSpan.FromMinutes(10))
                {
                    Tuple<DateTime, DateTime?> ignore;
                    this.requestedBlockTimes.TryRemove(keyPair.Key, out ignore);
                }
            }

            foreach (var keyPair in this.requestedBlockTimes)
            {
                if (now - keyPair.Value.Item1 > TimeSpan.FromSeconds(15))
                {
                    this.requestedBlocks.TryRemove(keyPair.Key);
                }
            }

            var receiveTimes = this.requestedBlockTimes.Where(x => x.Value.Item2 != null).ToList();

            var receiveRateHz = 0d;
            if (receiveTimes.Count > 0)
            {
                var minStartTime = new DateTime(receiveTimes.Min(x => x.Value.Item1.Ticks));
                var maxFinishTime = new DateTime(receiveTimes.Max(x => x.Value.Item2.Value.Ticks));

                if (maxFinishTime > minStartTime)
                    receiveRateHz = receiveTimes.Count / (maxFinishTime - minStartTime).TotalSeconds;
            }

            //var fulfilledRatio = (float)receiveTimes.Count / this.requestedBlockTimes.Count;
            //if (float.IsNaN(fulfilledRatio) || float.IsInfinity(fulfilledRatio) || fulfilledRatio <= 0.1f)
            //    fulfilledRatio = 0.1f;
            //else if (fulfilledRatio > 1.0f)
            //    fulfilledRatio = 1.0f;

            //TODO
            // request 60 seconds download worth of blocks
            var requestAmount = 1 + (int)(receiveRateHz * 60);


            //Debug.WriteLine("b: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));

            //Debug.WriteLine("{0:#,##0}%".Format2(fulfilledRatio * 100));
            Debug.WriteLine("{0,5}".Format2(this.requestedBlocks.Count));
            Debug.WriteLine("{0,5}".Format2(requestAmount));
            Debug.WriteLine("{0:#,##0.000}/s".Format2(receiveRateHz));
            if (this.requestedBlocks.Count > requestAmount)
                return;

            var connectedPeersLocal = this.connectedPeers.Values.SafeToList();
            if (connectedPeersLocal.Count == 0)
                return;

            var requestTasks = new List<Task>();

            var currentBlockchain = this.blockchainDaemon.CurrentBlockchain;
            var targetChainedBlock = this.blockchainDaemon.WinningBlock;
            //if (this.requestBlocks.IsEmpty) // && targetChainedBlock.BlockHash != this.lastTargetChainedBlock)
            //{
            try
            {
                if (this.newChainBlockList == null
                    //|| this.newChainBlockList.Count == 0
                    //|| currentBlockchain.RootBlockHash != this.lastCurrentBlockchain
                    || targetChainedBlock.BlockHash != this.lastTargetChainedBlock)
                {
                    using (var cancelToken = new CancellationTokenSource())
                    {
                        this.newChainBlockList =
                            new MethodTimer().Time("newChainBlockList", () =>
                                this.blockchainDaemon.Calculator.FindBlocksPastLastCommonAncestor(currentBlockchain, targetChainedBlock, cancelToken.Token)
                                .Except(this.blockchainDaemon.CacheContext.BlockCache.GetAllKeys())
                                .ToList());
                    }
                }

                this.lastCurrentBlockchain = currentBlockchain.RootBlockHash;
                this.lastTargetChainedBlock = targetChainedBlock.BlockHash;

                // send out requests for blocks in winning chain in order
                for (var i = 0; i < this.newChainBlockList.Count; i++)
                {
                    // cooperative loop
                    this.shutdownToken.Token.ThrowIfCancellationRequested();

                    var block = this.newChainBlockList[i];
                    if (!this.blockchainDaemon.CacheContext.BlockCache.ContainsKey(block))
                    {
                        this.requestBlocks.Enqueue(block);
                    }
                    else
                    {
                        this.newChainBlockList.RemoveAt(i);
                        i--;
                    }

                    //if (this.requestBlocks.Count > requestAmount * 10)
                    //    break;
                }
            }
            catch (AggregateException e)
            {
                foreach (var mde in e.InnerExceptions.OfType<MissingDataException>())
                {
                    if (mde.DataType == DataType.Block || mde.DataType == DataType.BlockHeader)
                        this.requestBlocks.Enqueue(mde.DataKey);
                }
            }
            catch (MissingDataException e)
            {
                if (e.DataType == DataType.Block || e.DataType == DataType.BlockHeader)
                    this.requestBlocks.Enqueue(e.DataKey);
            }
            catch (ValidationException) { }
            //}

            //Debug.WriteLine("c: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));

            UInt256 requestBlockHash;
            while (this.requestBlocks.TryDequeue(out requestBlockHash))
            {
                var task = RequestBlock(connectedPeersLocal.RandomOrDefault(), requestBlockHash);
                if (task != null)
                    requestTasks.Add(task);

                //if (requestTasks.Count > requestAmount)
                if (this.requestedBlocks.Count > requestAmount)
                    break;

                // cooperative loop
                this.shutdownToken.Token.ThrowIfCancellationRequested();
            }

            //Debug.WriteLine("d: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));

            // send out requests for any missing blocks
            foreach (var block in this.blockchainDaemon.MissingBlocks)
            {
                //if (requestTasks.Count > requestAmount)
                if (this.requestedBlocks.Count > requestAmount)
                    break;

                // cooperative loop
                this.shutdownToken.Token.ThrowIfCancellationRequested();

                var task = RequestBlock(connectedPeersLocal.RandomOrDefault(), block);
                if (task != null)
                    requestTasks.Add(task);
            }

            //Debug.WriteLine("e: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));

            //Task.WaitAll(requestTasks.ToArray(), TimeSpan.FromSeconds(15));

            //Debug.WriteLine("f: {0:#,##0.000} s".Format2(stopwatch.ElapsedSecondsFloat()));
        }

        private void RequestHeadersWorker()
        {
            var connectedPeersLocal = this.connectedPeers.Values.SafeToList();
            if (connectedPeersLocal.Count == 0)
                return;

            // send out request for unknown block headers
            if (this.Type != LocalClientType.ComparisonToolTestNet)
                SendGetHeaders(connectedPeersLocal.RandomOrDefault()).Forget();
        }

        private void StatsWorker()
        {
            Debug.WriteLine(string.Format("UNCONNECTED: {0,3}, PENDING: {1,3}, CONNECTED: {2,3}, BAD: {3,3}, INCOMING: {4,3}, MESSAGES/SEC: {5,6}", this.unconnectedPeers.Count, this.pendingPeers.Count, this.connectedPeers.Count, this.badPeers.Count, this.incomingCount, ((float)this.messageCount / ((float)this.messageStopwatch.ElapsedMilliseconds / 1000)).ToString("0")));

            this.messageStopwatch.Restart();
            this.messageCount = 0;
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
            switch (this.Type)
            {
                case LocalClientType.MainNet:
                case LocalClientType.TestNet3:
                    var externalIPAddress = Messaging.GetExternalIPAddress();
                    var localhost = Dns.GetHostEntry(Dns.GetHostName());

                    this.listenSocket = new Socket(externalIPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    this.listenSocket.Bind(new IPEndPoint(localhost.AddressList.Where(x => x.AddressFamily == externalIPAddress.AddressFamily).First(), Messaging.Port));
                    this.listenSocket.Listen(SERVER_BACKLOG);
                    break;

                case LocalClientType.ComparisonToolTestNet:
                    this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this.listenSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), Messaging.Port));
                    this.listenSocket.Listen(SERVER_BACKLOG);
                    break;
            }

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
                                if (await ConnectAndHandshake(remoteNode, isIncoming: true))
                                {
                                    Interlocked.Increment(ref this.incomingCount);
                                }
                                else
                                {
                                    DisconnectPeer(remoteNode.RemoteEndPoint, null);
                                }
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
        }

        private async Task SendGetHeaders(RemoteNode remoteNode)
        {
            var blockLocatorHashes = CalculateBlockLocatorHashes(this.blockchainDaemon.WinningBlockchain);

            await remoteNode.Sender.SendGetHeaders(blockLocatorHashes, hashStop: 0);
        }

        private async Task SendGetBlocks(RemoteNode remoteNode)
        {
            var blockLocatorHashes = CalculateBlockLocatorHashes(this.blockchainDaemon.WinningBlockchain);

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

            switch (this.Type)
            {
                case LocalClientType.MainNet:
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
                    //addSeed("www.dcscdn.com");
                    //addSeed("ns2.dcscdn.com");
                    //No such host is known: addSeed("coin.soul-dev.com");
                    addSeed("messier.bzfx.net");
                    break;

                case LocalClientType.TestNet3:
                    addSeed("testnet-seed.bitcoin.petertodd.org");
                    addSeed("testnet-seed.bluematt.me");
                    break;
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

                this.unconnectedPeers.TryRemove(remoteEndPoint);
                this.pendingPeers.TryAdd(remoteNode.RemoteEndPoint, remoteNode);

                var success = await ConnectAndHandshake(remoteNode, isIncoming: false);
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
            remoteNode.Receiver.OnBlockHeader += OnBlockHeader;
            remoteNode.Receiver.OnReceivedAddresses += OnReceivedAddresses;
            remoteNode.OnGetBlocks += OnGetBlocks;
            remoteNode.OnGetHeaders += OnGetHeaders;
            remoteNode.OnPing += OnPing;
            remoteNode.OnDisconnect += OnDisconnect;
        }

        private void UnwireNode(RemoteNode remoteNode)
        {
            remoteNode.Receiver.OnMessage -= OnMessage;
            remoteNode.Receiver.OnInventoryVectors -= OnInventoryVectors;
            remoteNode.Receiver.OnBlock -= OnBlock;
            remoteNode.Receiver.OnBlockHeader -= OnBlockHeader;
            remoteNode.Receiver.OnReceivedAddresses -= OnReceivedAddresses;
            remoteNode.OnGetBlocks -= OnGetBlocks;
            remoteNode.OnGetHeaders -= OnGetHeaders;
            remoteNode.OnPing -= OnPing;
            remoteNode.OnDisconnect -= OnDisconnect;
        }

        private void OnMessage(Message message)
        {
            Interlocked.Increment(ref this.messageCount);
        }

        private void OnInventoryVectors(ImmutableArray<InventoryVector> invVectors)
        {
            var connectedPeersLocal = this.connectedPeers.Values.SafeToList();
            if (connectedPeersLocal.Count == 0)
                return;

            foreach (var invVector in invVectors)
            {
                if (
                    invVector.Type == InventoryVector.TYPE_MESSAGE_BLOCK
                    && !this.blockchainDaemon.CacheContext.BlockCache.ContainsKey(invVector.Hash)
                    && (/*TODO properly handle comparison blockchain*/ this.Type == LocalClientType.ComparisonToolTestNet || this.blockchainDaemon.MissingBlocks.Contains(invVector.Hash)))
                {
                    this.requestBlocks.Enqueue(invVector.Hash);
                }
            }
        }

        private Task RequestBlock(RemoteNode remoteNode, UInt256 blockHash)
        {
            if (this.blockchainDaemon.CacheContext.BlockCache.ContainsKey(blockHash))
                return null;

            var now = DateTime.UtcNow;
            var newRequestTime = Tuple.Create(now, (DateTime?)null);

            // check if block has already been requested
            if (this.requestedBlockTimes.TryAdd(blockHash, newRequestTime))
            {
                this.requestedBlocks.TryAdd(blockHash);
                var invVectors = ImmutableArray.Create<InventoryVector>(new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, blockHash));
                return remoteNode.Sender.SendGetData(invVectors);
            }
            else
            {
                // if block has already been requested, check if the request is old enough to send again
                Tuple<DateTime, DateTime?> lastRequestTime;
                if (this.requestedBlockTimes.TryGetValue(blockHash, out lastRequestTime))
                {
                    if ((now - lastRequestTime.Item1) > TimeSpan.FromSeconds(15))
                    {
                        this.requestedBlocks.TryAdd(blockHash);
                        this.requestedBlockTimes.AddOrUpdate(blockHash, newRequestTime, (existingKey, existingValue) => newRequestTime);

                        var invVectors = ImmutableArray.Create<InventoryVector>(new InventoryVector(InventoryVector.TYPE_MESSAGE_BLOCK, blockHash));
                        return remoteNode.Sender.SendGetData(invVectors);
                    }
                }
            }

            return null;
        }

        private void OnBlock(Block block)
        {
            //Debug.WriteLine("Received block {0}".Format2(block.Hash));

            this.requestedBlocks.TryRemove(block.Hash);
            this.blockchainDaemon.CacheContext.BlockCache.CreateValue(block.Hash, block);

            Tuple<DateTime, DateTime?> receiveTime;
            if (this.requestedBlockTimes.TryGetValue(block.Hash, out receiveTime))
            {
                if (receiveTime.Item2 == null)
                    this.requestedBlockTimes[block.Hash] = Tuple.Create(receiveTime.Item1, (DateTime?)DateTime.UtcNow);

                this.requestBlocksWorker.NotifyWork();
            }
        }

        private void OnBlockHeader(BlockHeader blockHeader)
        {
            //Debug.WriteLine("Received block header {0}".Format2(blockHeader.Hash);
            if (!this.blockchainDaemon.CacheContext.BlockHeaderCache.ContainsKey(blockHeader.Hash))
            {
                this.blockchainDaemon.CacheContext.BlockHeaderCache.CreateValue(blockHeader.Hash, blockHeader);
            }
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

        private void OnGetBlocks(RemoteNode remoteNode, GetBlocksPayload payload)
        {
            //TODO
        }

        private void OnGetHeaders(RemoteNode remoteNode, GetBlocksPayload payload)
        {
            // if in comparison mode, synchronize all work before returning current headers
            if (this.Type == LocalClientType.ComparisonToolTestNet)
                this.blockchainDaemon.WaitForFullUpdate();

            var currentBlockchainLocal = this.blockchainDaemon.CurrentBlockchain;
            var blockHeaders = new List<BlockHeader>(currentBlockchainLocal.BlockCount);
            foreach (var chainedBlock in currentBlockchainLocal.BlockList)
            {
                BlockHeader blockHeader;
                if (this.blockchainDaemon.CacheContext.BlockHeaderCache.TryGetValue(chainedBlock.BlockHash, out blockHeader))
                {
                    blockHeaders.Add(blockHeader);
                }
                else
                {
                    Debugger.Break();
                    Debug.WriteLine("Couldn't generate getheaders response");
                    return;
                }
            }

            var payloadStream = new MemoryStream();
            using (var payloadWriter = new BinaryWriter(payloadStream))
            {
                payloadWriter.WriteVarInt((UInt64)blockHeaders.Count);
                foreach (var blockHeader in blockHeaders)
                {
                    NetworkEncoder.EncodeBlockHeader(payloadStream, blockHeader);
                    payloadWriter.WriteVarInt(0);
                }
            }

            remoteNode.Sender.SendMessageAsync(Messaging.ConstructMessage("headers", payloadStream.ToArray())).Wait();
        }

        private void OnPing(RemoteNode remoteNode, ImmutableArray<byte> payload)
        {
            remoteNode.Sender.SendMessageAsync(Messaging.ConstructMessage("pong", payload.ToArray())).Wait();
        }

        private void OnDisconnect(RemoteNode remoteNode)
        {
            DisconnectPeer(remoteNode.RemoteEndPoint, null);
        }

        private async Task<bool> ConnectAndHandshake(RemoteNode remoteNode, bool isIncoming)
        {
            // wire node
            WireNode(remoteNode);

            // connect
            await remoteNode.ConnectAsync();

            if (remoteNode.IsConnected)
            {
                //TODO
                RemoteNode ignore;
                this.pendingPeers.TryRemove(remoteNode.RemoteEndPoint, out ignore);
                this.connectedPeers.TryAdd(remoteNode.RemoteEndPoint, remoteNode);

                // setup task to wait for verack
                var verAckTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "verack", HANDSHAKE_TIMEOUT_MS);

                // setup task to wait for version
                var versionTask = remoteNode.Receiver.WaitForMessage(x => x.Command == "version", HANDSHAKE_TIMEOUT_MS);

                // start listening for messages after tasks have been setup
                remoteNode.Receiver.Listen();

                // send our local version
                var nodeId = (((UInt64)random.Next()) << 32) + (UInt64)random.Next(); //TODO should be generated and verified on version message

                var currentBlockchainLocal = this.blockchainDaemon.CurrentBlockchain;
                var currentHeight = !currentBlockchainLocal.IsDefault ? (UInt32)currentBlockchainLocal.Height : 0;
                await remoteNode.Sender.SendVersion(Messaging.GetExternalIPEndPoint(), remoteNode.RemoteEndPoint, nodeId, currentHeight);

                // wait for our local version to be acknowledged by the remote peer
                // wait for remote peer to send their version
                await Task.WhenAll(verAckTask, versionTask);

                //TODO shouldn't have to decode again
                var versionMessage = versionTask.Result;
                var versionPayload = NetworkEncoder.DecodeVersionPayload(versionMessage.Payload.ToArray().ToMemoryStream(), versionMessage.Payload.Length);

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

                if (!isIncoming)
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
        private static ImmutableArray<UInt256> CalculateBlockLocatorHashes(IImmutableList<ChainedBlock> blockHashes)
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

    namespace ExtensionMethods
    {
        internal static class LocalClientExtensionMethods
        {
            public static NetworkAddressKey GetKey(this NetworkAddressWithTime knownAddress)
            {
                return new NetworkAddressKey(knownAddress.NetworkAddress.IPv6Address, knownAddress.NetworkAddress.Port);
            }
        }
    }
}
