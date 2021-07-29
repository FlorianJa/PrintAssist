using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PrintAssistConsole.ThingiverseAPI
{

    public class ThingiverseAPIWrapper
    {
        public static string Token;

        public static async Task<Things> SearchThingsAsync(string searchTerm, int page, int pageCount)
        {
            if(string.IsNullOrEmpty(Token))
            {
                throw new ArgumentNullException("Token is not set.");
            }

            Things myDeserializedThings = null;
            using (HttpClient client = new HttpClient())
            {
                UriBuilder searchThing = new UriBuilder($"https://api.thingiverse.com/search/{searchTerm}");
                // set the query param type to things
                //searchThing.Query = "type=things";
                //searchThing.Query = "per_page=5";
                // include access token as query parameter
                searchThing.Query = $"type=things&page={page}&per_page={pageCount}&access_token={Token}";

                HttpResponseMessage response = await client.GetAsync(searchThing.Uri);
                if (response.IsSuccessStatusCode)
                {
                    var things = response.Content.ReadAsStringAsync().Result;
                    // Deserialize the json response
                    try
                    {
                        myDeserializedThings = JsonConvert.DeserializeObject<Things>(things);
                    }
                    catch (Exception)
                    {

                    }
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                }
            }
            return myDeserializedThings;
        }

        public static async Task<Tuple<int, string>> GetImageURLByThingId(int id)
        {
            if (string.IsNullOrEmpty(Token))
            {
                throw new ArgumentNullException("Token is not set.");
            }

            List<FilesResponse> files = null;
            using (HttpClient client = new HttpClient())
            {
                UriBuilder searchThing = new UriBuilder($"https://api.thingiverse.com/things/{id}/files");
                // set the query param type to things
                //searchThing.Query = "type=things";
                //searchThing.Query = "per_page=5";
                // include access token as query parameter
                searchThing.Query = $"&access_token={Token}";

                HttpResponseMessage response = await client.GetAsync(searchThing.Uri);
                if (response.IsSuccessStatusCode)
                {
                    var fileResponse = response.Content.ReadAsStringAsync().Result;
                    // Deserialize the json response
                    try
                    {
                        files = JsonConvert.DeserializeObject<List<FilesResponse>>(fileResponse);

                        string url = null;
                        int fileId = -1;
                        var firstSTLFile = files.Where(x => x.name.EndsWith(".stl")).First();
                        if (firstSTLFile != null)
                        {
                            var file = firstSTLFile.default_image?.sizes.Where(x => x.size == "large" && x.type == "display").FirstOrDefault();

                            url = file.url;
                            fileId = firstSTLFile.id;
                            return new Tuple<int, string>(fileId,url);
                        }

                    }
                    catch (Exception)
                    {

                    }
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                }
            }

            return null;
        }

        internal static async Task<Tuple<string, string>> GetDownloadLinkForFileById(int thingId, int fileId)
        {
            if (string.IsNullOrEmpty(Token))
            {
                throw new ArgumentNullException("Token is not set.");
            }

            FileDetail fileDetail = null;
            using (HttpClient client = new HttpClient())
            {
                UriBuilder searchThing = new UriBuilder($"https://api.thingiverse.com/things/{thingId}/files/{fileId}");
                // set the query param type to things
                //searchThing.Query = "type=things";
                //searchThing.Query = "per_page=5";
                // include access token as query parameter
                searchThing.Query = $"&access_token={Token}";

                HttpResponseMessage response = await client.GetAsync(searchThing.Uri);
                if (response.IsSuccessStatusCode)
                {
                    var fileResponse = response.Content.ReadAsStringAsync().Result;
                    // Deserialize the json response
                    try
                    {
                        fileDetail = JsonConvert.DeserializeObject<FileDetail>(fileResponse);
                        return new Tuple<string, string>(fileDetail.name, fileDetail.public_url);
                    }
                    catch (Exception)
                    {

                    }
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                }
            }
            return null;
        }
    }



    public class Size
    {
        public string type { get; set; }
        public string size { get; set; }
        public string url { get; set; }
    }

    public class DefaultImage
    {
        public int id { get; set; }
        public string url { get; set; }
        public string name { get; set; }
        public List<Size> sizes { get; set; }
        public DateTime added { get; set; }
    }

    public class FilesResponse
    {
        public int id { get; set; }
        public string name { get; set; }
        public int size { get; set; }
        public string url { get; set; }
        public string public_url { get; set; }
        public string download_url { get; set; }
        public string threejs_url { get; set; }
        public string thumbnail { get; set; }
        public DefaultImage default_image { get; set; }
        public string date { get; set; }
        public string formatted_size { get; set; }
        public List<object> meta_data { get; set; }
        public int download_count { get; set; }
    }


    public class FileDetail
    {
        public int id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public string public_url { get; set; }
        public string download_url { get; set; }
        public string threejs_url { get; set; }
        public string thumbnail { get; set; }
        public int downloads { get; set; }
        public DateTime added { get; set; }
        public string type { get; set; }
        public int size { get; set; }
        public string md5 { get; set; }
    }


    public class SearchResult
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("preview_image")]
        public string PreviewImage { get; set; }
    }

    public class Things
    {
        [JsonProperty("total")]
        public int TotalHits { get; set; }
        [JsonProperty("hits")]
        public List<SearchResult> Hits { get; set; }
    }
    
}
