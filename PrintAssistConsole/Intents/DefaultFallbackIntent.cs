using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("Default Fallback Intent")]
    public class DefaultFallbackIntent : BaseIntent
    {
        private List<string> messages = new List<string> {  "Ich habe dich leider nicht verstanden.",
                                                            "Ich verstehe deine Frage leider nicht.",
                                                            "Entschuldige bitte, ich habe deine Frage nicht verstanden.",
                                                            "Leider kann ich nicht verstehen, was du von mir möchtest.",
                                                            "Kannst du das noch mal anders formulieren?" };

        public override string Process()
        {
            var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            Random rnd = new Random((Int32)(DateTime.Now.Ticks % Int32.MaxValue));
            return messages[rnd.Next(messages.Count)];
        }
    }
}
