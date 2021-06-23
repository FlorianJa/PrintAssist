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
        public static string Process(DetectIntentResponse response) { throw new NotImplementedException(); }
    }
}
