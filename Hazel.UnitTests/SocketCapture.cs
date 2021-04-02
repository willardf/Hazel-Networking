using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Hazel.UnitTests
{
    /// <summary>
    /// Acts as an intermediate between to sockets.
    /// 
    /// Use SendToLocalSemaphore and SendToRemoteSemaphore for
    /// explicit control of packet flow.
    /// </summary>
    public class SocketCapture : IDisposable
    {
        private IPEndPoint localEndPoint;
        private readonly IPEndPoint remoteEndPoint;

        private Socket captureSocket;

        private Thread receiveThread;
        private Thread forLocalThread;
        private Thread forRemoteThread;

        private readonly BlockingCollection<ByteSpan> forLocal = new BlockingCollection<ByteSpan>();
        private readonly BlockingCollection<ByteSpan> forRemote = new BlockingCollection<ByteSpan>();

        public Semaphore SendToLocalSemaphore = null;
        public Semaphore SendToRemoteSemaphore = null;

        private CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;

        public SocketCapture(IPEndPoint captureEndpoint, IPEndPoint remoteEndPoint)
        {
            this.cancellationToken = this.cancellationSource.Token;

            this.remoteEndPoint = remoteEndPoint;

            this.captureSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.captureSocket.Bind(captureEndpoint);

            this.receiveThread = new Thread(this.ReceiveLoop);
            this.receiveThread.Start();

            this.forLocalThread = new Thread(this.SendToLocalLoop);
            this.forLocalThread.Start();

            this.forRemoteThread = new Thread(this.SendToRemoteLoop);
            this.forRemoteThread.Start();
        }

        public void Dispose()
        {
            if (this.cancellationSource != null)
            {
                this.cancellationSource.Cancel();
                this.cancellationSource.Dispose();
                this.cancellationSource = null;
            }

            if (this.captureSocket != null)
            {
                this.captureSocket.Close();
                this.captureSocket.Dispose();
                this.captureSocket = null;
            }

            if (this.receiveThread != null)
            {
                this.receiveThread.Join();
                this.receiveThread = null;
            }

            if (this.forLocalThread != null)
            {
                this.forLocalThread.Join();
                this.forLocalThread = null;
            }

            if (this.forRemoteThread != null)
            {
                this.forRemoteThread.Join();
                this.forRemoteThread = null;
            }

            GC.SuppressFinalize(this);
        }

        private void ReceiveLoop()
        {
            try
            {
                IPEndPoint fromEndPoint = new IPEndPoint(IPAddress.Any, 0);

                byte[] buffer = new byte[2000];
                for (; ; )
                {
                    EndPoint endPoint = fromEndPoint;
                    int read = this.captureSocket.ReceiveFrom(buffer, ref endPoint);
                    if (read > 0)
                    {
                        // from the remote endpoint?
                        if (IPEndPoint.Equals(endPoint, remoteEndPoint))
                        {
                            this.forLocal.Add(new ByteSpan(buffer, 0, read));
                        }
                        else
                        {
                            this.localEndPoint = (IPEndPoint)endPoint;
                            this.forRemote.Add(new ByteSpan(buffer, 0, read));
                        }
                    }
                }
            }
            catch (SocketException)
            {
            }
            finally
            {
                this.forLocal.CompleteAdding();
                this.forRemote.CompleteAdding();
            }
        }

        private void SendToRemoteLoop()
        {
            foreach (ByteSpan packet in this.forRemote.GetConsumingEnumerable())
            {
                if (this.cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (this.SendToRemoteSemaphore != null)
                {
                    if (!this.SendToRemoteSemaphore.WaitOne(100))
                    {
                        continue;
                    }
                }

                this.captureSocket.SendTo(packet.GetUnderlyingArray(), packet.Offset, packet.Length, SocketFlags.None, this.remoteEndPoint);
            }
        }

        private void SendToLocalLoop()
        {
            foreach (ByteSpan packet in this.forLocal.GetConsumingEnumerable())
            {
                if (this.cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (this.SendToLocalSemaphore != null)
                {
                    if (!this.SendToLocalSemaphore.WaitOne(100))
                    {
                        continue;
                    }
                }

                this.captureSocket.SendTo(packet.GetUnderlyingArray(), packet.Offset, packet.Length, SocketFlags.None, this.localEndPoint);
            }
        }
    }
}
