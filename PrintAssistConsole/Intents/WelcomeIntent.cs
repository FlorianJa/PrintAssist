﻿using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("Default Welcome Intent")]
    public class WelcomeIntent
    {
        public static string Process(DetectIntentResponse response) { throw new NotImplementedException(); }
    }
}
