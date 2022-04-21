using Google.Protobuf.Collections;
using Newtonsoft.Json;
using PrintAssistConsole.Intents;
using Stateless;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace PrintAssistConsole
{
    public enum StartPrintProcessState : int
    {
        BeforeStart = -1,
        Start = 0,
        CheckBuildplate = 1,
        Canceled = 2,
        CheckFilament = 3,
        BuildplateHelp = 4,
        FilamentHelp = 5,
        PrinterReady = 6,
        ChangeFilament = 7,
        WaitForFilamentChanged = 8,
        ChangeFilamentGuide = 9,
        PrintStarted = 10,
    }

    public class StartPrintProcess
    {
        private enum Trigger
        {
            Start,
            Cancel,
            StartPrintProcedure,
            GetHelpForBuildplate,
            BuildplateIsReady,
            GetHelpForFilament,
            FilamentIsNotReady,
            FilamentIsReady,
            StartPrintNow,
            ShowHelpForChangingFilament,
            ChangeIndependently,
            FilamentChanged,
            Skip
        }

        private StartPrintDialogDataProvider dialogData;
        private ITelegramBotClient bot;
        private long id;
        private string localGcodePath;
        private readonly ResourceManager resourceManager;
        private readonly CultureInfo currentCulture;
        private StateMachine<StartPrintProcessState, Trigger> machine;
        private List<string> dfContexts = new List<string>() { "startprintprocedure" };

        public event EventHandler PrintStarted;
        public event EventHandler StartPrintCanceled;
        
        public StartPrintProcess(long chatId, ITelegramBotClient bot, string localGcodePath, ResourceManager resourceManager, CultureInfo currentCulture)
        {
            this.localGcodePath = localGcodePath;
            this.resourceManager = resourceManager;
            this.currentCulture = currentCulture;

            dialogData = new StartPrintDialogDataProvider(resourceManager.GetString("StartPrintDialogDataPath", currentCulture));
            this.bot = bot;
            this.id = chatId;
            

            // Instantiate a new state machine in the Start state
            machine = new StateMachine<StartPrintProcessState, Trigger>(StartPrintProcessState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(StartPrintProcessState.BeforeStart)
                .Permit(Trigger.Start, StartPrintProcessState.Start);

            machine.Configure(StartPrintProcessState.Start)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.StartPrintProcedure, StartPrintProcessState.CheckBuildplate)
                .Permit(Trigger.Skip, StartPrintProcessState.PrintStarted);

            machine.Configure(StartPrintProcessState.CheckBuildplate)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.GetHelpForBuildplate, StartPrintProcessState.BuildplateHelp)
                .Permit(Trigger.BuildplateIsReady, StartPrintProcessState.CheckFilament);

            machine.Configure(StartPrintProcessState.CheckFilament)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.GetHelpForFilament, StartPrintProcessState.FilamentHelp)
                .Permit(Trigger.FilamentIsNotReady, StartPrintProcessState.ChangeFilament)
                .Permit(Trigger.FilamentIsReady, StartPrintProcessState.PrinterReady);

            machine.Configure(StartPrintProcessState.PrinterReady)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.StartPrintNow, StartPrintProcessState.PrintStarted);

            machine.Configure(StartPrintProcessState.PrintStarted)
                .OnEntryAsync(async () => { await SendMessageAsync(machine.State); PrintStarted?.Invoke(this, null); });

            machine.Configure(StartPrintProcessState.BuildplateHelp)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.BuildplateIsReady, StartPrintProcessState.CheckFilament);

            machine.Configure(StartPrintProcessState.FilamentHelp)
               .OnEntryAsync(async () => await SendMessageAsync(machine.State))
               .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
               .Permit(Trigger.FilamentIsReady, StartPrintProcessState.PrinterReady)
               .Permit(Trigger.FilamentIsNotReady, StartPrintProcessState.ChangeFilament);

            machine.Configure(StartPrintProcessState.ChangeFilament)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.ShowHelpForChangingFilament, StartPrintProcessState.ChangeFilamentGuide)
              .Permit(Trigger.ChangeIndependently, StartPrintProcessState.WaitForFilamentChanged);

            machine.Configure(StartPrintProcessState.ChangeFilamentGuide)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.FilamentIsReady, StartPrintProcessState.PrinterReady); ;

            machine.Configure(StartPrintProcessState.WaitForFilamentChanged)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.FilamentChanged, StartPrintProcessState.PrinterReady);

            #endregion

        }

        public async Task HandleInputAsync(Update update)
        {
            var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContexts, true);


            if (intent is DefaultFallbackIntent)
            {
                await SendMessageAsync(((DefaultFallbackIntent)intent).Process());
                await SendMessageAsync(machine.State);
            }
            else
            {
                var outputContexts = ((BaseIntent)intent).response.QueryResult.OutputContexts;

                SetDFContext(outputContexts);

                switch (machine.State)
                {
                    case StartPrintProcessState.BeforeStart:
                        break;
                    case StartPrintProcessState.Start:
                        {
                            switch (intent)
                            {
                                case SkipCheck:
                                    {
                                        await machine.FireAsync(Trigger.Skip);
                                        break;
                                    }
                                case StartProcedure:
                                    {
                                        await machine.FireAsync(Trigger.StartPrintProcedure);
                                        break;
                                    }
                                case StartProcedureCancel:
                                    {
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.CheckBuildplate:
                        {
                            switch (intent)
                            {
                                case BuildplateIsReady:
                                    {
                                        await machine.FireAsync(Trigger.BuildplateIsReady);
                                        break;
                                    }
                                case StartProcedureCancel:
                                    {
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                case GetHelpForBuildplate:
                                    {
                                        await machine.FireAsync(Trigger.GetHelpForBuildplate);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.Canceled:
                        break;
                    case StartPrintProcessState.CheckFilament:
                        {
                            switch (intent)
                            {
                                case FilamentIsReady:
                                    {
                                        await machine.FireAsync(Trigger.FilamentIsReady);
                                        break;
                                    }
                                case FilamentIsNotReady:
                                    {
                                        await machine.FireAsync(Trigger.FilamentIsNotReady);
                                        break;
                                    }
                                case StartProcedureCancel:
                                    {
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                case GetHelpForFilament:
                                    {
                                        await machine.FireAsync(Trigger.GetHelpForFilament);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.BuildplateHelp:
                        {
                            switch (intent)
                            {
                                case BuildplateIsReady:
                                    {
                                        await machine.FireAsync(Trigger.BuildplateIsReady);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.FilamentHelp:
                        {
                            switch (intent)
                            {
                                case FilamentIsNotReady:
                                    {
                                        await machine.FireAsync(Trigger.FilamentIsNotReady);
                                        break;
                                    }
                                case FilamentIsReady:
                                    {
                                        await machine.FireAsync(Trigger.FilamentIsReady);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.PrinterReady:
                        {
                            switch (intent)
                            {
                                case StartPrintNow:
                                    {
                                        await machine.FireAsync(Trigger.StartPrintNow);
                                        break;
                                    }
                                case StartProcedureCancel:
                                    {
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.ChangeFilament:
                        {
                            switch (intent)
                            {
                                case ShowHelpForChangingFilament:
                                    {
                                        await machine.FireAsync(Trigger.ShowHelpForChangingFilament);
                                        break;
                                    }
                                case ChangeIndependently:
                                    {
                                        await machine.FireAsync(Trigger.ChangeIndependently);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.WaitForFilamentChanged:
                        {
                            switch (intent)
                            {
                                case FilamentChanged:
                                    {
                                        await machine.FireAsync(Trigger.FilamentChanged);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.ChangeFilamentGuide:
                        {
                            switch (intent)
                            {
                                case FilamentChanged:
                                case BackToStartPrintProcedure:
                                    {
                                        await machine.FireAsync(Trigger.FilamentIsReady);
                                        break;
                                    }
                                default:
                                    {
                                        await SendMessageAsync("Ich habe folgenden Intent erkannt: " + intent.GetType().ToString() + "Dieser Intent wird hier noch nicht unterstützt.");
                                        await SendMessageAsync(machine.State);
                                        break;
                                    }
                            }
                            break;
                        }
                    case StartPrintProcessState.PrintStarted:
                        break;
                    default:
                        {
                            
                            break;
                        }
                }
            }
        }

        private void SetDFContext(RepeatedField<Google.Cloud.Dialogflow.V2.Context> outputContexts)
        {
            if (outputContexts.Count == 1 && dfContexts.Count == 1)
            {
                if (outputContexts[0].ContextName.ContextId == dfContexts[0])
                {
                    return;
                }
                else
                {
                    dfContexts.Clear();
                    dfContexts.Add(outputContexts[0].ContextName.ContextId);
                }
            }
            else
            {
                foreach (var outputContext in outputContexts)
                {
                    var contextName = outputContext.ContextName.ContextId.ToLower();
                    if (dfContexts.Contains(contextName))
                    {
                        dfContexts.Remove(contextName);
                    }
                    else
                    {
                        dfContexts.Add(contextName);
                    }
                }
            }
        }

        private async Task SendMessageAsync(string message)
        {
                await bot.SendChatActionAsync(id, ChatAction.Typing);
                await bot.SendTextMessageAsync(chatId: id,
                            text: message
                            );
        }
        private async Task SendMessageAsync(StartPrintProcessState state)
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

                    await bot.SendMediaGroupAsync(chatId: id, media: album);

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

                    await bot.SendMediaGroupAsync(chatId: id, media: album);
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

        public async Task StartAsync()
        {
            await machine.FireAsync(Trigger.Start);
        }
    }


    public class StartPrintDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<StartPrintProcessState, Message> messages { get; set; }

        public StartPrintDialogDataProvider(string path)
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(path))
            {
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    messages = serializer.Deserialize<Dictionary<StartPrintProcessState, Message>>(jsonReader);
                }
            }
        }

        public Message GetMessage(int state)
        {
            return messages[(StartPrintProcessState)state];
        }

        public int GetMessageCount()
        {
            return messages.Count;
        }
    }
}
