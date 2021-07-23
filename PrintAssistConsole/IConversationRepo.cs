using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    interface IConversationRepo
    {
        bool TryGetUserStateById(Int64 id, out ConversationState state);
        void AddConversation(Int64 id, Conversation conversation);
        Conversation GetConversationById(Int64 id);
    }
}
