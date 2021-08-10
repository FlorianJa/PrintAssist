using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.Intents
{

    [IntentAttribute("3DPrinting")]
    public class Printing : BaseIntent
    {
        public override string Process()
        {
            return "3D Druck ist ein Fertigunsverfahren, mit dem fast jede Modell herstellen kann.";
        }
    }
    [IntentAttribute("Material")]
    public class Material : BaseIntent
    {
        public override string Process()
        {
            return "Der Drucker druckt mit relativ einfachem Kunststoff, welcher sich auf der Spule befindet. Das Material wird auch Filament genannt.";
        }
    }

    [IntentAttribute("BuildVolume")]
    public class BuildVolume : BaseIntent
    {
        public override string Process()
        {
            return "Der Drucker hat ein Druckvolumen von 18cm x 18cm x 18cm. Wenn du mehr zu dem Drucker erfahren willst, schreib einfach \"Hardware Tutorial starten\".";
        }
    }

    [IntentAttribute("PrinterModel")]
    public class PrinterModel : BaseIntent
    {
        public override string Process()
        {
            return "Das ist der Prusa Mini. Wenn du mehr zu dem Drucker erfahren willst, schreib einfach \"Hardware Tutorial starten\".";
        }
    }

    [IntentAttribute("Filament.Costs")]
    public class FilamentCosts : BaseIntent
    {
        public override string Process()
        {
            return "Das Filament kostet ungefähr 20€ pro Spule. Je nach Hersteller, Material oder Größe der Spule kann der Preis aber variieren.";
        }
    }

    [IntentAttribute("PrintNowNo")]
    public class PrintNowNo : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("UserName")]
    public class UserName : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("PrintStartYes")]
    public class PrintStartYes : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("PrintNowYes")]
    public class PrintNowYes : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("CheckFilamentNext")]
    public class CheckFilamentNext : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("PrintComponentsInformation")]
    public class PrintComponentsInformation : BaseIntent
    {
        public override string Process()
        {
            return "Im Workflow Tutorial findest du zu diesem Thema mehr Informationen. Schreib einfach \"Workflow Tutorial starten\" umd mehr zu erfahren.";
        }
    }

    [IntentAttribute("Filament.Properties")]
    public class FilamentProperties : BaseIntent
    {
        public override string Process()
        {
            return "Das Material wird ab ungefähr 170°C zähflüssig. Im Hardwre Tutorial findest du zu diesem Thema mehr Informationen. Schreib einfach \"Hardware Tutorial starten\" umd mehr zu erfahren.";
        }
    }

    [IntentAttribute("CheckFilament")]
    public class CheckFilament : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }

    [IntentAttribute("Filament.Purchase")]
    public class FilamentPurchase : BaseIntent
    {
        public override string Process()
        {
            return "Filament kannst du bei den meisten Online-Händlern bestellen. In Zukunft kann ich auch Material für dich bestellen.";
        }
    }

    [IntentAttribute("CheckFilamentCancel")]
    public class CheckFilamentCancel : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }


    [IntentAttribute("Printer.Costs")]
    public class PrinterCosts : BaseIntent
    {
        public override string Process()
        {
            return "Der Drucker kostet 419€.";
        }
    }

    [IntentAttribute("Filament")]
    public class Filament : BaseIntent
    {
        public override string Process()
        {
            return "Auf der Spule befindet sich das Material mit dem der Drucker druckt. Es wird auch Filament genannt"; 
        }
    }

    [IntentAttribute("Create3DModel")]
    public class Create3DModel : BaseIntent
    {
        public override string Process()
        {
            throw new NotImplementedException();
        }
    }
}
