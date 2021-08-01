using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    public class HardwareTutorialDataProvider : ITutorialDataProvider
    {
        public Dictionary<HardwareTutorialState,Message> messages { get; set; }

        public HardwareTutorialDataProvider(string path)
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = File.OpenText(path))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    messages = serializer.Deserialize<Dictionary<HardwareTutorialState, Message>>(jsonReader);
                } 
            }
        }

        public Message GetMessage(int state)
        {
            return messages[(HardwareTutorialState)state];
        }

        public int GetMessageCount()
        {
            return messages.Count;
        }
    }
}
