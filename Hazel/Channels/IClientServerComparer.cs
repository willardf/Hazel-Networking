using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Channels
{
    public interface IClientServerComparer<ClientState, ServerState>
    {
        bool AreEqual(ClientState clientState, ServerState serverState);

        /// <summary>
        /// Converts a ServerState into a client state with the understanding
        /// </summary>
        ClientState ConvertServerToClient(ServerState serverState);

        /// <summary>
        /// Create a next client state based on a last-good previous state.
        /// </summary>
        ClientState Reconcile(ClientState lastGood);
    }
}
