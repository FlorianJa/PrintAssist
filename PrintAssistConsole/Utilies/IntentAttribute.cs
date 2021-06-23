using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Utilies
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class IntentAttribute : System.Attribute
    {
        public string IntentName;

        public IntentAttribute(string name)
        {
            this.IntentName = name;
        }
    }
    
}
