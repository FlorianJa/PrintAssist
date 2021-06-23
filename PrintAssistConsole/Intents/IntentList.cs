using Google.Cloud.Dialogflow.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    public class IntentsList : List<KeyValuePair<string, Func<DetectIntentResponse, string>>>
    {
        public void Add(string intentName, Func<DetectIntentResponse, string> function)
        {
            var intent = this.FirstOrDefault(i => i.Key.ToLower() == intentName.ToLower());
            if (string.IsNullOrWhiteSpace(intent.Key))
            {
                Add(new KeyValuePair<string, Func<DetectIntentResponse, string>>(intentName, function));
            }
        }
    }
}
