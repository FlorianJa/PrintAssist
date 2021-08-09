using Humanizer;
using OctoPrintConnector;
using PrintAssistConsole.Intents;
using Stateless;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public class Conversation
    {
        private enum Trigger { NameEntered,
            ConversationStarted,
            StartHardwareTutorial,
            Starting,
            HardwareTutorialFinished,
            StartWorkflowTutorial,
            WorkTutorialFinished,
            HardwareTutorialCanceled,
            WorkTutorialCanceled,
            STLFileReceived,
            StartSlicing,
            Cancel,
            SlicingCompletedWithoutPrintStart,
            SlicingCompletedWithPrintStart,
            PrintStarted,
            SearchCompleted,
            SliceLater,
            SearchModel,
            StartPrint,
            SearchAborted,
            GcodeFileReceived
        }

        public Int64 Id { get; private set; }
        public string UserName { get; internal set; }
        private Tutorial tutorial;
        private SlicingProcess slicingProcess;
        private string selectedModelUrl;
        private StartPrintProcess startPrintProcess;
        private SearchModelDialog searchModelProcess;
        private string modelName;
        private string printObject = null;
        private List<string> contexts;
        private CollectPrintInformationDialog collectingDataForPrintingDialog;
        private string lastGcodeFile;
        private bool printStarted = false;
        private OctoprintServer octoprinServer;
        private bool firstTemperatureReceivedMessage = true;
        private string lastTemperaturMessageString;
        private int temperatureMessageId;
        private int temperaturToReach;
        private bool calibrationFinished;
        private bool updateTemperatureMessage;
        private bool temperatureReached;
        private readonly StateMachine<ConversationState, Trigger> machine;
        protected readonly ITelegramBotClient bot;


        ResourceManager resourceManager;    
        CultureInfo currentCulture;

        public ConversationState CurrentState
        { 
            get
            {
                return machine.State;
            }
        }

        public Conversation(Int64 id, ITelegramBotClient bot, string culture = "de-DE")
        {
            Id = id;
            this.bot = bot;

            currentCulture = CultureInfo.CreateSpecificCulture(culture);
            resourceManager = new ResourceManager("PrintAssistConsole.Properties.text", Assembly.GetExecutingAssembly());

            // Instantiate a new state machine in the Start state
            machine = new StateMachine<ConversationState, Trigger>(ConversationState.Connected);
            SetUpStateMachine();
        }

        public async Task StartAsync()
        {
            //await machine.FireAsync(Trigger.Starting);
        }

        private void SetUpStateMachine()
        {
            machine.Configure(ConversationState.Connected)
                .Permit(Trigger.Starting, ConversationState.ConversationStarting);

            machine.Configure(ConversationState.ConversationStarting)
                .OnEntryAsync(async () => { await SendWelcomeMessageAsync(); await machine.FireAsync(Trigger.ConversationStarted); })
                .Permit(Trigger.ConversationStarted, ConversationState.Idle);

            machine.Configure(ConversationState.EnteringNamen)
                .OnExitAsync(async () => await SendGreetingWithNameAsync())
                .Permit(Trigger.NameEntered, ConversationState.Idle);
                //.Permit(Trigger.NameEntered, ConversationState.Idle);

            machine.Configure(ConversationState.Idle)
                //.OnEntryAsync(async () => await SendMessageAsync("What can i do for you?"))
                .OnEntryAsync(async () => await SendMessageAsync(resourceManager.GetString("StartIdleMessage", currentCulture)))
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.StartHardwareTutorial, ConversationState.HardwareTutorial)
                .Permit(Trigger.STLFileReceived, ConversationState.STLFileReceived)
                .Permit(Trigger.GcodeFileReceived, ConversationState.GcodeFileReceived)
                .Permit(Trigger.SearchModel, ConversationState.SearchModel)
                .Permit(Trigger.StartPrint, ConversationState.CollectDataForPrint);

            machine.Configure(ConversationState.CollectDataForPrint)
                .OnEntryAsync(async () => await StartCollectingDataForPrintingAsync())
                .Permit(Trigger.StartSlicing, ConversationState.Slicing)
                .Permit(Trigger.SearchModel, ConversationState.SearchModel)
                .Permit(Trigger.StartPrint, ConversationState.CheckBeforePrint);

            machine.Configure(ConversationState.HardwareTutorial)
                .OnEntryAsync(async () => await StartHardwareTutorialAsync())
                .Permit(Trigger.HardwareTutorialFinished, ConversationState.HardwareTutorialFinished)
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.HardwareTutorialCanceled, ConversationState.Idle);

            machine.Configure(ConversationState.HardwareTutorialFinished)
               //.OnEntryAsync(async () => await StartHardwareTutorialAsync())
               //.Permit(Trigger.HardwareTutorialFinished, ConversationState.HardwareTutorialFinished)
               .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
               .Permit(Trigger.HardwareTutorialFinished, ConversationState.Idle);

            machine.Configure(ConversationState.WorkflowTutorial)
                .OnEntryAsync(async () => await StartWorkflowTutorialAsync())
                .Permit(Trigger.WorkTutorialCanceled, ConversationState.Idle)
                .Permit(Trigger.WorkTutorialFinished, ConversationState.Idle);

            machine.Configure(ConversationState.GcodeFileReceived)
                .OnEntryAsync(async () => await AskForPrintNowAsync())
                .Permit(Trigger.Cancel, ConversationState.Idle)
                .Permit(Trigger.StartPrint, ConversationState.CheckBeforePrint);

            machine.Configure(ConversationState.STLFileReceived)
                .OnEntryAsync(async () => await AskForSlicingNowAsync())
                .Permit(Trigger.Cancel, ConversationState.Idle)
                .Permit(Trigger.StartSlicing, ConversationState.Slicing);

            machine.Configure(ConversationState.Slicing)
                .OnEntryAsync(async () => await StartSlicingAsync())
                .Permit(Trigger.SlicingCompletedWithoutPrintStart, ConversationState.Idle)
                .Permit(Trigger.SlicingCompletedWithPrintStart, ConversationState.CheckBeforePrint)
                .Permit(Trigger.Cancel, ConversationState.Idle);

            machine.Configure(ConversationState.CheckBeforePrint)
               .OnEntryAsync(async () => await CheckbeforePrintDialog())
               .Permit(Trigger.PrintStarted, ConversationState.Printing);

            machine.Configure(ConversationState.SearchModel)
               .OnEntryAsync(async () => await StartSearchModelProcessAsync())
               .Permit(Trigger.SearchCompleted, ConversationState.AskToSliceSelectedFile)
               .Permit(Trigger.SearchAborted, ConversationState.Idle);

            machine.Configure(ConversationState.AskToSliceSelectedFile)
               .OnEntryAsync(async () => await SendMessageAsync(resourceManager.GetString("SliceNowQuestion", currentCulture), CustomKeyboards.NoYesKeyboard))
               .Permit(Trigger.SliceLater, ConversationState.Idle)
               .Permit(Trigger.StartSlicing, ConversationState.Slicing);


            machine.Configure(ConversationState.Printing)
               .OnEntryAsync(async () => await StartPrinting());

        }

        private async Task StartPrinting()
        {
            // octoprint connection herstellen
            // gcode hochladen
            // druck starten

            printStarted = true;
            calibrationFinished = false;
            firstTemperatureReceivedMessage = true;
            updateTemperatureMessage = true;
            temperatureReached = false;
            octoprinServer = new OctoprintServer("192.168.2.197", "F1D0D415AF734647B739B34E8B55304F"); //move api key to appsettings.json. this octopi instance is only reachable from local network
            octoprinServer.TemperatureReceived += OctoprinServer_TemperatureReceived;
            octoprinServer.PrinterHoming += OctoprinServer_PrinterHoming;
            octoprinServer.CalibrationFinishedAndWaitingForFinalTemperature += OctoprinServer_CalibrationFinishedAndWaitingForFinalTemperature;
            var tmp = octoprinServer.GeneralOperations.Login();
            await octoprinServer.StartWebsocketAsync(tmp.name, tmp.session);
            await octoprinServer.FileOperations.UploadFileAsync(lastGcodeFile, "local", true, true);
        }

        private async void OctoprinServer_CalibrationFinishedAndWaitingForFinalTemperature(object sender, int args)
        {
            temperaturToReach = args;
            calibrationFinished = true;
            await SendMessageAsync(resourceManager.GetString("CalibrationFinished", currentCulture));
        }


        private async void OctoprinServer_PrinterHoming(object sender, EventArgs e)
        {
            await SendMessageAsync(resourceManager.GetString("Homing", currentCulture));
        }

        private async void OctoprinServer_TemperatureReceived(object sender, TemperReceivedEventArgs e)
        {
            if(firstTemperatureReceivedMessage)
            {
                firstTemperatureReceivedMessage = false;
                lastTemperaturMessageString = $"Temperatur {resourceManager.GetString("Nozzle", currentCulture)}: {e.ToolActual}°C/{e.ToolTarget}°C {Environment.NewLine}Temperatur Buildplate: {e.BedActual}°C/{e.BedTarget}°C";

                var message = await bot.SendTextMessageAsync(Id, lastTemperaturMessageString);
                temperatureMessageId = message.MessageId;
            }
            else
            {
                try
                {
                    var newMessage = $"Temperatur {resourceManager.GetString("Nozzle", currentCulture)}: {e.ToolActual}°C/{e.ToolTarget}°C {Environment.NewLine}Temperatur Buildplate: {e.BedActual}°C/{e.BedTarget}°C";
                    if (newMessage != lastTemperaturMessageString && updateTemperatureMessage)
                    {
                        await bot.EditMessageTextAsync(Id, temperatureMessageId, newMessage);
                        lastTemperaturMessageString = newMessage;
                    }

                    if(calibrationFinished && Math.Abs(e.ToolActual-temperaturToReach) <= 2 && !temperatureReached)
                    {
                        temperatureReached = true;
                        updateTemperatureMessage = false;
                        await SendMessageAsync( resourceManager.GetString("PrintStarting", currentCulture));
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        private async Task StartCollectingDataForPrintingAsync()
        {
            collectingDataForPrintingDialog = new CollectPrintInformationDialog(Id, bot, printObject, lastGcodeFile, contexts, resourceManager, currentCulture);
            collectingDataForPrintingDialog.StartModelSearch += CollectingDataForPrintingDialog_StartModelSearch;
            collectingDataForPrintingDialog.StartSlicing += CollectingDataForPrintingDialog_StartSlicing;
            collectingDataForPrintingDialog.StartPrinting += CollectingDataForPrintingDialog_StartPrinting;
            await collectingDataForPrintingDialog.StartAsync();
        }

        private async void CollectingDataForPrintingDialog_StartPrinting(object sender, string e)
        {
            lastGcodeFile = e;
            await machine.FireAsync(Trigger.StartPrint);
        }

        private async void CollectingDataForPrintingDialog_StartSlicing(object sender, string e)
        {
            selectedModelUrl = e;
            await machine.FireAsync(Trigger.StartSlicing);
        }

        private async void CollectingDataForPrintingDialog_StartModelSearch(object sender, string e)
        {

            printObject = e;
            await machine.FireAsync(Trigger.SearchModel);
            //await StartSearchModelProcessAsync();
        }
        private async Task StartSearchModelProcessAsync()
        {
            this.searchModelProcess = new SearchModelDialog(Id, bot, resourceManager, currentCulture, printObject);
            searchModelProcess.SearchAborted += SearchModelProcess_SearchAborted;
            searchModelProcess.SearchCompleted += SearchModelProcess_SearchCompleted;
            await searchModelProcess.StartAsync();
        }

        private async void SearchModelProcess_SearchCompleted(object sender, Tuple<string,string> NameAndUrl)
        {
            (modelName, selectedModelUrl) = NameAndUrl;
            await machine.FireAsync(Trigger.SearchCompleted);
        }

        private async void SearchModelProcess_SearchAborted(object sender, EventArgs e)
        {
            await machine.FireAsync(Trigger.SearchAborted);
        }

        private async Task CheckbeforePrintDialog()
        {
            this.startPrintProcess = new StartPrintProcess(Id, bot, lastGcodeFile, resourceManager, currentCulture);
            startPrintProcess.PrintStarted += StartPrintProcess_PrintStarted;
            await startPrintProcess.StartAsync();
        }

        private async void StartPrintProcess_PrintStarted(object sender, EventArgs e)
        {
            await machine.FireAsync(Trigger.PrintStarted);
        }

        private async Task StartSlicingAsync()
        {
            
            if(selectedModelUrl.EndsWith(".stl"))
            {
                modelName = new Uri(selectedModelUrl).Segments[^1];
            }
            
            this.slicingProcess = new SlicingProcess(Id, bot, selectedModelUrl, modelName, resourceManager, currentCulture);
            slicingProcess.SlicingProcessCompletedWithoutStartPrint += SlicingProcess_SlicingProcessWithoutCompleted;
            slicingProcess.SlicingProcessCompletedWithStartPrint += SlicingProcess_SlicingProcessCompletedWithStartPrint;
            await slicingProcess.StartAsync();
        }

        private async void SlicingProcess_SlicingProcessCompletedWithStartPrint(object sender, string gcodeLink)
        {
            
            var gcodeUri = new Uri("http://localhost:5003" + gcodeLink);
            await DownloadGcodeAsync(gcodeUri);

            await machine.FireAsync(Trigger.SlicingCompletedWithPrintStart);
        }

        private async Task<bool> DownloadGcodeAsync(Uri remotelocaltion, string fileName = null)
        {
            string downloadPath;
            if (fileName == null && remotelocaltion.Segments[^1].EndsWith(".gcode"))
            {
                downloadPath = Path.Combine(".\\Gcode", remotelocaltion.Segments[^1]);
            }
            else if(fileName!= null && fileName.EndsWith(".gcode"))
            {
                downloadPath = Path.Combine(".\\Gcode", fileName);
            }
            else
            {
                throw new ArgumentException();
            }

            lastGcodeFile = downloadPath;
            using (WebClient webclient = new WebClient())
            {
                try
                {
                    await webclient.DownloadFileTaskAsync(remotelocaltion, downloadPath);
                }
                catch (Exception e)
                {
                    return false;
                }
                return true;
            }
        }

        private async void SlicingProcess_SlicingProcessWithoutCompleted(object sender, EventArgs e)
        {
            await machine.FireAsync(Trigger.SlicingCompletedWithoutPrintStart);
        }

        private async Task AskForPrintNowAsync()
        {
            //await SendMessageAsync("I got your model. Do you want to slice it now?", CustomKeyboards.NoYesKeyboard);
            await SendMessageAsync(resourceManager.GetString("GcodeRecieved", currentCulture), CustomKeyboards.NoYesKeyboard);
        }

        private async Task AskForSlicingNowAsync()
        {
            //await SendMessageAsync("I got your model. Do you want to slice it now?", CustomKeyboards.NoYesKeyboard);
            await SendMessageAsync(resourceManager.GetString("StlRecieved", currentCulture), CustomKeyboards.NoYesKeyboard);
        }

        private async Task StartWorkflowTutorialAsync()
        {
            ITutorialDataProvider data = new WorkflowTutorialDataProvider(resourceManager.GetString("WorkflowTutorialFilePath", currentCulture));
            var tutorial = new WorkflowTutorial(Id, bot, data);
            tutorial.Finished += Tutorial_Finished;
            this.tutorial = tutorial;
            await tutorial.StartAsync();
        }

        private async void Tutorial_Finished(object sender, EventArgs e)
        {
            await machine.FireAsync(Trigger.WorkTutorialFinished);
        }

        private async Task StartHardwareTutorialAsync()
        {
            ITutorialDataProvider data = new HardwareTutorialDataProvider(resourceManager.GetString("HardwareTutorialFilePath", currentCulture));
            var tutorial = new HardwareTutorial(Id, bot, data);
            this.tutorial = tutorial;
            tutorial.TutorialFinished += Tutorial_TutorialFinished;
            await tutorial.StartAsync();
        }

        private async void Tutorial_TutorialFinished(object sender, EventArgs e)
        {
            await machine.FireAsync(Trigger.HardwareTutorialFinished);
        }

        private async Task SendGreetingWithNameAsync()
        {
            await SendMessageAsync($"Hi {UserName}.");
        }

        private async Task SendWelcomeMessageAsync()
        {
            await SendMessageAsync(resourceManager.GetString("greeting", currentCulture));
        }
        private async Task<Telegram.Bot.Types.Message> SendMessageAsync(string text, IReplyMarkup replyKeyboardMarkup = null)
        {
            replyKeyboardMarkup ??= new ReplyKeyboardRemove(); // if null then
            
            return await bot.SendTextMessageAsync(chatId: Id,
                            text: text,
                            replyMarkup: replyKeyboardMarkup);
        }

        public async Task HandleUserInputAsync(Update update)
        {
           
            if ((update.Type == UpdateType.Message &&update.Message.Text != null) || update.Type == UpdateType.CallbackQuery || update.Message.Document != null) //only react on normal messages, ignores edited messages.
            {
                switch (machine.State)
                {
                    case ConversationState.Connected:
                        {
                            if (update.Type == UpdateType.Message)
                            {
                                await machine.FireAsync(Trigger.Starting);
                            }
                            break;
                        }
                    case ConversationState.ConversationStarting:
                        break;
                    case ConversationState.EnteringNamen:
                        {
                            if (update.Type == UpdateType.Message)
                            {
                                AssignUserName(update.Message.Text);
                                await machine.FireAsync(Trigger.NameEntered);
                            }
                            break;
                        }
                    case ConversationState.Idle:
                        {
                            if (update.Message.Document != null)
                            {

                                var path = update.Message.Document.FileName;
                                
                                if (Path.GetExtension(update.Message.Document.FileName) == ".stl")
                                {
                                    selectedModelUrl = Path.GetFullPath(path); // this should be done after the user confirmed to slice the file now.
                                    path = Path.Combine(".\\Models", path);

                                    using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                                    {
                                        var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);
                                    }

                                    await machine.FireAsync(Trigger.STLFileReceived);
                                
                                }
                                else if (Path.GetExtension(update.Message.Document.FileName) == ".gcode")
                                {
                                    path = Path.Combine(".\\Gcode", path);
                                    lastGcodeFile = path;
                                    using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                                    {
                                        var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);
                                    }
                                    await machine.FireAsync(Trigger.GcodeFileReceived);
                                }
                                else
                                {
                                    await SendMessageAsync("other file");
                                }
                            }
                            else
                            {
                                var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text);

                                switch (intent)
                                {
                                    case HardwareTutorialStartIntent:
                                        {
                                            await machine.FireAsync(Trigger.StartHardwareTutorial);
                                            break;
                                        }
                                    case DefaultFallbackIntent defaultFallbackIntent:
                                        {
                                            await SendMessageAsync(defaultFallbackIntent.Process());
                                            break;
                                        }
                                    case WelcomeIntent welcomeIntent:
                                        {
                                            await SendMessageAsync(welcomeIntent.Process());
                                            break;
                                        }
                                    case WorkflowTutorialStartIntent:
                                        {
                                            await machine.FireAsync(Trigger.StartWorkflowTutorial);
                                            break;
                                        }

                                    case StartPrint startPrintIntent:
                                        {
                                            printObject = null;
                                            contexts = new List<string>();
                                            foreach (var context in startPrintIntent.response.QueryResult.OutputContexts)
                                            {
                                                contexts.Add(context.ContextName.ContextId);
                                            }
                                            if (startPrintIntent.response.QueryResult.Parameters.Fields.ContainsKey("object"))
                                            {
                                                printObject = startPrintIntent.response.QueryResult.Parameters.Fields["object"].StringValue;
                                            }
                                            //startPrintIntent.response.QueryResult.Parameters
                                            await machine.FireAsync(Trigger.StartPrint);
                                            break;
                                        }
                                    case SkillInformation skillIntent:
                                        {
                                            await SendMessageAsync(skillIntent.Process());
                                            break;
                                        }
                                    case SearchModel:
                                        {
                                            printObject = null;
                                            await machine.FireAsync(Trigger.SearchModel);
                                            break;
                                        }
                                    default:
                                        {
                                            if (intent != null)
                                            {
                                                try
                                                {
                                                    await SendMessageAsync(((BaseIntent)intent).Process());

                                                }
                                                catch (Exception)
                                                {
                                                    await SendMessageAsync("Intent detected:" + intent.GetType() + ". There is no implementation for this intent yet.");

                                                }
                                            }
                                            break;
                                        }
                                }
                            }
                            break;
                        }
                    case ConversationState.HardwareTutorial:
                        { 
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup");
                            switch (intent)
                            {
                                case TutorialNext next:
                                    {
                                        await tutorial.NextAsync();
                                        break;
                                    }
                                case TutorialCancel:
                                    {
                                        await tutorial.CancelAsync();
                                        await machine.FireAsync(Trigger.HardwareTutorialCanceled);
                                        break;
                                    }
                                case DefaultFallbackIntent defaultIntent:
                                    {
                                        await SendMessageAsync(defaultIntent.Process(), new ReplyKeyboardMarkup(
                                                                                        new KeyboardButton[] { resourceManager.GetString("CancelExplanation", currentCulture), resourceManager.GetString("Next", currentCulture) },
                                                                                        resizeKeyboard: true));
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Zwischenfragen kann ich leider noch nicht beantworten.");
                                        break;
                                    }
                            }

                            break;
                        }
                    case ConversationState.HardwareTutorialFinished:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup");
                            switch (intent)
                            {
                                case TutorialYes yes:
                                    {
                                        await machine.FireAsync(Trigger.StartWorkflowTutorial);
                                        break;
                                    }
                                case TutorialNo no:
                                    {
                                        await SendMessageAsync("Okay.");
                                        await machine.FireAsync(Trigger.HardwareTutorialFinished);
                                        break;
                                    }
                                default:
                                    break;
                            }

                            break;
                        }
                    case ConversationState.WorkflowTutorial:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup");
                            switch (intent)
                            {
                                case TutorialNext next:
                                    {
                                        await tutorial.NextAsync();
                                        break;
                                    }
                                case TutorialCancel:
                                    {
                                        await tutorial.CancelAsync();
                                        await machine.FireAsync(Trigger.WorkTutorialCanceled);
                                        break;
                                    }
                                case DefaultFallbackIntent defaultIntent:
                                    {
                                        await SendMessageAsync(defaultIntent.Process(), new ReplyKeyboardMarkup(
                                                                                        new KeyboardButton[] { resourceManager.GetString("CancelExplanation", currentCulture), resourceManager.GetString("Next", currentCulture) },
                                                                                        resizeKeyboard: true));
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Zwischenfragen kann ich leider noch nicht beantworten.");
                                        break;
                                    }
                            }
                            break;
                        }
                    case ConversationState.SendStlFile:
                        break;
                    case ConversationState.SendGcodeFile:
                        break;
                    case ConversationState.Slicing:
                        {
                            await slicingProcess.HandleInputAsync(update);
                            break;
                        }
                    case ConversationState.CheckBeforePrint:
                        {
                            await startPrintProcess.HandleInputAsync(update);
                            break;
                        }
                    case ConversationState.STLFileReceived:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                            switch (intent)
                            {
                                case TutorialYes:
                                    {
                                        await machine.FireAsync(Trigger.StartSlicing);
                                        break;
                                    }
                                case TutorialNo:
                                    {
                                        //await SendMessageAsync("Okay, I will store it for you. You can ask me later to slice it.");
                                        await SendMessageAsync(resourceManager.GetString("StoringFile", currentCulture));
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                default:
                                    break;
                            }

                            break;
                        }
                    case ConversationState.GcodeFileReceived:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                            switch (intent)
                            {
                                case TutorialYes:
                                    {
                                        await machine.FireAsync(Trigger.StartPrint);
                                        break;
                                    }
                                case TutorialNo:
                                    {
                                        //await SendMessageAsync("Okay, I will store it for you. You can ask me later to slice it.");
                                        await SendMessageAsync(resourceManager.GetString("StoringFile", currentCulture));
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                default:
                                    break;
                            }

                            break;
                        }
                    case ConversationState.SearchModel:
                        {
                            await searchModelProcess.HandleInputAsync(update);
                            break;
                        }
                    case ConversationState.AskToSliceSelectedFile:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "AskToSliceFile"); //reuse the Yes No intents from the tutorial
                            switch (intent)
                            {
                                case AskToSliceFileYes:
                                    {
                                        await machine.FireAsync(Trigger.StartSlicing);
                                        break;
                                    }
                                case AskToSliceFileNo:
                                    {
                                        //await SendMessageAsync("Okay, I will store it for you. You can ask me later to slice it.");
                                        await SendMessageAsync(resourceManager.GetString("StoringFile", currentCulture));
                                        await machine.FireAsync(Trigger.SliceLater);
                                        break;
                                    }
                                default:
                                    break;
                            }
                            break;
                        }
                    case ConversationState.CollectDataForPrint:
                        {
                            await collectingDataForPrintingDialog.HandleInputAsync(update);
                            break;
                        }
                    case ConversationState.Printing:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "Printing");

                            switch (intent)
                            {
                                case PrintStatus:
                                    {
                                        var snapshot = await octoprinServer.GeneralOperations.GetSnapShotAsync();

                                        await bot.SendPhotoAsync(Id, new Telegram.Bot.Types.InputFiles.InputOnlineFile(snapshot));

                                        var jobinfo = await octoprinServer.JobOpertations.GetJobInformationAsync();
                                        var message = resourceManager.GetString("CurrentState", currentCulture) + Environment.NewLine +
                                                      $"{resourceManager.GetString("Progess", currentCulture)}: {jobinfo.progress.completion:F0}%" + Environment.NewLine;
                                        
                                        if (jobinfo.progress.printTimeLeft.HasValue)
                                        {
                                            message += $"{resourceManager.GetString("RemainingTime", currentCulture)}: {TimeSpan.FromSeconds(jobinfo.progress.printTimeLeft.Value).Humanize()}";
                                        }

                                        await SendMessageAsync(message);
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
            else if(update.Type == UpdateType.Message && update.Message.Voice != null)
            {
                await SendMessageAsync("Sprachnachrichten unterstütze ich noch nicht :(");
            }
        }

        private void AssignUserName(string name)
        {
            UserName = name;
        }
    }
}
