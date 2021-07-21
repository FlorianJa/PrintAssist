using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("Tutorial.Next")]
    public class TutorialNext:BaseIntent
    {

        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("Tutorial.Cancel")]
    public class TutorialCancel : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("Tutorial.Yes")]
    public class TutorialYes : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("Tutorial.No")]
    public class TutorialNo : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }


}
