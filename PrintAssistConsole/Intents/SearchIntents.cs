using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("NewSearchTerm")]
    public class NewSearchTerm : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("AbortSearch")]
    public class AbortSearch : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("AskToSliceFile.No")]
    public class AskToSliceFileNo : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("AskToSliceFile.Yes")]
    public class AskToSliceFileYes : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("SearchModel")]
    public class SearchModel : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    
}
