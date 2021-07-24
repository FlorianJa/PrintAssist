using Newtonsoft.Json;
using PrintAssistConsole.Intents;
using SlicingCLI;
using Stateless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public enum SlicingProcessState : int
    {
        BeforeStart = -1,
        ModeSelection,
        ExpertModeLayerHeight,
        BeginnerModeQuality,
        ExpertModeInfill,
        ExpertModeSupport,
        ExpertModeAskToChangeOtherParameters,
        SlicingServiceStarted,
        ExpertModeAskForParameterName,
        EndSlicingWithoutPrinting,
        EndSlicingWithPrinting,
        BeginnerModeSurfaceSelection,
        BeginnerModeMechanicalForce,
        BeginnerModeOverhangs,
        BeginnerModeSummary,
        SlicingServiceCompleted,
        StartPrinting,
        DontPrintAfterSclicing,
        PrintAfterSclicing,
    }

    public class SlicingProcess
    {
        private enum Trigger
        {
            Next, Cancel,
            Start,
            StoreFile,
            SelectingMode,
            SelectingExpertMode,
            SelectingBeginnerMode,
            LayerHeightEntered,
            InfillEntered,
            ExpertModeSupportEntered,
            Yes,
            No
        }

        private readonly SlicingDialogDataProvider dialogData;
        private readonly ITelegramBotClient bot;
        private readonly long id;
        private readonly string modelPath;
        private readonly StateMachine<SlicingProcessState, Trigger> machine;
        private float layerHeight;
        private int fillDensity;
        private bool supportMaterial;
        private SlicingServiceClient slicingServiceClient;
        private string gcodeFile;

        public event EventHandler SlicingProcessCompletedWithoutStartPrint;
        public event EventHandler<string> SlicingProcessCompletedWithStartPrint;

        public SlicingProcess(long chatId, ITelegramBotClient bot, string modelPath)
        {
            dialogData = new SlicingDialogDataProvider();
            this.bot = bot;
            this.id = chatId;
            this.modelPath = modelPath;
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<SlicingProcessState, Trigger>(SlicingProcessState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(SlicingProcessState.BeforeStart)
                .Permit(Trigger.Start, SlicingProcessState.ModeSelection);

            //machine.Configure(SlicingProcessState.Start)
            //    .OnEntryAsync(async () => await SendMessageAsync(machine.State)) 
            //    .Permit(Trigger.No, SlicingProcessState.StoringFile)
            //    .Permit(Trigger.Yes, SlicingProcessState.ModeSelection);

            //machine.Configure(SlicingProcessState.StoringFile);
            ////.OnEntryAsync(async () => ) 

            machine.Configure(SlicingProcessState.ModeSelection)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.SelectingExpertMode, SlicingProcessState.ExpertModeLayerHeight)
                .Permit(Trigger.SelectingBeginnerMode, SlicingProcessState.BeginnerModeQuality);

            machine.Configure(SlicingProcessState.ExpertModeLayerHeight)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .OnExit(async () => await SendMessageAsync($"Layer height = {layerHeight:F2} mm"))
                .Permit(Trigger.Next, SlicingProcessState.ExpertModeInfill);

            machine.Configure(SlicingProcessState.ExpertModeInfill)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .OnExitAsync(async () => await SendMessageAsync($"Infill = {fillDensity}%"))
                .Permit(Trigger.Next, SlicingProcessState.ExpertModeSupport);

            machine.Configure(SlicingProcessState.ExpertModeSupport)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .OnExitAsync(async () => await SendMessageAsync($"Support = {supportMaterial}"))
                .Permit(Trigger.Next, SlicingProcessState.SlicingServiceStarted);

            machine.Configure(SlicingProcessState.SlicingServiceStarted)
                .OnEntryAsync(async () => { await SendMessageAsync(machine.State); await CallSlicingService(); })
                .Permit(Trigger.Next, SlicingProcessState.SlicingServiceCompleted);
            //.Permit(Trigger.Yes, SlicingProcessState.EndSlicingWithPrinting);

            machine.Configure(SlicingProcessState.SlicingServiceCompleted)
               .OnEntryAsync(async () => { await SendMessageAsync(machine.State); })
               .Permit(Trigger.No, SlicingProcessState.DontPrintAfterSclicing)
               .Permit(Trigger.Yes, SlicingProcessState.StartPrinting);

            machine.Configure(SlicingProcessState.DontPrintAfterSclicing)
               .OnEntryAsync(async () => { await SendMessageAsync(machine.State); SlicingProcessCompletedWithoutStartPrint?.Invoke(this, null); });

            machine.Configure(SlicingProcessState.StartPrinting)
               .OnEntry(() => SlicingProcessCompletedWithStartPrint?.Invoke(this, gcodeFile))
               .Permit(Trigger.No, SlicingProcessState.DontPrintAfterSclicing)
               .Permit(Trigger.Yes, SlicingProcessState.PrintAfterSclicing);

            machine.Configure(SlicingProcessState.BeginnerModeQuality)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeSurfaceSelection);

            machine.Configure(SlicingProcessState.BeginnerModeSurfaceSelection)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeMechanicalForce);

            machine.Configure(SlicingProcessState.BeginnerModeMechanicalForce)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeOverhangs);

            machine.Configure(SlicingProcessState.BeginnerModeOverhangs)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeSummary);

            machine.Configure(SlicingProcessState.BeginnerModeSummary)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.SlicingServiceStarted);

            #endregion
        }

        private async Task CallSlicingService()
        {
            slicingServiceClient = new SlicingServiceClient("ws://localhost:5003/ws");
            slicingServiceClient.SlicingCompleted += SlicingServiceClient_SlicingCompleted;
            var tmp = PrusaSlicerCLICommands.Default;
            tmp.FileURI = modelPath;
            tmp.LayerHeight = layerHeight;
            tmp.SupportMaterial = supportMaterial;
            tmp.FillDensity = fillDensity/100f;
            await slicingServiceClient.MakeRequest(tmp);
        }

        private async void SlicingServiceClient_SlicingCompleted(object sender, string gcodeFileLink)
        {
            gcodeFile = gcodeFileLink;
            await machine.FireAsync(Trigger.Next);
        }

        public async Task StartAsync()
        {
            await machine.FireAsync(Trigger.Start);
        }
        private async Task SendMessageAsync(SlicingProcessState state)
        {
            var message = dialogData.GetMessage((int)state);
            await SendMessageAsync(message);
        }

        private async Task SendMessageAsync(Message message)
        {
            #region send photo(s)
            if (message.PhotoFilePaths != null)
            {
                if (message.PhotoFilePaths.Count == 1)
                {
                    if (System.IO.File.Exists(message.PhotoFilePaths[0]))
                    {
                        await bot.SendChatActionAsync(id, ChatAction.UploadPhoto);
                        using FileStream fileStream = new(message.PhotoFilePaths[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                        var fileName = message.PhotoFilePaths[0].Split(Path.DirectorySeparatorChar).Last();
                        await bot.SendPhotoAsync(chatId: id, photo: new InputOnlineFile(fileStream, fileName));
                    }
                }
                else if (message.PhotoFilePaths.Count > 1)
                {
                    await bot.SendChatActionAsync(id, ChatAction.UploadPhoto);

                    var album = new List<IAlbumInputMedia>();
                    var tmp = new List<FileStream>();
                    foreach (var path in message.PhotoFilePaths)
                    {
                        if (System.IO.File.Exists(path))
                        {

                            FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            tmp.Add(fileStream);
                            var fileName = path.Split(Path.DirectorySeparatorChar).Last();

                            album.Add(new InputMediaPhoto(new InputMedia(fileStream, fileName)));
                        }
                    }

                    await bot.SendMediaGroupAsync(chatId: id, inputMedia: album);

                    foreach (var stream in tmp)
                    {
                        stream.Dispose();
                    }
                    tmp.Clear();
                }
            }
            #endregion

            #region send video(s)
            if (message.VideoFilePaths != null)
            {
                if (message.VideoFilePaths.Count == 1)
                {

                    if (System.IO.File.Exists(message.VideoFilePaths[0]))
                    {
                        await bot.SendChatActionAsync(id, ChatAction.UploadVideo);
                        using FileStream fileStream = new(message.VideoFilePaths[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                        var fileName = message.VideoFilePaths[0].Split(Path.DirectorySeparatorChar).Last();
                        await bot.SendVideoAsync(chatId: id, video: new InputOnlineFile(fileStream, fileName));
                    }
                }
                else if (message.VideoFilePaths.Count > 1)
                {
                    await bot.SendChatActionAsync(id, ChatAction.UploadVideo);

                    var album = new List<IAlbumInputMedia>();
                    foreach (var path in message.VideoFilePaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            using FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var fileName = path.Split(Path.DirectorySeparatorChar).Last();

                            album.Add(new InputMediaPhoto(new InputMedia(fileStream, fileName)));
                        }
                    }

                    await bot.SendMediaGroupAsync(chatId: id, inputMedia: album);
                }
            }
            #endregion

            #region send text
            if (message.Text != null)
            {
                await bot.SendChatActionAsync(id, ChatAction.Typing);
                await bot.SendTextMessageAsync(chatId: id,
                            text: message.Text,
                            replyMarkup: message.ReplyKeyboardMarkup);
            }
            #endregion
        }

        public async Task HandleInputAsync(Update update)
        {
            switch (machine.State)
            {
                case SlicingProcessState.BeforeStart:
                    break;
                case SlicingProcessState.ModeSelection:
                    {
                        switch (update.Message.Text.ToLower())
                        {
                            case "expert":
                                {
                                    await machine.FireAsync(Trigger.SelectingExpertMode);
                                    break;
                                }
                            case "beginner":
                                {
                                    await machine.FireAsync(Trigger.SelectingBeginnerMode);
                                    break;
                                }
                            default:
                                {
                                    await SendMessageAsync("Bitte wähe einen Slicingmode aus [Expert/Beginner] oder sag abbrechen, wenn du den Vorgang abbrechen möchtest.", CustomKeyboards.ExpertBeginnerKeyboard);
                                    break;
                                }
                        }
                        break;
                    }
                case SlicingProcessState.ExpertModeLayerHeight:
                    {
                        Regex rx = new Regex(@"\d*[,.]\d+", RegexOptions.Compiled);

                        var match = rx.Match(update.Message.Text);

                        if(match.Success)
                        {
                            layerHeight = float.Parse(match.Value);
                            if(layerHeight >= 0.05f && layerHeight <= 0.3f)
                            {
                                await machine.FireAsync(Trigger.Next);
                            }
                            else
                            {
                                await SendMessageAsync("Deine Eingabe nicht gültig. Die Layer Height muss zwischen 0,05 mm und 0.3 mm betragen.", CustomKeyboards.LayerHeightKeyboard);
                            }
                        }
                        else
                        {
                            await SendMessageAsync("Bitte wähle eine Layer Height aus der Auswahl unten aus oder gibt die Zahl in Millimeter ein.", CustomKeyboards.LayerHeightKeyboard);
                        }

                        break;
                    }
                case SlicingProcessState.BeginnerModeQuality:
                    break;
                case SlicingProcessState.ExpertModeInfill:
                    {
                        Regex rx = new Regex(@"\d*", RegexOptions.Compiled);

                        var match = rx.Match(update.Message.Text);

                        if (match.Success)
                        {
                            fillDensity = Int32.Parse(match.Value);

                            if (fillDensity >= 0 && fillDensity <= 100)
                            {
                                await machine.FireAsync(Trigger.Next);
                            }
                            else
                            {
                                await SendMessageAsync("Deine Eingabe nicht gültig. Das Infill muss zwischen 0% und 100% betragen.", CustomKeyboards.InfillKeyboard);
                            }
                        }
                        else
                        {
                            await SendMessageAsync("Bitte wähle eine Füllmenge aus der Auswahl unten aus oder gibt die Zahl in Prozent ein.");
                        }
                        break;
                    }
                case SlicingProcessState.ExpertModeSupport:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    supportMaterial = true;
                                    break;
                                }
                            case TutorialNo:
                                {
                                    supportMaterial = false;
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);

                        break;
                    }
                case SlicingProcessState.ExpertModeAskToChangeOtherParameters:
                    break;
                case SlicingProcessState.SlicingServiceStarted:
                    break;
                case SlicingProcessState.ExpertModeAskForParameterName:
                    break;
                case SlicingProcessState.EndSlicingWithoutPrinting:
                    break;
                case SlicingProcessState.EndSlicingWithPrinting:
                    break;
                case SlicingProcessState.BeginnerModeSurfaceSelection:
                    break;
                case SlicingProcessState.BeginnerModeMechanicalForce:
                    break;
                case SlicingProcessState.BeginnerModeOverhangs:
                    break;
                case SlicingProcessState.BeginnerModeSummary:
                    break;
                case SlicingProcessState.SlicingServiceCompleted:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    await machine.FireAsync(Trigger.Yes);
                                    break;
                                }
                            case TutorialNo:
                                {
                                    await machine.FireAsync(Trigger.No);
                                    break;
                                }
                            default:
                                break;
                        }
                        
                        break;
                    }
                default:
                    break;
            }
        }

        private async Task SendMessageAsync(string text, IReplyMarkup keyboardMarkup = null)
        {
            keyboardMarkup ??= new ReplyKeyboardRemove();

            await bot.SendChatActionAsync(id, ChatAction.Typing);
            await bot.SendTextMessageAsync(chatId: id,
                        text: text,
                        replyMarkup: keyboardMarkup); 
            
        }
    }


    public class SlicingDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<SlicingProcessState, Message> messages { get; set; }

        public SlicingDialogDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(@".\BotContent\SlicingProcess.json"))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    messages = serializer.Deserialize<Dictionary<SlicingProcessState, Message>>(jsonReader);
                }
            }
        }

        public Message GetMessage(int state)
        {
            return messages[(SlicingProcessState)state];
        }

        public int GetMessageCount()
        {
            return messages.Count;
        }
    }
}
