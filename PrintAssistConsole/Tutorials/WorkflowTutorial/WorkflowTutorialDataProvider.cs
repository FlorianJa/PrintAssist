using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{ 
    public class WorkflowTutorialDataProvider : ITutorialDataProvider
    {
        public Dictionary<WorkflowTutorialState, Message> messages { get; set; }

        public WorkflowTutorialDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = File.OpenText(@".\BotContent\WorkflowTutorial_de.json"))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    messages = serializer.Deserialize<Dictionary<WorkflowTutorialState, Message>>(jsonReader);
                }
            }
        }

        public Message GetMessage(int state)
        {
            return messages[(WorkflowTutorialState)state];
        }

        public int GetMessageCount()
        {
            return messages.Count;
        }
    }
}
