using Humanizer;
using OctoPrintConnector;
using PrintAssistConsole.Intents;
using Stateless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
            SearchAborted
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

        public ConversationState CurrentState
        { 
            get
            {
                return machine.State;
            }
        }

        public Conversation(Int64 id, ITelegramBotClient bot)
        {
            Id = id;
            this.bot = bot;

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
                .Permit(Trigger.ConversationStarted, ConversationState.EnteringNamen);

            machine.Configure(ConversationState.EnteringNamen)
                .OnExitAsync(async () => await SendGreetingWithNameAsync())
                .Permit(Trigger.NameEntered, ConversationState.Idle);
                //.Permit(Trigger.NameEntered, ConversationState.Idle);

            machine.Configure(ConversationState.Idle)
                //.OnEntryAsync(async () => await SendMessageAsync("What can i do for you?"))
                .OnEntryAsync(async () => await SendMessageAsync("Was kann ich für dich tun?"))
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.StartHardwareTutorial, ConversationState.HardwareTutorial)
                .Permit(Trigger.STLFileReceived, ConversationState.STLFileReceived)
                .Permit(Trigger.SearchModel, ConversationState.SearchModel)
                .Permit(Trigger.StartPrint, ConversationState.CollectDataForPrint);

            machine.Configure(ConversationState.CollectDataForPrint)
                .OnEntryAsync(async () => await StartCollectingDataForPrintingAsync())
                .Permit(Trigger.StartSlicing, ConversationState.Slicing)
                .Permit(Trigger.SearchModel, ConversationState.SearchModel);

            machine.Configure(ConversationState.HardwareTutorial)
                .OnEntryAsync(async () => await StartHardwareTutorialAsync())
                .Permit(Trigger.HardwareTutorialFinished, ConversationState.Idle)
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.HardwareTutorialCanceled, ConversationState.Idle);

            machine.Configure(ConversationState.WorkflowTutorial)
                .OnEntryAsync(async () => await StartWorkflowTutorialAsync())
                .Permit(Trigger.WorkTutorialCanceled, ConversationState.Idle)
                .Permit(Trigger.WorkTutorialFinished, ConversationState.Idle);

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
               .OnEntryAsync(async () => await SendMessageAsync("Möchtest du die Datei jetzt slicen und drucken?", CustomKeyboards.NoYesKeyboard))
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
            octoprinServer = new OctoprintServer("octopi.local", "F1D0D415AF734647B739B34E8B55304F"); //move api key to appsettings.json. this octopi instance is only reachable from local network
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
            await SendMessageAsync("Die Kalibrierung ist abgeschlossen. Der Drucker heizt auf seine finale Betriebstempertur auf. Es geht gleich los.");
        }


        private async void OctoprinServer_PrinterHoming(object sender, EventArgs e)
        {
            await SendMessageAsync("Der Druck fährt nun zu seiner Startposition und führt anschließend seine automatische Kalibrierung aus.");
        }

        private async void OctoprinServer_TemperatureReceived(object sender, TemperReceivedEventArgs e)
        {
            if(firstTemperatureReceivedMessage)
            {
                firstTemperatureReceivedMessage = false;
                lastTemperaturMessageString = $"Temperatur Düse: {e.ToolActual}°C/{e.ToolTarget}°C {Environment.NewLine}Temperatur Buildplate: {e.BedActual}°C/{e.BedTarget}°C";

                var message = await bot.SendTextMessageAsync(Id, lastTemperaturMessageString);
                temperatureMessageId = message.MessageId;
            }
            else
            {
                try
                {
                    var newMessage = $"Temperatur Düse: {e.ToolActual}°C/{e.ToolTarget}°C {Environment.NewLine}Temperatur Buildplate: {e.BedActual}°C/{e.BedTarget}°C";
                    if (newMessage != lastTemperaturMessageString && updateTemperatureMessage)
                    {
                        await bot.EditMessageTextAsync(Id, temperatureMessageId, newMessage);
                        lastTemperaturMessageString = newMessage;
                    }

                    if(calibrationFinished && Math.Abs(e.ToolActual-temperaturToReach) <= 2 && !temperatureReached)
                    {
                        temperatureReached = true;
                        updateTemperatureMessage = false;
                        await SendMessageAsync("Es geht los!");
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        private async Task StartCollectingDataForPrintingAsync()
        {
            collectingDataForPrintingDialog = new CollectPrintInformationDialog(Id, bot, printObject, contexts);
            collectingDataForPrintingDialog.StartModelSearch += CollectingDataForPrintingDialog_StartModelSearch;
            collectingDataForPrintingDialog.StartSlicing += CollectingDataForPrintingDialog_StartSlicing;
            collectingDataForPrintingDialog.StartPrinting += CollectingDataForPrintingDialog_StartPrinting;
            //collectingDataForPrintingDialog.StartPrintWithModel += CollectingDataForPrintingDialog_StartPrintWithModel;
            //collectingDataForPrintingDialog.StartPrintWithoutModel += CollectingDataForPrintingDialog_StartPrintWithoutModel;
            await collectingDataForPrintingDialog.StartAsync();
        }

        private void CollectingDataForPrintingDialog_StartPrinting(object sender, string e)
        {
            throw new NotImplementedException();
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

        private async void CollectingDataForPrintingDialog_StartPrintWithoutModel(object sender, string e)
        {
            printObject = e;
            await machine.FireAsync(Trigger.SearchModel);
        }

        private async void CollectingDataForPrintingDialog_StartPrintWithModel(object sender, string e)
        {
            selectedModelUrl = e; 
            await machine.FireAsync(Trigger.StartSlicing);
        }

        private async Task StartSearchModelProcessAsync()
        {
            this.searchModelProcess = new SearchModelDialog(Id, bot, printObject);
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
            this.startPrintProcess = new StartPrintProcess(Id, bot, lastGcodeFile);
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
            
            this.slicingProcess = new SlicingProcess(Id, bot, selectedModelUrl, modelName);
            slicingProcess.SlicingProcessCompletedWithoutStartPrint += SlicingProcess_SlicingProcessWithoutCompleted;
            slicingProcess.SlicingProcessCompletedWithStartPrint += SlicingProcess_SlicingProcessCompletedWithStartPrint;
            await slicingProcess.StartAsync();
        }

        private async void SlicingProcess_SlicingProcessCompletedWithStartPrint(object sender, string gcodeLink)
        {
            await machine.FireAsync(Trigger.SlicingCompletedWithPrintStart);
            var gcodeUri = new Uri("http://localhost:5003" + gcodeLink);
            await DownloadGcodeAsync(gcodeUri);
            
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

        private async Task AskForSlicingNowAsync()
        {
            //await SendMessageAsync("I got your model. Do you want to slice it now?", CustomKeyboards.NoYesKeyboard);
            await SendMessageAsync("Ich hab dein Modell erhalten. Möchtest du das Modell jetzt slicen?", CustomKeyboards.NoYesKeyboard);
        }

        private async Task StartWorkflowTutorialAsync()
        {
            ITutorialDataProvider data = new WorkflowTutorialDataProvider();
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
            ITutorialDataProvider data = new HardwareTutorialDataProvider();
            var tutorial = new HardwareTutorial(Id, bot, data);
            this.tutorial = tutorial;
            await tutorial.StartAsync();
        }

        private async Task SendGreetingWithNameAsync()
        {
            await SendMessageAsync($"Hi {UserName}.");
        }

        private async Task SendWelcomeMessageAsync()
        {
            //await SendMessageAsync("Hi I am your print assistant. I can do stuff for you. How should i call you?");
            await SendMessageAsync("Hi. Ich bin dein persönlicher Druckassistent. Ich kann XXX für dich tun. Wie soll ich dich nennen?");
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
           
            if (update.Type == UpdateType.Message || update.Type == UpdateType.CallbackQuery) //only react on normal messages, ignores edited messages.
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
                                using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                                {
                                    var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);
                                }

                                if (Path.GetExtension(update.Message.Document.FileName) == ".stl")
                                {
                                    selectedModelUrl = Path.GetFullPath(path); // this should be done after the user confirmed to slice the file now.
                                    await machine.FireAsync(Trigger.STLFileReceived);
                                }
                                else if (Path.GetExtension(update.Message.Document.FileName) == ".gcode")
                                {
                                    await SendMessageAsync("got gcode");
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
                                    default:
                                        await SendMessageAsync("Intent detected:" + intent.GetType() + ". There is no implementation for this intent yet.");
                                        break;
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
                                case DefaultFallbackIntent defaultIntent:
                                    {
                                        await SendMessageAsync(defaultIntent.Process(), new ReplyKeyboardMarkup(
                                                                                        new KeyboardButton[] { "Erklärung abbrechen", "Weiter" },
                                                                                        resizeKeyboard: true));
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
                                default:
                                    break;
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
                                        await SendMessageAsync("Okay, ich passe auf die Datei auf. Du kannst mich später nochmal fragen.");
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
                                        await SendMessageAsync("Okay, ich passe auf die Datei auf. Du kannst mich später nochmal fragen.");
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
                                        var message = "Aktueller Status:" + Environment.NewLine +
                                                      $"Fortschritt: {jobinfo.progress.completion:F0}%" + Environment.NewLine;
                                        
                                        if (jobinfo.progress.printTimeLeft.HasValue)
                                        {
                                            message += $"Restliche Druckdauer: {TimeSpan.FromSeconds(jobinfo.progress.printTimeLeft.Value).Humanize()}";
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
            //else if(update.Type == UpdateType.CallbackQuery)
            //{
            //    if (searchModelProcess != null)
            //    {
            //        await searchModelProcess.HandleCallbackQueryAsync(update);
            //    }
                
                


            //}

        }

     

        private void AssignUserName(string name)
        {
            UserName = name;
        }
    }
}
