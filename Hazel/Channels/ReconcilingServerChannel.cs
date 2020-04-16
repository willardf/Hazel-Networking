using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Channels
{
    public class ReconcilingServerChannel<ClientState, ServerState>
    {
        private struct BufferedServerState
        {
            public ushort SequenceId;
            public bool IsValidated;
            public ServerState Data;

            public BufferedServerState(ushort seq, ServerState state)
            {
                this.SequenceId = seq;
                this.IsValidated = false;
                this.Data = state;
            }
        }

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
        private CircularBuffer<BufferedServerState> serverBuffer;
        private CircularBuffer<BufferedClientState> clientBuffer;

        private readonly int delay;
        private IEqualityComparer<ServerState> comparer;
        private ushort lastSeqFromServer;

        public ReconcilingServerChannel(int size, int delay, IEqualityComparer<ServerState> comparer)
        {
            this.serverBuffer = new CircularBuffer<BufferedServerState>(size);
            this.clientBuffer = new CircularBuffer<BufferedClientState>(size);
            this.delay = delay;
            this.comparer = comparer;
        }

        /// <summary>
        /// Prepares a message from the server.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="appendData">Called when appropriate</param>
        public void SendUpdate(MessageWriter writer, Action<MessageWriter> appendData)
        {
            sequencer.AddSequenceNumber(writer);
            appendData(writer);
        }

        public void ReceiveUpdate(MessageReader reader, Func<MessageReader, ClientState> parseData)
        {
            if (!sequencer.CheckSequenceNumber(reader, out ushort newSeq)) return;

            byte numUpdates = reader.ReadByte();
            for (int updateNum = 0; updateNum < numUpdates; updateNum++)
            {
                var state = parseData(reader);
                ushort seqNum = (ushort)(newSeq - numUpdates + updateNum);

                bool insertEnd = true;
                for (int i = clientBuffer.Count - 1; i >= 0; --i)
                {
                    var entry = clientBuffer[i];
                    if (entry.SequenceId == seqNum)
                    {
                        insertEnd = false;
                        break;
                    }

                    if (SequenceIsNewer(entry.SequenceId, seqNum))
                    {
                        this.clientBuffer.Insert(i, new BufferedClientState(seqNum, state));
                        insertEnd = false;
                        break;
                    }
                }

                if (insertEnd)
                {
                    this.clientBuffer.AddLast(new BufferedClientState(seqNum, state));
                }
            }
        }

        private static bool SequenceIsNewer(ushort newSeq, ushort lastSeqNumber)
        {
            ushort cutoff = (ushort)(lastSeqNumber - 32768);
            if (cutoff < lastSeqNumber)
            {
                if (newSeq > lastSeqNumber || newSeq <= cutoff)
                {
                    lastSeqNumber = newSeq;
                    return true;
                }
            }
            else
            {
                if (newSeq > lastSeqNumber && newSeq <= cutoff)
                {
                    lastSeqNumber = newSeq;
                    return true;
                }
            }

            return false;
        }
    }
}