using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("TutorialStarten")]
    public class TutorialStartIntent:BaseIntent
    {
        public override string Process()
        {
            return "Okay, ich erkläre dir den Drucker.";
        }
    }
}
