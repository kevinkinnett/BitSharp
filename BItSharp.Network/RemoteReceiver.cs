using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using BitSharp.WireProtocol;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Common;
using System.Collections.Immutable;

namespace BitSharp.Network
{
    public class RemoteReceiver
    {
        public event Action<Exception> OnFailed;
        public event Action<Message> OnMessage;
        public event Action<VersionPayload> OnVersion;
        public event Action OnVersionAcknowledged;
        public event Action<ImmutableArray<InventoryVector>> OnInventoryVectors;
        public event Action<ImmutableArray<InventoryVector>> OnNotFound;
        public event Action<Block> OnBlock;
        public event Action<Transaction> OnTransaction;
        public event Action<ImmutableArray<NetworkAddressWithTime>> OnReceivedAddresses;

        private readonly Socket socket;
        private readonly bool persistent;

        public RemoteReceiver(Socket socket, bool persistent)
        {
            this.socket = socket;
            this.persistent = persistent;
        }

        private void Fail(Exception e)
        {
            var handler = this.OnFailed;
            if (handler != null)
                handler(e);
        }

        public void Listen()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    while (true)
                    {
                        var buffer = new byte[4];
                        var bytesReceived = await Task.Factory.FromAsync<int>(this.socket.BeginReceive(buffer, 0, 4, SocketFlags.None, null, null), this.socket.EndReceive);

                        HandleMessage(buffer, bytesReceived);
                    }
                }
                catch (Exception e)
                {
                    Fail(e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public async Task<Message> WaitForMessage(Func<Message, bool> predicate, int timeoutMilliseconds)
        {
            return await WaitForMessage(predicate, TimeSpan.FromMilliseconds(timeoutMilliseconds));
        }

        public async Task<Message> WaitForMessage(Func<Message, bool> predicate, TimeSpan timeout)
        {
            var messageTcs = new TaskCompletionSource<Message>();
            Action<Message> handler =
                message =>
                {
                    if (predicate(message))
                        messageTcs.SetResult(message);
                };

            this.OnMessage += handler;
            try
            {
                if (await Task.WhenAny(messageTcs.Task, Task.Delay(timeout)) == messageTcs.Task)
                {
                    return await messageTcs.Task;
                }
                else
                {
                    throw new TimeoutException();
                }
            }
            finally
            {
                this.OnMessage -= handler;
            }
        }

        private void HandleMessage(byte[] buffer, int bytesReceived)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (bytesReceived == 0)
            {
                Thread.Sleep(10);
                return;
            }
            else if (bytesReceived < 4)
            {
                using (var stream = new NetworkStream(this.socket))
                {
                    for (var i = bytesReceived; i < 4; i++)
                    {
                        var read = stream.ReadByte();
                        if (read >= 0)
                        {
                            buffer[i] = (byte)read;
                        }
                        else
                        {
                            throw new Exception(string.Format("Read wrong number of bytes when listening for magic bytes: {0} bytes read, {1}", i, buffer.ToHexDataString()));
                        }
                    }
                }
            }

            var magic = Bits.ToUInt32(buffer);
            if (magic != Messaging.Magic)
                throw new Exception(string.Format("Unknown magic bytes {0}", buffer.ToHexNumberString()));

            using (var stream = new NetworkStream(this.socket))
            {
                var wireReader = new WireReader(stream);
                var message = WireDecodeMessage(magic, wireReader);

                var handler = this.OnMessage;
                if (handler != null)
                    handler(message);

                stopwatch.Stop();
                //Debug.WriteLine(string.Format("{2,25} Received message {0,12} in {1,6} ms", message.Command, stopwatch.ElapsedMilliseconds, this.socket.RemoteEndPoint));
            }
        }

        private Message WireDecodeMessage(UInt32 magic, WireReader wireReader)
        {
            var command = wireReader.ReadFixedString(12);
            var payloadSize = wireReader.Read4Bytes();
            var payloadChecksum = wireReader.Read4Bytes();
            var payload = wireReader.ReadRawBytes(payloadSize.ToIntChecked());
            //TODO figure out RawBytes so it doesn't have to be regenerated for message

            var message = new Message
            (
                Magic: magic,
                Command: command,
                PayloadSize: payloadSize,
                PayloadChecksum: payloadChecksum,
                Payload: payload.ToImmutableArray()
            );

            if (!Messaging.VerifyPayloadChecksum(message.PayloadChecksum, payload))
                throw new Exception(string.Format("Checksum failed for {0}", message.Command));

            switch (message.Command)
            {
                case "addr":
                    {
                        var addressPayload = AddressPayload.FromRawBytes(payload);

                        var handler = this.OnReceivedAddresses;
                        if (handler != null)
                            handler(addressPayload.NetworkAddresses);
                    }
                    break;

                case "alert":
                    {
                        var alertPayload = AlertPayload.FromRawBytes(payload);
                    }
                    break;

                case "block":
                    {
                        var block = Block.FromRawBytes(payload);

                        var handler = this.OnBlock;
                        if (handler != null)
                            handler(block);
                    }
                    break;

                case "getblocks":
                    {
                        var getBlocksPayload = GetBlocksPayload.FromRawBytes(payload);

                        //var handler = this.OnGetBlocks;
                        //if (handler != null)
                        //    handler(getBlocksPayload);
                    }
                    break;

                case "inv":
                    {
                        var invPayload = InventoryPayload.FromRawBytes(payload);

                        var handler = this.OnInventoryVectors;
                        if (handler != null)
                            handler(invPayload.InventoryVectors);
                    }
                    break;

                case "notfound":
                    {
                        var invPayload = InventoryPayload.FromRawBytes(payload);

                        var handler = this.OnNotFound;
                        if (handler != null)
                            handler(invPayload.InventoryVectors);
                    }
                    break;

                case "tx":
                    {
                        var tx = Transaction.FromRawBytes(payload);

                        var handler = this.OnTransaction;
                        if (handler != null)
                            handler(tx);
                    }
                    break;

                case "version":
                    {
                        var versionPayload = VersionPayload.FromRawBytes(payload);
                        //Debug.WriteLine(string.Format("{0}, {1}", versionPayload.RemoteAddress.ToIPEndPoint(), this.socket.RemoteEndPoint));

                        var handler = this.OnVersion;
                        if (handler != null)
                            handler(versionPayload);
                    }
                    break;

                case "verack":
                    {
                        var handler = this.OnVersionAcknowledged;
                        if (handler != null)
                            handler();
                    }
                    break;

                default:
                    break;
            }

            //TODO
            //if (payloadStream.Position != payloadStream.Length)
            //{
            //    var exMessage = string.Format("Wrong number of bytes read for {0}, parser error: read {1} bytes from a {2} byte payload", message.Command, payloadStream.Position, payloadStream.Length);
            //    Debug.WriteLine(exMessage);
            //    throw new Exception(exMessage);
            //}

            return message;
        }
    }
}
