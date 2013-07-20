using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using BitSharp.Network;
using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using System.Collections.Immutable;
using System.IO;

namespace BitSharp.Network
{
    public class RemoteSender
    {
        public event Action<Exception> OnFailed;

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private readonly Socket socket;

        public RemoteSender(Socket socket)
        {
            this.socket = socket;
        }

        private void Fail(Exception e)
        {
            var handler = this.OnFailed;
            if (handler != null)
                handler(e);
        }

        public async Task RequestKnownAddressesAsync()
        {
            await SendMessageAsync("getaddr");
        }

        public async Task PingAsync()
        {
            await SendMessageAsync("ping");
        }

        public async Task SendGetData(InventoryVector invVector)
        {
            await SendGetData(ImmutableArray.Create(invVector));
        }

        public async Task SendGetData(ImmutableArray<InventoryVector> invVectors)
        {
            var getDataPayload = Messaging.ConstructInventoryPayload(invVectors);
            var getDataMessage = Messaging.ConstructMessage("getdata", getDataPayload, NetworkEncoder.EncodeInventoryPayload);

            await SendMessageAsync(getDataMessage);
        }

        public async Task SendGetBlocks(ImmutableArray<UInt256> blockLocatorHashes, UInt256 hashStop)
        {
            var getBlocksPayload = Messaging.ConstructGetBlocksPayload(blockLocatorHashes, hashStop);
            var getBlocksMessage = Messaging.ConstructMessage("getblocks", getBlocksPayload, NetworkEncoder.EncodeGetBlocksPayload);

            await SendMessageAsync(getBlocksMessage);
        }

        public async Task SendVersion(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, UInt64 nodeId, UInt32 startBlockHeight)
        {
            var versionPayload = Messaging.ConstructVersionPayload(localEndPoint, remoteEndPoint, nodeId, startBlockHeight);
            var versionMessage = Messaging.ConstructMessage("version", versionPayload, NetworkEncoder.EncodeVersionPayload);

            await SendMessageAsync(versionMessage);
        }

        public async Task SendVersionAcknowledge()
        {
            await SendMessageAsync("verack");
        }

        private async Task SendMessageAsync(string command)
        {
            await SendMessageAsync(Messaging.ConstructMessage(command, payload: ImmutableArray.Create<byte>()));
        }

        private async Task SendMessageAsync(Message message)
        {
            try
            {
                await semaphore.DoAsync(async () =>
                {
                    using (var stream = new NetworkStream(this.socket))
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var byteStream = new MemoryStream();
                        NetworkEncoder.EncodeMessage(byteStream, message);

                        var messageBytes = byteStream.ToArray();
                        await stream.WriteAsync(messageBytes, 0, messageBytes.Length);

                        stopwatch.Stop();
                        //Debug.WriteLine("-------------------------");
                        //Debug.WriteLine(string.Format("Sent {0} in {1} ms\nPayload: {2}", message.Command, stopwatch.ElapsedMilliseconds, message.Payload.ToHexDataString()));
                    }
                });
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
