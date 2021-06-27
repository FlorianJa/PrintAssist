using PrintAssistConsole.Classes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    public class RamUserRepo : IUserRepo
    {
        private Dictionary<Int64, Classes.User> users;


        public RamUserRepo()
        {
            users = new Dictionary<long, User>();
        }

        public bool AddUser(long id, User user)
        {
            return users.TryAdd(id, user);
        }

        public User GetUserById(long id)
        {
            User user;
            users.TryGetValue(id, out user);
            return user;
        }

        public bool TryGetUserStateById(long id, out UserState state)
        {
            User user;
            users.TryGetValue(id, out user);

            if(user == null)
            {
                state = UserState.Unknown;
                return false;
            }
            else
            {
                state = user.CurrentState;
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
