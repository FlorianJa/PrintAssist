using Newtonsoft.Json;
using PrintAssistConsole.Intents;
using Stateless;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        ShowRemoveHelp = 4,
        ShowFilamentHelp = 5,
        AskToStartAgain = 6,
        FilamentNotOk = 7,
        WaitForFilamentChanged = 8,
        HelpFilamentChange = 9,
        PrintStarting = 10,
    }

    public class StartPrintProcess
    {
        private enum Trigger
        {
            Start,
            Okay,
            Cancel,
            Next,
            HowToRemove,
            FilamentInformationHelp,
            StartPrint,
            FilamentNotOkay,
            FilamenOkay,
            Help,
            NoHelp,
        }

        private StartPrintDialogDataProvider dialogData;
        private ITelegramBotClient bot;
        private long id;
        private StateMachine<StartPrintProcessState, Trigger> machine;
        private string dfContext = "StartPrint-followup";

        public event EventHandler PrintStarted;
        public event EventHandler StartPrintCanceled;
        
        public StartPrintProcess(long chatId, ITelegramBotClient bot)
        {
            dialogData = new StartPrintDialogDataProvider();
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
                .Permit(Trigger.Okay, StartPrintProcessState.CheckBuildplate);

            machine.Configure(StartPrintProcessState.CheckBuildplate)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.Help, StartPrintProcessState.ShowRemoveHelp )
                .Permit(Trigger.Next, StartPrintProcessState.CheckFilament);

            machine.Configure(StartPrintProcessState.CheckFilament)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.Help, StartPrintProcessState.ShowFilamentHelp)
                .Permit(Trigger.FilamentNotOkay, StartPrintProcessState.FilamentNotOk)
                .Permit(Trigger.Next, StartPrintProcessState.AskToStartAgain);

            machine.Configure(StartPrintProcessState.AskToStartAgain)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.StartPrint, StartPrintProcessState.CheckFilament);

            machine.Configure(StartPrintProcessState.PrintStarting)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));

            machine.Configure(StartPrintProcessState.ShowRemoveHelp)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
                .Permit(Trigger.Next, StartPrintProcessState.CheckFilament);

            machine.Configure(StartPrintProcessState.ShowFilamentHelp)
               .OnEntryAsync(async () => await SendMessageAsync(machine.State))
               .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
               .Permit(Trigger.FilamenOkay, StartPrintProcessState.AskToStartAgain)
               .Permit(Trigger.FilamentNotOkay, StartPrintProcessState.FilamentNotOk);

            machine.Configure(StartPrintProcessState.FilamentNotOk)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.Help, StartPrintProcessState.HelpFilamentChange)
              .Permit(Trigger.NoHelp, StartPrintProcessState.WaitForFilamentChanged);

            machine.Configure(StartPrintProcessState.HelpFilamentChange)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.Next, StartPrintProcessState.AskToStartAgain);

            machine.Configure(StartPrintProcessState.WaitForFilamentChanged)
              .OnEntryAsync(async () => await SendMessageAsync(machine.State))
              .Permit(Trigger.Cancel, StartPrintProcessState.Canceled)
              .Permit(Trigger.Okay, StartPrintProcessState.AskToStartAgain);

            #endregion

        }

        public async Task HandleInputAsync(Update update)
        {
            var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContext); //reuse the Yes No intents from the tutorial
            dfContext = ((BaseIntent)intent).response.QueryResult.OutputContexts[0].ContextName.ContextId;

            switch (machine.State)
            {
                case StartPrintProcessState.BeforeStart:
                    break;
                case StartPrintProcessState.Start:
                    {
                        switch (intent)
                        {
                            case TutorialNext:
                                {
                                    await machine.FireAsync(Trigger.Okay);
                                    break;
                                }
                            case TutorialCancel:
                                {
                                    await machine.FireAsync(Trigger.Cancel);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case StartPrintProcessState.CheckBuildplate:
                    {
                        //var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, "CheckUpNext-followup"); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialNext:
                                {
                                    await machine.FireAsync(Trigger.Okay);
                                    break;
                                }
                            case TutorialCancel:
                                {
                                    await machine.FireAsync(Trigger.Cancel);
                                    break;
                                }
                            case Help:
                                {
                                    await machine.FireAsync(Trigger.Help);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case StartPrintProcessState.Canceled:
                    break;
                case StartPrintProcessState.CheckFilament:
                { 
                    //var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text); //reuse the Yes No intents from the tutorial
                    switch (intent)
                    {
                        case TutorialNext:
                            {
                                await machine.FireAsync(Trigger.Next);
                                break;
                            }
                        case TutorialCancel:
                            {
                                await machine.FireAsync(Trigger.Cancel);
                                break;
                            }
                        case Help:
                            {
                                await machine.FireAsync(Trigger.Help);
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
                case StartPrintProcessState.ShowRemoveHelp:
                    break;
                case StartPrintProcessState.ShowFilamentHelp:
                    break;
                case StartPrintProcessState.AskToStartAgain:
                    {
                        //var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text); //reuse the Yes No intents from the tutorial
                        switch (intent)
                        {
                            case TutorialYes:
                                {
                                    await machine.FireAsync(Trigger.Next);
                                    break;
                                }
                            case TutorialNo:
                                {
                                    await machine.FireAsync(Trigger.Cancel);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case StartPrintProcessState.FilamentNotOk:
                    break;
                case StartPrintProcessState.WaitForFilamentChanged:
                    break;
                case StartPrintProcessState.HelpFilamentChange:
                    break;
                case StartPrintProcessState.PrintStarting:
                    break;
                default:
                    break;
            }
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

        public async Task StartAsync()
        {
            await machine.FireAsync(Trigger.Start);
        }
    }


    public class StartPrintDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<StartPrintProcessState, Message> messages { get; set; }

        public StartPrintDialogDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(@".\BotContent\StartPrintingProcess.json"))
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
