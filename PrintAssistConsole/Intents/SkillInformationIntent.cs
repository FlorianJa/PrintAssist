using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{
    [IntentAttribute("SkillInformation")]
    public class SkillInformation : BaseIntent
    {
        public override string Process()
        {
            return "Ich bin dein 3D Druck Experte. Ich kann dir den Drucker und den 3D Druck Workflow erklären." +
                " Außerdem kann ich für dich 3D Modelle im Internet suche, die Modelle für Druck vorbereiten und anschließend den Druck starten." +
                " Stell mir gerne Fragen zum 3D Druck. Ich weiß zwar noch nicht alles, aber ich lerne kontinuierlich dazu.";
        }
    }
}
