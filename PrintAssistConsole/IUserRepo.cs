using PrintAssistConsole.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    interface IUserRepo
    {
        bool TryGetUserStateById(Int64 id, out UserState state);
        bool AddUser(Int64 id, User user);
        User GetUserById(Int64 id);
    }
}
