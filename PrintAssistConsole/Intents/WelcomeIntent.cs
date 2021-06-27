using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("Default Welcome Intent")]
    public class WelcomeIntent:BaseIntent
    {
        private List<string> messages = new List<string> { "Hallo!", "Guten Tag!", "Ich grüße dich!" };
        public override string Process() 
        {
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            Random rnd = new Random((Int32)(DateTime.Now.Ticks % Int32.MaxValue));
            return messages[rnd.Next(messages.Count)];
        }
    }
}
