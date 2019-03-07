using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hazel.Udp
{
    ///
    public class UdpBroadcaster : IDisposable
    {
        ///
        private Socket socket;

        ///
        private byte[] data;

        ///
        private EndPoint endpoint;

        ///
        public UdpBroadcaster(int port)
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            this.endpoint = new IPEndPoint(IPAddress.Broadcast, port);
        }

        ///
        public void SetData(string data)
        {
            int len = UTF8Encoding.UTF8.GetByteCount(data);
            this.data = new byte[len + 2];
            this.data[0] = 4;
            this.data[1] = 2;

            UTF8Encoding.UTF8.GetBytes(data, 0, data.Length, this.data, 2);
        }

        ///
        public void Broadcast()
        {
            if (this.data == null)
            {
                return;
            }

            this.socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, this.endpoint, (evt) => this.socket.EndSendTo(evt), null);
        }

        ///
        public void Dispose()
        {
            if (this.socket != null)
            {
                try
                {
                    this.socket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                try
                {
                    this.socket.Close();
                }
                catch { }
                try
                {
                    this.socket.Dispose();
                }
                catch { }
                this.socket = null;
            }
        }
    }
}