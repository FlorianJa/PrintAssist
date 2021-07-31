using OctoPrintConnector.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPrintConnector
{
    public class FileAddedEventArgs : EventArgs
    {
        public Payload Payload{get;set;}

        public FileAddedEventArgs(Payload payload)
        {
            Payload = payload;
        }

    }

    public class TemperReceivedEventArgs : EventArgs
    {
        public float ToolActual { get; }
        public float ToolTarget { get; }
        public float BedActual { get; }
        public float BedTarget { get; }
        
        public TemperReceivedEventArgs(float toolActual, float toolTarget, float bedActual, float bedTarget)
        {
            ToolActual = toolActual;
            ToolTarget = toolTarget;
            BedActual = bedActual;
            BedTarget = bedTarget;
        }
    }
}
