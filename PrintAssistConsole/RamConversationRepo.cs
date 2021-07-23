using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    public class RamConversationRepo : IConversationRepo
    {
        private Dictionary<Int64, Conversation> conversations;


        public RamConversationRepo()
        {
            conversations = new Dictionary<long, Conversation>();
        }

        public void AddConversation(long id, Conversation conversation)
        {
            if(conversations.ContainsKey(id))
            {
                conversations[id] = conversation;
            }
            else
            {
                conversations.Add(id, conversation);
            }
        }

        public Conversation GetConversationById(long id)
        {
            Conversation conversation;
            conversations.TryGetValue(id, out conversation);
            return conversation;
        }

        public bool TryGetUserStateById(long id, out ConversationState state)
        {
            Conversation conversation;
            conversations.TryGetValue(id, out conversation);

            if(conversation == null)
            {
                state = ConversationState.Unknown;
                return false;
            }
            else
            {
                state = conversation.CurrentState;
                return true;
            }
        }

        //public UserState GetUserStateById(long id)
        //{
        //    User user;
        //    users.TryGetValue(id, out user);

        //    if (user == null)
        //    {
        //        throw new ArgumentOutOfRangeException("id");
        //    }
        //    else
        //    {
        //        return user.CurrentState;
        //    }
        //}
    }
}
