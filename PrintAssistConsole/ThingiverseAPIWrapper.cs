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
                    myDeserializedThings = JsonConvert.DeserializeObject<Things>(things);
                    //foreach (var thingDetail in myDeserializedThings.Hits)
                    //{
                    //    ret.Add(thingDetail.PreviewImage);
                    //    // display the returned search results of things details (Id, name, PreviewImage)
                    //    //Console.WriteLine("Thing Id: {0}, Name: {1},  Preview image link: {2}", thingDetail.Id, thingDetail.Name, thingDetail.PreviewImage);
                    //}
                }
                else
                {
                    Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                }
                Console.WriteLine();
            }
            return myDeserializedThings;
        }
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

    public class File
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("public_url")]
        public string PublicUrl { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

    }
    public class Size
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("size")]
        public string size { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class Image
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("sizes")]
        public List<Size> Sizes { get; set; }
    }
}
