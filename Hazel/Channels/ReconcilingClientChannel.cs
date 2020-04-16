using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Channels
{
    public interface IMessageSerializable
    {
        void Serialize(MessageWriter writer);
    }

    public class ReconcilingClientChannel<ClientState, ServerState> where ClientState : IMessageSerializable
    {
        private struct BufferedClientState
        {
            public ushort SequenceId;
            public bool IsValidated;
            public ClientState Data;

            public BufferedClientState(ushort seq, ClientState state)
            {
                this.SequenceId = seq;
                this.IsValidated = false;
                this.Data = state;
            }
        }

        private SequencedChannel sequencer = new SequencedChannel();
        private CircularBuffer<BufferedClientState> circleBuffer;

        private readonly int delay;
        private IClientServerComparer<ClientState, ServerState> reconciler;
        private ushort lastSeqFromServer;

        /// <summary>
        /// Size should be enough to hold a few seconds at your desired update rate.
        /// EG. If you are updating 10/sec, consider 50 or 100 to store 5-10 seconds of inputs for reconciliation.
        /// Balance this against the size of your ClientState and memory usage.
        /// 
        /// Delay should be as small as possible while avoiding mispredictions. Most sources recommend 100ms.
        /// EG. If you are updating 10/sec, consider 10 to buffer 100ms of inputs.
        /// </summary>
        /// <param name="size">The buffer size in number of updates.</param>
        /// <param name="delay">The buffer delay in number of updates.</param>
        /// <param name="comparer">Used to determine if client states match the server outcome</param>
        public ReconcilingClientChannel(int size, int delay, IClientServerComparer<ClientState, ServerState> comparer)
        {
            this.circleBuffer = new CircularBuffer<BufferedClientState>(size);
            this.delay = delay;
            this.reconciler = comparer;
        }

        public bool HandleUpdates(out ClientState state)
        {
            if (this.circleBuffer.Count >= this.delay)
            {
                state = this.circleBuffer.Last.Data;
                this.circleBuffer.RemoveLast();
                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// Prepares a message from the server.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="appendData">Called when appropriate</param>
        public void SendUpdate(MessageWriter writer, ClientState state)
        {
            var seq = sequencer.AddSequenceNumber(writer);

            int pos = writer.Position;
            writer.Write((byte)0);

            byte cnt = 0;
            for (int i = circleBuffer.Count - 5; i < circleBuffer.Count; ++i)
            {
                if (i >= 0)
                {
                    circleBuffer[i].Data.Serialize(writer);
                    cnt++;
                }                
            }

            writer.Position = pos;
            writer.Write(cnt);
            writer.Position = writer.Length;

            circleBuffer.AddLast(new BufferedClientState(seq, state));
        }

        public void ReceiveUpdate(MessageReader reader, Func<MessageReader, ServerState> parseData)
        {
            if (!sequencer.CheckSequenceNumber(reader, out ushort newSeq)) return;
            this.lastSeqFromServer = newSeq;

            ServerState serverData = parseData(reader);
            if (this.circleBuffer.Count == 0)
            {
                var newState = this.reconciler.ConvertServerToClient(serverData);
                this.circleBuffer.AddLast(new BufferedClientState(newSeq, newState));
                return;
            }

            for (int i = 0; i < this.circleBuffer.Count; ++i)
            {
                var node = this.circleBuffer[i];
                if (node.SequenceId == newSeq)
                {
                    // When bundling updates, check IsValidated
                    if (this.reconciler.AreEqual(node.Data, serverData))
                    {
                        node.IsValidated = true;
                        this.circleBuffer[i] = node;
                        return;
                    }
                    else
                    {
                        node.Data = this.reconciler.ConvertServerToClient(serverData);
                        node.IsValidated = true;
                        this.circleBuffer[i] = node;
                        for (i++; i < this.circleBuffer.Count; ++i)
                        {
                            var nextNode = this.circleBuffer[i];
                            nextNode.Data = this.reconciler.Reconcile(node.Data);
                            this.circleBuffer[i] = nextNode;
                            node = nextNode;
                        }
                    }
                }
            }
        }
    }
}
