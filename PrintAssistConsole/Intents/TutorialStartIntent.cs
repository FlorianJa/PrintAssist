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
    public class TutorialStartIntent
    {
        public static string Process(DetectIntentResponse response)
        {
            return "Okay, ich erkläre dir den Drucker.";
        }
    }
}
