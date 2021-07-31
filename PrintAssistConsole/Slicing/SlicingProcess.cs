using Humanizer;
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
        GuidedModePrototype,
        ExpertModeInfill,
        ExpertModeSupport,
        ExpertModeAskToChangeOtherParameters,
        SlicingServiceStarted,
        ExpertModeAskForParameterName,
        EndSlicingWithoutPrinting,
        EndSlicingWithPrinting,
        GuidedModeSurfaceSelection,
        GuidedModeMechanicalForce,
        GuidedModeOverhangs,
        guidedModeSummary,
        SlicingServiceCompleted,
        StartPrinting,
        DontPrintAfterSclicing,
        PrintAfterSclicing,
        GuidedModeDetails,
        SelectingPreset,
        AdvancedSupport,
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
            SelectingGuidedMode,
            LayerHeightEntered,
            InfillEntered,
            ExpertModeSupportEntered,
            Yes,
            No,
            SelectingPresets,
            PresetSelcted,
            AdvancedSupportEntered
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
        private PrusaSlicerCLICommands cliCommands = null;
        private string modelName;
        private int counter;
        private int progessMessageId;
        private long fromId;
        private Timer Timer;
        private List<string> Profiles = new List<string>()
        {
            "0.07mm ULTRADETAIL", "0.10mm DETAIL",
            "0.15mm QUALITY", "0.15mm SPEED",
            "0.20mm QUALITY", "0.20mm SPEED",
            "0.25mm DRAFT"
        };
        private string selectedProfileFile = null;

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
                .Permit(Trigger.SelectingPresets, SlicingProcessState.SelectingPreset)
                .Permit(Trigger.SelectingExpertMode, SlicingProcessState.ExpertModeLayerHeight)
                .Permit(Trigger.SelectingGuidedMode, SlicingProcessState.GuidedModePrototype);

            machine.Configure(SlicingProcessState.SelectingPreset)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .InternalTransitionAsync(Trigger.SelectingPresets, async () => { await SendMessageAsync(machine.State); })
                .Permit(Trigger.PresetSelcted, SlicingProcessState.AdvancedSupport)
                ;

            machine.Configure(SlicingProcessState.AdvancedSupport)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.AdvancedSupportEntered, SlicingProcessState.SlicingServiceStarted);

            machine.Configure(SlicingProcessState.ExpertModeLayerHeight)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .OnExit(async () => await SendMessageAsync($"Schichthöhe = {layerHeight:F2} mm"))
                .Permit(Trigger.Next, SlicingProcessState.ExpertModeInfill);

            machine.Configure(SlicingProcessState.ExpertModeInfill)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .OnExitAsync(async () => await SendMessageAsync($"Füllgrad = {fillDensity}%"))
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

            machine.Configure(SlicingProcessState.GuidedModePrototype)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.No, SlicingProcessState.GuidedModeSurfaceSelection)
                .Permit(Trigger.Yes, SlicingProcessState.GuidedModeOverhangs);

            machine.Configure(SlicingProcessState.GuidedModeSurfaceSelection)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, SlicingProcessState.GuidedModeMechanicalForce);

            machine.Configure(SlicingProcessState.GuidedModeMechanicalForce)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, SlicingProcessState.GuidedModeOverhangs);

            machine.Configure(SlicingProcessState.GuidedModeOverhangs)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .PermitIf(Trigger.Next, SlicingProcessState.GuidedModeDetails, () => { return isPrototype == false; })
                .PermitIf(Trigger.Next, SlicingProcessState.guidedModeSummary, () => { return isPrototype == true; });

            machine.Configure(SlicingProcessState.GuidedModeDetails)
               .OnEntryAsync(async () => await SendMessageAsync(machine.State))
               .Permit(Trigger.Next, SlicingProcessState.guidedModeSummary);

            machine.Configure(SlicingProcessState.guidedModeSummary)
                .OnEntryAsync(async () => { CalculateSlicingParameters(); await SendSlicingParameterSummaryMessageAsync(); })
                .Permit(Trigger.Next, SlicingProcessState.SlicingServiceStarted);

            #endregion
        }

        private void CalculateSlicingParameters()
        {
            cliCommands = PrusaSlicerCLICommands.Default;

            if (isPrototype)
            {
                cliCommands.LoadConfigFile = "0.25D.ini";
                cliCommands.SupportMaterial = objectHasOverhangs;
                cliCommands.SupportMaterialBuildeplateOnly = objectHasOverhangs;
                cliCommands.FillDensity = 0.2f;
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
            string message = "Übersicht zu den wichtigsten Parametern:" + Environment.NewLine;

            if(isPrototype)
            {
                message += "Schichthöhe = 0.3mm" + Environment.NewLine;
                message += $"Füllgrad = {cliCommands.FillDensity}" + Environment.NewLine;
                message += "Supportstrukturen sind ";
                message += objectHasOverhangs ? "aktiviert.": "deaktiviert.";
            }
            else
            {
                message += $"Schichthöhe = {cliCommands.LayerHeight}" + Environment.NewLine;
                message += $"Füllgrad = {cliCommands.FillDensity}" + Environment.NewLine;
                message += $"Supportstrukturen sind = ";
                message += (bool)cliCommands.SupportMaterial ? " aktiviert." : " deaktiviert.";
            }
            await SendMessageAsync(message);
            await machine.FireAsync(Trigger.Next);
        }

        private async Task CallSlicingService()
        {
            slicingServiceClient = new SlicingServiceClient("ws://localhost:5003/ws");
            slicingServiceClient.SlicingCompleted += SlicingServiceClient_SlicingCompleted;

            if (cliCommands == null)
            {
                if (selectedProfileFile != null)
                {
                    cliCommands = PrusaSlicerCLICommands.Default;
                    cliCommands.SupportMaterial = supportMaterial;
                    cliCommands.LoadConfigFile = selectedProfileFile;
                }
                else
                {
                    var tmp = PrusaSlicerCLICommands.Default;

                    tmp.LayerHeight = layerHeight;
                    tmp.SupportMaterial = supportMaterial;
                    tmp.FillDensity = fillDensity / 100f;
                    cliCommands = tmp;
                }
            }

            cliCommands.FileURI = modelPath;
            cliCommands.FileName = modelName;
            ////var message = await SendMessageAsync("Slicing...");


            var message = await bot.SendTextMessageAsync(id, "Slicing läuft");
            progessMessageId = message.MessageId;
            fromId = message.Chat.Id;


            Timer = new Timer();
            Timer.Interval = 500;
            Timer.Elapsed += Timer_Elapsed1;
            Timer.Start();
            await slicingServiceClient.MakeRequest(cliCommands);
        }

        private async void Timer_Elapsed1(object sender, ElapsedEventArgs e)
        {
            counter++;
            var newText = "Slicing läuft" + String.Concat(Enumerable.Repeat(".", (counter % 3) + 1));
            await bot.EditMessageTextAsync(id, progessMessageId, newText);
        }
        
        private async void SlicingServiceClient_SlicingCompleted(object sender, SlicingCompletedEventArgs args)
        {
            Timer.Stop();
            gcodeFile = args.GcodeLink;
            var message = "Slicing abgeschlossen." + Environment.NewLine;
            message += $"Druckdauer = {args.PrintDuration.Value.Humanize(2)}";
            message += Environment.NewLine;
            message += $"Benötigtes Filament = {args.UsedFilament:F2}m";

            var photos = new List<string>() { ".\\BotContent\\images\\Placeholder.png" };
            Message tmp = new Message(text: message,photos);

            await SendMessageAsync(tmp);
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
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "slicing");
                        switch (intent)
                        {
                            case SlicingModeSelectionExpert:
                                {
                                    await machine.FireAsync(Trigger.SelectingExpertMode);
                                    break;
                                }
                            case SlicingModeSelectionAdvanced:
                                {
                                    await machine.FireAsync(Trigger.SelectingPresets);
                                    break;
                                }
                            case SlicingModeSelectionGuided:
                            {
                                    await machine.FireAsync(Trigger.SelectingGuidedMode);
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
                case SlicingProcessState.SelectingPreset:
                    {
                        var profil = update.Message.Text;

                        if(Profiles.Any((x) => x == profil))
                        {
                            selectedProfileFile = profil + ".ini";
                            await machine.FireAsync(Trigger.PresetSelcted);
                        }
                        else
                        {
                            await SendMessageAsync("Ungültiges Profil.");
                            await machine.FireAsync(Trigger.SelectingPresets);
                        }

                        break;
                    }
                case SlicingProcessState.AdvancedSupport:
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
                        await machine.FireAsync(Trigger.AdvancedSupportEntered);
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
                case SlicingProcessState.GuidedModePrototype:
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
                case SlicingProcessState.GuidedModeSurfaceSelection:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    smoothSurfaceNeeded = true;
                                    await SendMessageAsync("Für eine glatte Oberfläche wähle ich eine geringe Schichthöhe von 0,1 mm.");
                                    break;
                                }
                            case TutorialNo:
                                {
                                    smoothSurfaceNeeded = false;
                                    await SendMessageAsync("Okay, dann reicht eine höhere Schichthöhe von 0,25 mm");
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);

                        break;
                    }
                case SlicingProcessState.GuidedModeMechanicalForce:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectNeedsToHandleMechanicalForces = true;
                                    await SendMessageAsync("Mehr Infill wird die Stabilität des Objets erhöhen.");
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectNeedsToHandleMechanicalForces = false;
                                    await SendMessageAsync("Okay, dann reicht ein geringerer Füllgrad von 15%");
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
                case SlicingProcessState.GuidedModeOverhangs:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectHasOverhangs = true;
                                    await SendMessageAsync("Bei Objekten mit Überhängen sollten Supportstrukturen aktiviert werden.");
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectHasOverhangs = false;
                                    await SendMessageAsync("Bei Objekten ohne Überhängen können Supportstrukturen deaktiviert werden.");
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
                case SlicingProcessState.GuidedModeDetails:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    objectHasFineDetails = true;
                                    await SendMessageAsync("Für feine Details veringere ich die Druckgeschwindigkeit etwas.");
                                    break;
                                }
                            case TutorialNo:
                                {
                                    objectHasFineDetails = false;
                                    await SendMessageAsync("Okay, dann können wir mit normaler Druckgegeschwindigkeit drucken.");
                                    break;
                                }
                            default:
                                break;
                        }
                        await machine.FireAsync(Trigger.Next);
                        break;
                    }
                case SlicingProcessState.guidedModeSummary:
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
