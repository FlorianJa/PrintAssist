using Google.Cloud.Dialogflow.V2;
using Google.Protobuf.WellKnownTypes;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("GetränkeBestellung")]
    public class DrinkOrderIntent
    {
        public static string Process(DetectIntentResponse response)
        {
            var getraenke = new Value();
            var amount = new Value();

            response.QueryResult.Parameters.Fields.TryGetValue("Getraenk", out getraenke);
            response.QueryResult.Parameters.Fields.TryGetValue("number", out amount);
            var ret = "Deine Bestellung lautet: ";

            for (int i = 0; i < getraenke.ListValue.Values.Count; i++)
            {
                ret += amount.ListValue.Values[i] + " " + getraenke.ListValue.Values[i];
            }

            return ret;
        }
    }
}
