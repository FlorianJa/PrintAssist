using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    public interface ITutorialDataProvider
    {
        int GetMessageCount();
        TutorialMessage GetMessage(int stepnumber);
    }
       
}
