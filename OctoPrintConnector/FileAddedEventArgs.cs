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
}
