using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitSharp.Common.ExtensionMethods;
using System.Collections.Immutable;

namespace BitSharp.Network
{
    public class RemoteNode : IDisposable
    {
        public event Action<RemoteNode, GetBlocksPayload> OnGetBlocks;
        public event Action<RemoteNode, GetBlocksPayload> OnGetHeaders;
        public event Action<RemoteNode, ImmutableArray<byte>> OnPing;
        public event Action<RemoteNode> OnDisconnect;

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

        private bool startedConnecting = false;
        private bool isConnected = false;
        private /*readonly*/ IPEndPoint localEndPoint;
        private readonly IPEndPoint remoteEndPoint;
        private readonly Socket socket;
        private readonly RemoteReceiver receiver;
        private readonly RemoteSender sender;

        public RemoteNode(IPEndPoint remoteEndPoint)
        {
            this.remoteEndPoint = remoteEndPoint;

            this.socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.receiver = new RemoteReceiver(this.socket, persistent: false);
            this.sender = new RemoteSender(this.socket);

            WireNode();
        }

        public RemoteNode(Socket socket)
        {
            this.socket = socket;
            this.isConnected = true;

            this.localEndPoint = (IPEndPoint)socket.LocalEndPoint;
            this.remoteEndPoint = (IPEndPoint)socket.RemoteEndPoint;

            this.receiver = new RemoteReceiver(this.socket, persistent: false);
            this.sender = new RemoteSender(this.socket);

            WireNode();
        }

        ~RemoteNode() { ((IDisposable)this).Dispose(); }

        void IDisposable.Dispose()
        {
            Disconnect();
        }

        public IPEndPoint LocalEndPoint { get { return this.localEndPoint; } }

        public IPEndPoint RemoteEndPoint { get { return this.remoteEndPoint; } }

        public RemoteReceiver Receiver { get { return this.receiver; } }

        public RemoteSender Sender { get { return this.sender; } }

        public bool IsConnected { get { return this.isConnected; } }

        public async Task ConnectAsync()
        {
            await semaphore.DoAsync(async () =>
            {
                try
                {
                    if (!IsConnected)
                    {
                        this.startedConnecting = true;

                        await Task.Factory.FromAsync(this.socket.BeginConnect(this.remoteEndPoint, null, null), this.socket.EndConnect);

                        this.localEndPoint = (IPEndPoint)this.socket.LocalEndPoint;

                        this.isConnected = true;
                    }
                }
                catch (Exception)
                {
                    //Debug.WriteLine(string.Format("Error on connecting to {0}: {1}", remoteEndPoint, e.Message));
                    Disconnect();
                }
            });
        }

        public void Disconnect()
        {
            UnwireNode();

            try
            {
                this.socket.Dispose();
            }
            catch (Exception) { }

            if (this.startedConnecting || this.isConnected)
            {
                this.startedConnecting = false;
                this.isConnected = false;
                //TODO GC.SuppressFinalize(this);

                var handler = this.OnDisconnect;
                if (handler != null)
                    handler(this);
            }
        }

        private void WireNode()
        {
            this.receiver.OnFailed += HandleFailed;
            this.sender.OnFailed += HandleFailed;
            this.receiver.OnGetBlocks += HandleGetBlocks;
            this.receiver.OnGetHeaders += HandleGetHeaders;
            this.receiver.OnPing += HandlePing;
        }

        private void UnwireNode()
        {
            this.receiver.OnFailed -= HandleFailed;
            this.sender.OnFailed -= HandleFailed;
            this.receiver.OnGetBlocks -= HandleGetBlocks;
            this.receiver.OnGetHeaders -= HandleGetHeaders;
            this.receiver.OnPing -= HandlePing;
        }

        private void HandleFailed(Exception e)
        {
            //Debug.WriteLine(string.Format("Remote peer {0} failed, disconnecting: {1}", this.remoteEndPoint, e.Message));
            Disconnect();
        }

        private void HandleGetBlocks(GetBlocksPayload payload)
        {
            var handler = this.OnGetBlocks;
            if (handler != null)
                handler(this, payload);
        }

        private void HandleGetHeaders(GetBlocksPayload payload)
        {
            var handler = this.OnGetHeaders;
            if (handler != null)
                handler(this, payload);
        }

        private void HandlePing(ImmutableArray<byte> payload)
        {
            var handler = this.OnPing;
            if (handler != null)
                handler(this, payload);
        }
    }
}
