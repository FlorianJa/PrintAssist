using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("SliceAgain")]
    public class SliceAgain : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("SlicingModeSelection.Guided")]
    public class SlicingModeSelectionGuided : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }
    [IntentAttribute("SlicingModeSelection.Advanced")]
    public class SlicingModeSelectionAdvanced : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }
    [IntentAttribute("SlicingModeSelection.Expert")]
    public class SlicingModeSelectionExpert : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }
}
