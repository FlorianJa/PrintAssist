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
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{

    public enum CollectPrintInformationState : int
    {
        BeforeStart = -1,
        ConfirmingInput = 0,
        EnteringObject = 1,
        AskIfModelOrGcodeIsAvailable = 2,
        StartSearchingModel = 3,
        StartPrint = 4,
        AskToSlice = 5,
    }



    public class CollectPrintInformationDialog
    {
        private enum Trigger
        {
            Start,
            Cancel,
            SearchTermEntered,
            RestartSearch,
            ModelSelected,
            AskForObject,
            ConfirmInput,
            InputCorrect,
            InputIncorrect,
            STLFileAvailable,
            GCodeFileAvailable,
            NoFile,
            StartSearchingModel,
        }
        private CollectPrintInformationDialogDataProvider dialogData;
        private long id;
        private ITelegramBotClient bot;
        private StateMachine<CollectPrintInformationState, Trigger> machine;
        private string printObject;
        private List<string> contexts;
        private List<string> dfContexts = new List<string>() { "PrintObjectRecognised" };
        private string selectedModelUrl;


        public event EventHandler<string> StartPrintWithModel;
        public event EventHandler<string> StartPrintWithoutModel;

        public CollectPrintInformationDialog(long chatId, ITelegramBotClient bot, string printObject, List<string> contexts)
        {
            id = chatId;
            this.bot = bot;
            dialogData = new CollectPrintInformationDialogDataProvider();
            this.printObject = new String(printObject);
            this.contexts = contexts;
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<CollectPrintInformationState, Trigger>(CollectPrintInformationState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(CollectPrintInformationState.BeforeStart)
                .Permit(Trigger.AskForObject, CollectPrintInformationState.AskIfModelOrGcodeIsAvailable)
                .Permit(Trigger.ConfirmInput, CollectPrintInformationState.ConfirmingInput);

            machine.Configure(CollectPrintInformationState.EnteringObject)
                .OnEntryAsync(async () => await SendMessageAsync("Was möchtest du drucken?", new ReplyKeyboardRemove()))
                .Permit(Trigger.ConfirmInput, CollectPrintInformationState.ConfirmingInput);

            machine.Configure(CollectPrintInformationState.ConfirmingInput)
                .OnEntryAsync(async () => await SendConfirmationQuestionAsync())
                .Permit(Trigger.InputCorrect, CollectPrintInformationState.StartSearchingModel)
                .Permit(Trigger.InputIncorrect, CollectPrintInformationState.EnteringObject);

            machine.Configure(CollectPrintInformationState.AskIfModelOrGcodeIsAvailable)
                .OnEntryAsync(async () => await SendMessageAsync("Hast du schon ein 3D Modell oder eine geslicte Datei? Wenn ja, kannst du sie mir einfach schicken.", CustomKeyboards.NoYesKeyboard))
                .Permit(Trigger.STLFileAvailable, CollectPrintInformationState.AskToSlice)
                .Permit(Trigger.GCodeFileAvailable, CollectPrintInformationState.StartPrint)
                .Permit(Trigger.NoFile, CollectPrintInformationState.EnteringObject);


            machine.Configure(CollectPrintInformationState.AskToSlice)
                .OnEntry(() => InvokeStartPrintWithModelEvent());

            machine.Configure(CollectPrintInformationState.StartSearchingModel)
                .OnEntry(() => InvokeStartPrintWithoutModelEvent());
            #endregion

        }

        private void InvokeStartPrintWithoutModelEvent()
        {
            StartPrintWithoutModel?.Invoke(this, printObject);
        }

        private void InvokeStartPrintWithModelEvent()
        {
            StartPrintWithModel?.Invoke(this, selectedModelUrl);
        }

        private async Task SendConfirmationQuestionAsync()
        {
            var message = $"Du möchtest *{printObject}* drucken. Habe ich dich richtig verstanden?";
            await SendMessageAsync(message,CustomKeyboards.NoYesKeyboard, ParseMode.Markdown);
        }

        public async Task HandleInputAsync(Update update)
        {
            //if(machine.State == CollectPrintInformationState.EnteringObject)
            //{

            //}


            //if (intent is DefaultFallbackIntent)
            //{
            //    await SendMessageAsync(((DefaultFallbackIntent)intent).Process());
            //    //await SendMessageAsync(machine.State);
            //}
            //else
            //{

            switch (machine.State)
            {
                case CollectPrintInformationState.BeforeStart:
                    break;
                case CollectPrintInformationState.ConfirmingInput:
                    {
                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContexts, false);
                        switch (intent)
                        {
                            case PrintObjectRecognisedYes:
                                {
                                    await machine.FireAsync(Trigger.InputCorrect);
                                    break;
                                }
                            case PrintObjectRecognisedNo:
                                {
                                    await machine.FireAsync(Trigger.InputIncorrect);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case CollectPrintInformationState.EnteringObject:
                    {
                        //printObject = update.Message.Text;

                        var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, contexts, false);

                        if (intent is StartPrint)
                        {

                            if (((StartPrint)intent).response.QueryResult.Parameters.Fields.ContainsKey("object"))
                            {
                                printObject = new String(((StartPrint)intent).response.QueryResult.Parameters.Fields["object"].StringValue);
                            }
                        }

                        await machine.FireAsync(Trigger.ConfirmInput);
                        break;
                    }

                case CollectPrintInformationState.AskIfModelOrGcodeIsAvailable:
                    {

                        if(update.Message.Document != null)
                        {
                            if(update.Message.Document.FileSize >= 20000000) //20MB
                            {
                                await SendMessageAsync("Die Datei ist leider zu groß für mich. Ich unterstütze aktuell nur Dateien mit bis zu 20 MB");
                                return;
                            }
                            var path = update.Message.Document.FileName;
                            
                            using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                            {
                                var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);
                            }

                            if (Path.GetExtension(update.Message.Document.FileName) == ".stl")
                            {
                                selectedModelUrl = Path.GetFullPath(path); 
                                await machine.FireAsync(Trigger.STLFileAvailable);
                            }
                            else if (Path.GetExtension(update.Message.Document.FileName) == ".gcode")
                            {
                                await SendMessageAsync("got gcode");
                                await machine.FireAsync(Trigger.GCodeFileAvailable);
                            }
                            else
                            {
                                await SendMessageAsync("other file");
                            }
                        }
                        else
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContexts, true);

                            switch (intent)
                            {
                                case PrintObjectRecognisedYes:
                                    {
                                        await SendMessageAsync("Okay. Schick mir die Datei bitte zu.", new ReplyKeyboardRemove());
                                        break;
                                    }
                                case PrintObjectRecognisedNo:
                                    {
                                        //await SendMessageAsync($"Okay. Soll ich schauen was ich im Internet zu *{printObject}* finde?", CustomKeyboards.NoYesKeyboard, ParseMode.Markdown);
                                        await machine.FireAsync(Trigger.NoFile);
                                        break;
                                    }
                                default:
                                    break;
                            }

                        }
                        //stl file

                        //gcode file

                        // no file


                        break;
                    }
                default:
                    break;
            }
            //}
        }

        private async Task SendMessageAsync(string message, IReplyMarkup keyboard, ParseMode parseMode = ParseMode.Default)
        {
            await bot.SendChatActionAsync(id, ChatAction.Typing);
            await bot.SendTextMessageAsync(chatId: id,
                        text: message,
                        replyMarkup: keyboard,
                        parseMode: parseMode
                        ); 
        }

        private async Task SendMessageAsync(string message, IReplyMarkup keyboard)
        {
            await bot.SendChatActionAsync(id, ChatAction.Typing);
            await bot.SendTextMessageAsync(chatId: id,
                        text: message,
                        replyMarkup: keyboard
                        );
        }
        private async Task SendMessageAsync(string message, ParseMode parseMode = ParseMode.Default)
        {
            await bot.SendChatActionAsync(id, ChatAction.Typing);
            await bot.SendTextMessageAsync(chatId: id,
                        text: message,
                        parseMode: parseMode
                        );
        }
        private async Task SendMessageAsync(CollectPrintInformationState state)
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
            if(string.IsNullOrEmpty(printObject))
            {
                await machine.FireAsync(Trigger.AskForObject);
            }
            else
            {
                await machine.FireAsync(Trigger.ConfirmInput);
            }
        }
    }


    public class CollectPrintInformationDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<StartPrintProcessState, Message> messages { get; set; }

        public CollectPrintInformationDialogDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(@".\BotContent\CollectPrintInformationDialog.json"))
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
