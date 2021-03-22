using Hazel.Udp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.UnitTests
{
    internal class UdpConnectionTestHarness : UdpConnection
    {
        public List<MessageReader> BytesSent = new List<MessageReader>();
        public ushort ReliableReceiveLast => this.reliableReceiveLast;


        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            this.State = ConnectionState.Connected;
        }

        public override void ConnectAsync(byte[] bytes = null)
        {
            this.State = ConnectionState.Connected;
        }

        protected override bool SendDisconnect(MessageWriter writer)
        {
            lock (this)
            {
                if (this.State != ConnectionState.Connected)
                {
                    return false;
                }

                this.State = ConnectionState.NotConnected;
            }

            return true;
        }

        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            this.BytesSent.Add(MessageReader.Get(bytes));
        }

        public void Test_Receive(MessageWriter msg)
        {
            byte[] buffer = new byte[msg.Length];
            Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

            var data = MessageReader.Get(buffer);
            this.HandleReceive(data, data.Length);
        }
    }
}
