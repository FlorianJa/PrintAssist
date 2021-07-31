using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OctoPrintConnector.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OctoPrintConnector.Operations
{
    public class OctoprintGeneral: OctoprintConnection
    {
        public OctoprintGeneral(OctoprintServer server) : base(server)
        {

        }

        public LoginResponse Login()
        {
            JObject data = new JObject
            {
                { "passive", server.ApplicationKey }
            };

            var response = PostJson("api/login", data);
            return JsonConvert.DeserializeObject<LoginResponse>(response);
        }
 
    
        public async Task<Stream> GetSnapShotAsync()
        {
            using (WebClient webclient = new WebClient())
            {
                try
                {
                    var uri = new Uri("http://" + server.DomainNmaeOrIp + ":8080/?action=snapshot");
                    return await webclient.OpenReadTaskAsync(uri);
                }
                catch (Exception e)
                {
                    
                }
            }

            return null;
        }
    }
}
