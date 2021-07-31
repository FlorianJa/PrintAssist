using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoPrintConnector.Job
{
    
    public class OctoprintJobOperations : OctoprintConnection
    {
        public OctoprintJobOperations(OctoprintServer server) : base(server)
        {

        }

        public async Task<OctoprintJobResponse> GetJobInformationAsync()
        {
            string jobInfo = await GetAsync("api/job");
            return JsonConvert.DeserializeObject<OctoprintJobResponse>(jobInfo);

        }

    }
}
