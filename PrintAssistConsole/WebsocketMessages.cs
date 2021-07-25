using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets._ExtendedPrinter.Scripts.SlicingService
{
    [Serializable]
    public class FileSlicedMessageArgs
    {
        public string File;// { get; set; }
        public string FilamentLength;// { get; set; }
        public string PrintTime;// { get; set; }
    }

    public class FileSlicedMessage
    {
        public string MessageType;
        public FileSlicedArgs Payload;// { get; set; }
    }

    public class FileSlicedArgs
    {
        public string SlicedFilePath;
        public int Days;
        public int Hours;
        public int Minutes;
        public float UsedFilament;
        public bool Success;
    }


    public class ProfileListMessage 
    {
        public string MessageType;
        public List<string> Payload;
    }


}
