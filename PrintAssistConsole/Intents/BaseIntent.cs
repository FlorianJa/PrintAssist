using Google.Cloud.Dialogflow.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    public abstract class BaseIntent
    {
        public DetectIntentResponse response;
        //public string OutputContext;
        public abstract string Process(); 
    }
}
