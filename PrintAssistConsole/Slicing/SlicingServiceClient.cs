using Assets._ExtendedPrinter.Scripts.SlicingService;
using Newtonsoft.Json;
using SlicingCLI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PrintAssistConsole
{

    public class SlicingCompletedEventArgs: EventArgs
    {
        public string GcodeLink { get; private set; }
        public TimeSpan? PrintDuration { get; private set; }
        public float UsedFilament { get; private set; }
        public SlicingCompletedEventArgs(string link, TimeSpan printDuration, float usedFilament)
        {
            GcodeLink = link;
            PrintDuration = printDuration;
            UsedFilament = usedFilament;
        }
    }
    public class SlicingServiceClient
    {

        /// <summary>
        /// The Websocket Client
        /// </summary>
        private ClientWebSocket webSocket { get; set; }
        public List<string> AvailableProfiles { get; private set; }

        private string selectedSlicingConfigFile;

        /// <summary>
        /// Defines if the WebsocketClient is listening and the Tread is running
        /// </summary>
        private volatile bool listening;
        private CancellationToken cancellationToken;

        /// <summary>
        /// The size of the web socket buffer. Should work just fine, if the Websocket sends more, it will be split in 4096 Byte and reassembled in this class.
        /// </summary>
        private int WebSocketBufferSize = 4096;
        private string websocketURI;


        public event EventHandler<SlicingCompletedEventArgs> SlicingCompleted;

        public SlicingServiceClient(string websocketUri)
        {
            this.websocketURI = websocketUri;
        }


        private async Task StartWebsocketAsync()
        {
            if (!listening)
            {
                listening = true;
                await ConnectWebsocket();
                Task.Run(WebsocketDataReceiverHandler);
            }
        }

        private async Task ConnectWebsocket()
        {
            var canceltoken = CancellationToken.None;
            webSocket = new ClientWebSocket();
            try
            {
                await webSocket.ConnectAsync(new Uri(websocketURI),
                    canceltoken);
            }
            catch (Exception ex )
            {

                throw;
            }
        }


        /// <summary>
        /// Method to handle incomming data on the websocket
        /// </summary>
        /// <returns></returns>
        private async Task WebsocketDataReceiverHandler()
        {
            var buffer = new byte[8096];
            StringBuilder stringbuilder = new StringBuilder();
            while (!webSocket.CloseStatus.HasValue && listening)
            {
                WebSocketReceiveResult received = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string text = Encoding.UTF8.GetString(buffer, 0, received.Count);

                stringbuilder.Append(text);
                if (received.EndOfMessage)
                {
                    ParseWebsocketData(stringbuilder.ToString());
                    Console.WriteLine(stringbuilder.ToString());
                    stringbuilder.Clear();
                }
            }
            var cancelationToken = new CancellationToken();
            await webSocket.CloseAsync( WebSocketCloseStatus.NormalClosure, null, cancelationToken);
        }



        /// <summary>
        /// Starts a slicing process with the given commands
        /// </summary>
        /// <param name="prusaSlicerCLICommands"></param>
        public async Task MakeRequest(PrusaSlicerCLICommands prusaSlicerCLICommands)
        {
            await StartWebsocketAsync();

            prusaSlicerCLICommands.LoadConfigFile ??= "0.2Q.ini";

            var json = JsonConvert.SerializeObject(prusaSlicerCLICommands, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var tmp = Encoding.ASCII.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(tmp, 0, json.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }


        private void ParseWebsocketData(string data)
        {
            var _type = GetMessageType(data);
            if (_type == typeof(FileSlicedMessage))
            {
                try
                {
                    var slicingCompletedMessage = JsonConvert.DeserializeObject<FileSlicedMessage>(data);
                    var timespan = new TimeSpan(slicingCompletedMessage.Payload.Days, slicingCompletedMessage.Payload.Hours, slicingCompletedMessage.Payload.Minutes, 0);
                    if (SlicingCompleted != null)
                    {
                        SlicingCompleted(this, new SlicingCompletedEventArgs(slicingCompletedMessage.Payload.SlicedFilePath, timespan, slicingCompletedMessage.Payload.UsedFilament));
                    }

                }
                catch (Exception ex)
                {

                    throw;
                }
                listening = false;

                //if (SlicingCompleted != null)
                //{
                //    SlicingCompleted(this, new SlicingCompletedEventArgs(slicingCompletedMessage.Payload.SlicedFilePath, slicingCompletedMessage.Payload.PrintDuration, slicingCompletedMessage.Payload.UsedFilament));
                //}
            }
            else if (_type == typeof(ProfileListMessage))
            {
                AvailableProfiles = JsonConvert.DeserializeObject<ProfileListMessage>(data).Payload;
                selectedSlicingConfigFile = AvailableProfiles[AvailableProfiles.Count - 1];
            }
        }

        /// <summary>
        /// Gets the Messagetype in the json string
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private Type GetMessageType(string data)
        {
            var start = data.IndexOf(':') + 2;
            var end = data.IndexOf(',') - 1;
            var length = end - start;
            var type = data.Substring(start, length);

            return type switch
            {
                "SlicingCompleted" => typeof(FileSlicedMessage),
                "Profiles" => typeof(ProfileListMessage),
                _ => null,
            };
        }
    }
}
