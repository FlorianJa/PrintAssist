using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Classes
{
    public class User
    {
        public Int64 Id { get; private set; }
        public UserState CurrentState { get; set; }
        public string Name { get; internal set; }

        public User(Int64 id)
        {
            Id = id;
        }

    }
}
