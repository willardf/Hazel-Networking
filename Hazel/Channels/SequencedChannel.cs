using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Channels
{
    /// <summary>
    /// A helper class for creating a sequenced reliable or unreliable channel.
    /// * Not thread safe.
    /// * Unidirectional, you should only be sending OR receiving with one instance.
    ///   * This class has methods for both, but it's uncommon to need bidirectional sequencing.
    /// * There should be one per instance where a sequenced channel is needed.
    ///   * EG. Each character should have their own for filtering out old position updates.
    ///   * If two characters (or a class) shared one, one or more instances may be starved of updates.
    /// </summary>
    public class SequencedChannel
    {
        private ushort seqNumber;

        /// <summary>
        /// Writes an incrementing sequence number to a message.
        /// Generally used on the server-side.
        /// </summary>
        public ushort AddSequenceNumber(MessageWriter writer)
        {
            ushort output = seqNumber++;
            writer.Write(output);
            return output;
        }

        /// <summary>
        /// Reads an incrementing sequence number from a message and returns if the message is newer.
        /// Generally used on the client-side.
        /// </summary>
        /// <returns>True</returns>
        public bool CheckSequenceNumber(MessageReader reader, out ushort newSeq)
        {
            ushort cutoff = (ushort)(seqNumber - 32768);
            newSeq = reader.ReadUInt16();

            if (cutoff < seqNumber)
            {
                if (newSeq > seqNumber || newSeq <= cutoff)
                {
                    seqNumber = newSeq;
                    return true;
                }
            }
            else
            {
                if (newSeq > seqNumber && newSeq <= cutoff)
                {
                    seqNumber = newSeq;
                    return true;
                }
            }

            return false;
        }
    }
}
