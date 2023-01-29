using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

        private ILogger logger;

        private readonly BlockingCollection<ByteSpan> forLocal = new BlockingCollection<ByteSpan>();
        private readonly BlockingCollection<ByteSpan> forRemote = new BlockingCollection<ByteSpan>();

        /// <summary>
        /// Useful for debug logging, prefer <see cref="AssertPacketsToLocalCountEquals(int)"/> for assertions
        /// </summary>
        public int PacketsForLocalCount => this.forLocal.Count;

        /// <summary>
        /// Useful for debug logging, prefer <see cref="AssertPacketsToRemoteCountEquals(int)"/> for assertions
        /// </summary>
        public int PacketsForRemoteCount => this.forRemote.Count;

        public Semaphore SendToLocalSemaphore = null;
        public Semaphore SendToRemoteSemaphore = null;

        private CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;

        public SocketCapture(IPEndPoint captureEndpoint, IPEndPoint remoteEndPoint, ILogger logger = null)
        {
            this.logger = logger ?? new NullLogger();
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

                for (; ; )
                {
                    byte[] buffer = new byte[2000];
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
            while (!this.cancellationToken.IsCancellationRequested)
            {
                if (this.SendToRemoteSemaphore != null)
                {
                    if (!this.SendToRemoteSemaphore.WaitOne(100))
                    {
                        continue;
                    }
                }

                if (this.forRemote.TryTake(out var packet))
                {
                    this.logger.WriteInfo($"Passed 1 packet of {packet.Length} bytes to remote");
                    this.captureSocket.SendTo(packet.GetUnderlyingArray(), packet.Offset, packet.Length, SocketFlags.None, this.remoteEndPoint);
                }
            }
        }

        private void SendToLocalLoop()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                if (this.SendToLocalSemaphore != null)
                {
                    if (!this.SendToLocalSemaphore.WaitOne(100))
                    {
                        continue;
                    }
                }

                if (this.forLocal.TryTake(out var packet))
                {
                    this.logger.WriteInfo($"Passed 1 packet of {packet.Length} bytes to local");
                    this.captureSocket.SendTo(packet.GetUnderlyingArray(), packet.Offset, packet.Length, SocketFlags.None, this.localEndPoint);
                }
            }
        }

        public void AssertPacketsToLocalCountEquals(int pktCnt)
        {
            DateTime start = DateTime.UtcNow;
            while (this.forLocal.Count != pktCnt)
            {
                if ((DateTime.UtcNow - start).TotalSeconds >= 5)
                {
                    Assert.AreEqual(pktCnt, this.forLocal.Count);
                }

                Thread.Yield();
            }
        }

        public void AssertPacketsToRemoteCountEquals(int pktCnt)
        {
            DateTime start = DateTime.UtcNow;
            while (this.forRemote.Count != pktCnt)
            {
                if ((DateTime.UtcNow - start).TotalSeconds >= 5)
                {
                    Assert.AreEqual(pktCnt, this.forRemote.Count);
                }

                Thread.Yield();
            }
        }

        public void DiscardPacketForLocal(int numToDiscard = 1)
        {
            for (int i = 0; i < numToDiscard; ++i)
            {
                this.forLocal.Take();
            }
        }

        public void DiscardPacketForRemote(int numToDiscard = 1)
        {
            for (int i = 0; i < numToDiscard; ++i)
            {
                this.forRemote.Take();
            }
        }

        public ByteSpan PeekPacketForLocal()
        {
            return this.forLocal.First();
        }

        public void ReorderPacketsForRemote(Action<List<ByteSpan>> reorderCallback)
        {
            List<ByteSpan> buffer = new List<ByteSpan>();
            while (this.forRemote.TryTake(out var pkt)) buffer.Add(pkt);
            reorderCallback(buffer);
            foreach (var item in buffer)
            {
                this.forRemote.Add(item);
            }
        }

        public void ReorderPacketsForLocal(Action<List<ByteSpan>> reorderCallback)
        {
            List<ByteSpan> buffer = new List<ByteSpan>();
            while (this.forLocal.TryTake(out var pkt)) buffer.Add(pkt);
            reorderCallback(buffer);
            foreach (var item in buffer)
            {
                this.forLocal.Add(item);
            }
        }

        internal void ReleasePacketsToLocal(int numToSend)
        {
            var newExpected = this.forLocal.Count - numToSend;
            this.SendToLocalSemaphore.Release();
            this.AssertPacketsToLocalCountEquals(newExpected);
        }
    }
}