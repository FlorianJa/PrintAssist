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
using System.Timers;
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
        BeginnerModePrototype,
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
        BeginnerModeDetails,
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
        private bool isPrototype;
        private bool smoothSurfaceNeeded;
        private bool objectHasOverhangs;
        private bool objectHasFineDetails;
        private bool objectNeedsToHandleMechanicalForces;
        private string slicingProfile;
        private PrusaSlicerCLICommands cliCommands;
        private string modelName;
        private int counter;
        private int progessMessageId;
        private long fromId;
        private Timer Timer;

        public event EventHandler SlicingProcessCompletedWithoutStartPrint;
        public event EventHandler<string> SlicingProcessCompletedWithStartPrint;

        public SlicingProcess(long chatId, ITelegramBotClient bot, string modelPath, string modelName)
        {
            dialogData = new SlicingDialogDataProvider();
            this.bot = bot;
            this.id = chatId;
            this.modelPath = modelPath;
            this.modelName = modelName;

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
                .Permit(Trigger.SelectingBeginnerMode, SlicingProcessState.BeginnerModePrototype);

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
               .OnEntryAsync(async () => await SendMessageAsync(machine.State))
               .Permit(Trigger.No, SlicingProcessState.DontPrintAfterSclicing)
               .Permit(Trigger.Yes, SlicingProcessState.StartPrinting);

            machine.Configure(SlicingProcessState.DontPrintAfterSclicing)
               .OnEntryAsync(async () => { await SendMessageAsync(machine.State); SlicingProcessCompletedWithoutStartPrint?.Invoke(this, null); });

            machine.Configure(SlicingProcessState.StartPrinting)
               .OnEntry(() => SlicingProcessCompletedWithStartPrint?.Invoke(this, gcodeFile))
               .Permit(Trigger.No, SlicingProcessState.DontPrintAfterSclicing)
               .Permit(Trigger.Yes, SlicingProcessState.PrintAfterSclicing);

            machine.Configure(SlicingProcessState.BeginnerModePrototype)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.No, SlicingProcessState.BeginnerModeSurfaceSelection)
                .Permit(Trigger.Yes, SlicingProcessState.BeginnerModeOverhangs);

            machine.Configure(SlicingProcessState.BeginnerModeSurfaceSelection)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeMechanicalForce);

            machine.Configure(SlicingProcessState.BeginnerModeMechanicalForce)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeOverhangs);

            machine.Configure(SlicingProcessState.BeginnerModeOverhangs)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .PermitIf(Trigger.Next, SlicingProcessState.BeginnerModeDetails, () => { return isPrototype == false; })
                .PermitIf(Trigger.Next, SlicingProcessState.BeginnerModeSummary, () => { return isPrototype == true; });

            machine.Configure(SlicingProcessState.BeginnerModeDetails)
               .OnEntryAsync(async () => await SendMessageAsync(machine.State))
               .Permit(Trigger.Next, SlicingProcessState.BeginnerModeSummary);

            machine.Configure(SlicingProcessState.BeginnerModeSummary)
                .OnEntryAsync(async () => { CalculateSlicingParameters(); await SendSlicingParameterSummaryMessageAsync(); })
                .Permit(Trigger.Next, SlicingProcessState.SlicingServiceStarted);

            #endregion
        }

        private void CalculateSlicingParameters()
        {
            cliCommands = new PrusaSlicerCLICommands();

            if (isPrototype)
            {
                cliCommands.LoadConfigFile = "0.25D.ini";
                cliCommands.SupportMaterial = objectHasOverhangs;
                cliCommands.SupportMaterialBuildeplateOnly = objectHasOverhangs;
            }
            else
            {

                if (smoothSurfaceNeeded)
                {
                    cliCommands.LayerHeight = 0.1f;
                }
                else
                {
                    cliCommands.LayerHeight = 0.15f;
                }

                if (objectNeedsToHandleMechanicalForces)
                {
                    cliCommands.FillDensity = 1;
                }
                else
                {
                    cliCommands.FillDensity = 0.2f;
                }

                if (objectHasOverhangs)
                {
                    cliCommands.SupportMaterial = objectHasOverhangs;
                    cliCommands.SupportMaterialBuildeplateOnly = objectHasOverhangs;
                }

                if(objectHasFineDetails)
                {
                    cliCommands.LoadConfigFile = "0.1D.ini";
                    cliCommands.LayerHeight = 0.1f; // fine details overwrite smoothsurface
                }
                else
                {
                    cliCommands.LoadConfigFile = "0.15S.ini";
                }
            }
        }

        private async Task SendSlicingParameterSummaryMessageAsync()
        {
            string message = "Parameter Summary:" + Environment.NewLine;

            if(isPrototype)
            {
                message = "Layer height = 0.3mm" + Environment.NewLine;
                message += $"Infill density = {cliCommands.FillDensity}" + Environment.NewLine;
                message += "Support is";
                message += objectHasOverhangs ? "enabled.": "disabled.";
            }
            else
            {
                message = $"Layer height = {cliCommands.LayerHeight}" + Environment.NewLine;
                message += $"Infill density = {cliCommands.FillDensity}" + Environment.NewLine;
                message += $"Support is = ";
                message += (bool)cliCommands.SupportMaterial ? "enabled." : "disabled.";
            }
            await SendMessageAsync(message);
        }

        private async Task CallSlicingService()
        {
            slicingServiceClient = new SlicingServiceClient("ws://localhost:5003/ws");
            slicingServiceClient.SlicingCompleted += SlicingServiceClient_SlicingCompleted;
            var tmp = PrusaSlicerCLICommands.Default;
            tmp.FileURI = modelPath;
            tmp.FileName = modelName;
            tmp.LayerHeight = layerHeight;
            tmp.SupportMaterial = supportMaterial;
            tmp.FillDensity = fillDensity/100f;

            ////var message = await SendMessageAsync("Slicing...");
            

            var message = await bot.SendTextMessageAsync(id, "Slicing");
            progessMessageId = message.MessageId;
            fromId = message.Chat.Id;


            Timer = new Timer();
            Timer.Interval = 500;
            Timer.Elapsed += Timer_Elapsed1;
            Timer.Start();
            await slicingServiceClient.MakeRequest(tmp);
        }

        private async void Timer_Elapsed1(object sender, ElapsedEventArgs e)
        {
            counter++;
            var newText = "Slicing" + String.Concat(Enumerable.Repeat(".", (counter % 3) + 1));
            await bot.EditMessageTextAsync(id, progessMessageId, newText);
        }
        
        private async void SlicingServiceClient_SlicingCompleted(object sender, SlicingCompletedEventArgs args)
        {
            Timer.Stop();
            gcodeFile = args.GcodeLink;
            var message = "Slicing completed. " + Environment.NewLine;
            message += $"Print duration = {args.PrintDuration}";
            message += Environment.NewLine;
            message += $"Used filament = {args.UsedFilament:F2}m";

            await SendMessageAsync(message);
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
                case SlicingProcessState.BeginnerModePrototype:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    isPrototype = true;
                                    await machine.FireAsync(Trigger.Yes);
                                    break;
                                }
                            case TutorialNo:
                                {
                                    isPrototype = false;
                                    await machine.FireAsync(Trigger.No);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
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
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    smoothSurfaceNeeded = true;
                                    break;
                                }
                            case TutorialNo:
                                {
                                    smoothSurfaceNeeded = false;
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);

                        break;
                    }
                case SlicingProcessState.BeginnerModeMechanicalForce:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectNeedsToHandleMechanicalForces = true;
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectNeedsToHandleMechanicalForces = false;
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
                case SlicingProcessState.BeginnerModeOverhangs:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectHasOverhangs = true;
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectHasOverhangs = false;
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
                case SlicingProcessState.BeginnerModeDetails:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectHasFineDetails = true;
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectHasFineDetails = false;
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
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

        private async Task<Telegram.Bot.Types.Message> SendMessageAsync(string text, IReplyMarkup keyboardMarkup = null)
        {
            keyboardMarkup ??= new ReplyKeyboardRemove();

            await bot.SendChatActionAsync(id, ChatAction.Typing);
             var message = await bot.SendTextMessageAsync(chatId: id,
                        text: text,
                        replyMarkup: keyboardMarkup);

            return message;

        }
    }


    public class SlicingDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<SlicingProcessState, Message> messages { get; set; }

        public SlicingDialogDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(@".\BotContent\SlicingProcess_de.json"))
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
