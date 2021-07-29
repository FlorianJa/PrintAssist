using Google.Api.Gax;
using Google.Cloud.Dialogflow.V2;
using PrintAssistConsole.Intents;
using PrintAssistConsole.Utilies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole
{
    public class IntentDetector
    {
        private static string _agentId;
        private static string _dialogFlowAPIKeyFile;
        private static Dictionary<string, Type> intentMap;
        private static Dictionary<string, ProcessDelegate> intentHandlers;

        private static IntentDetector mInstance;

        public static IntentDetector Instance
        {
            get
            {
                if (mInstance == null)
                {
                    throw new Exception("Object not created");
                }
                return mInstance;
            }
        }
        private IntentDetector(string agentId, string PathToAPIKeyFile)
        {
            _agentId = agentId;
            _dialogFlowAPIKeyFile = PathToAPIKeyFile;
        }

        public static async Task CreateAsync(string agentId, string dialogFlowAPIKeyFile)
        {
            if (mInstance != null)
            {
                throw new Exception("Object already created");
            }
            mInstance = new IntentDetector(agentId, dialogFlowAPIKeyFile);
            await SetupIntentMappingAsync();
        }

        public async Task<object> CallDFAPIAsync(long sessionId, string message)
        {
            return await CallDFAPIAsync(sessionId, message, contextName: null) ;
        }

        public async Task<object> CallDFAPIAsync(long sessionId, string message, string contextName, bool clearContext = false)
        {
            var tmp = new List<string>();
            if(contextName != null)
            {
                tmp.Add(contextName);
            }

            return await CallDFAPIAsync(sessionId, message, tmp, clearContext);
        }

        public async Task<object> CallDFAPIAsync(long sessionId, string message, List<string> contextNames, bool clearContext = false)
        {
            
            var query = new QueryInput
            {
                Text = new TextInput
                {
                    Text = message,
                    LanguageCode = "de"
                }
            };

            var request = new DetectIntentRequest
            {
                SessionAsSessionName = new SessionName(_agentId, sessionId.ToString()),
                QueryInput = query,
                QueryParams = new QueryParameters()
            };

            request.QueryParams.ResetContexts = clearContext;

            if (contextNames != null)
            {
                foreach (var contextName in contextNames)
                {
                    var context = new Context
                    {
                        ContextName = new ContextName(_agentId, sessionId.ToString(), contextName),
                        LifespanCount = 3,
                    };
                    request.QueryParams.Contexts.Add(context);
                }
            }
            else
            {
                request.QueryParams.ResetContexts = clearContext;
            }

            SessionsClient client = new SessionsClientBuilder
            {
                CredentialsPath = _dialogFlowAPIKeyFile
            }.Build();

            var response = await client.DetectIntentAsync(request);

            Type type;
            try
            {
                type = intentMap[response.QueryResult.Intent.DisplayName]; //key not found exception could be thrown here

            }
            catch (Exception)
            {
                return null;
            }
            var intent = (BaseIntent)Activator.CreateInstance(type);
            intent.response = response;
            return intent;
        }

        private static async Task SetupIntentMappingAsync()
        {
            intentHandlers = new Dictionary<string, ProcessDelegate>();
            intentMap = new Dictionary<string, System.Type>();
            //Get all types with custom attribute IntentAttribute in assembly
            var intentClasses =
            from a in AppDomain.CurrentDomain.GetAssemblies()
            from t in a.GetTypes()
            let attributes = t.GetCustomAttributes(typeof(IntentAttribute), true)
            where attributes != null && attributes.Length > 0
            select new { Type = t, Attributes = attributes.Cast<IntentAttribute>() };

            foreach (var type in intentClasses)
            {

                //get intent name from attribute
                var intentName = ((IntentAttribute)(Attribute.GetCustomAttribute(type.Type, typeof(IntentAttribute)))).IntentName;
                intentMap.Add(intentName, type.Type);

                //get method info for Process method
                var mi = type.Type.GetMethod("Process", BindingFlags.Public | BindingFlags.Instance);

                if (mi != null)
                {
                    //store intent name and connected Process method in dictionary
                    intentHandlers.Add(intentName, (ProcessDelegate)mi.CreateDelegate(typeof(ProcessDelegate), null));
                }
                else
                {
                    throw new NotImplementedException("Process method is not implemented for class: " + type.Type.Name);
                }
            }

            var intents = await GetAllIntentsForAgentAsync();
            foreach (var intent in intents)
            {
                //check if all intents are implemented
                if (!intentHandlers.ContainsKey(intent.DisplayName))
                {
                    Console.WriteLine("No class for " + intent.DisplayName);
                }
            }
        }

        private static async Task<Page<Intent>> GetAllIntentsForAgentAsync()
        {
            ListIntentsRequest request = new ListIntentsRequest
            {
                Parent = $"projects/{_agentId}/agent"
            };

            IntentsClient client = new IntentsClientBuilder
            {
                CredentialsPath = _dialogFlowAPIKeyFile
            }.Build();

            var intents = client.ListIntentsAsync(request);

            //ToDo: check if there are more than 100 intents
            return await intents.ReadPageAsync(100);
        }
    } 
}



