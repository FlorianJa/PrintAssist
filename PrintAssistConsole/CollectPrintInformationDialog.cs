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
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{

    public enum CollectPrintInformationState : int
    {
        Start = -1,
        ConfirmInput = 0,
        AskForFile = 1,
        EnteringObjectName = 2,
        SearchModel = 3,
        ModelSelection = 4,
        End = 5,
        Abort = 6,
        EndWithSTL = 7,
        EndWithGcode = 8,
        WaitForFile = 9
    }



    public class CollectPrintInformationDialog
    {
        private enum Trigger
        {
            AskForFile,
            ConfirmInput,
            InputCorrect,
            InputIncorrect,
            NoFile,
            InputEntered,
            AutoTransition,
            NewSearchTerm, // delete
            FileAvailable,
            Abort,
            ModelSelected,
            STLFileAvailable,
            GCodeFileAvailable,
        }

        private CollectPrintInformationDialogDataProvider dialogData;
        private long id;
        private ITelegramBotClient bot;
        private readonly string printObject1;
        private StateMachine<CollectPrintInformationState, Trigger> machine;
        private string printObject;
        private List<string> contexts;
        private readonly ResourceManager resourceManager;
        private readonly CultureInfo currentCulture;
        private List<string> dfContexts = new List<string>() { "PrintObjectRecognised" };
        private string selectedModelUrl;
        private CollectPrintInformationState previousState;

        public event EventHandler<string> StartModelSearch;
        public event EventHandler<string> StartSlicing;
        public event EventHandler<string> StartPrinting;

        public CollectPrintInformationDialog(long chatId, ITelegramBotClient bot, string printObject, List<string> contexts, ResourceManager resourceManager, CultureInfo currentCulture)
        {
            id = chatId;
            this.bot = bot;
            printObject1 = printObject;
            dialogData = new CollectPrintInformationDialogDataProvider(resourceManager.GetString("CollectPrintInformationDataPath", currentCulture));
            this.printObject = new String(printObject);
            this.contexts = contexts;
            this.resourceManager = resourceManager;
            this.currentCulture = currentCulture;
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<CollectPrintInformationState, Trigger>(CollectPrintInformationState.Start);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(CollectPrintInformationState.Start)
                .OnExit(() =>previousState = machine.State)
                .Permit(Trigger.ConfirmInput, CollectPrintInformationState.ConfirmInput)
                .Permit(Trigger.AskForFile, CollectPrintInformationState.AskForFile);

            machine.Configure(CollectPrintInformationState.ConfirmInput)
                .OnEntryAsync(SendConfirmationQuestionAsync)
                .OnExit(() => previousState = machine.State)
                .PermitDynamic(Trigger.InputCorrect,  () =>{ return previousState == CollectPrintInformationState.Start ? CollectPrintInformationState.AskForFile : CollectPrintInformationState.SearchModel;})
                .Permit(Trigger.InputIncorrect, CollectPrintInformationState.EnteringObjectName);

            machine.Configure(CollectPrintInformationState.AskForFile)
                .OnEntryAsync(async () => await SendMessageAsync(resourceManager.GetString("FileAvialable", currentCulture), CustomKeyboards.NoYesKeyboard))
                .OnExit(() => previousState = machine.State)
                .PermitDynamic(Trigger.NoFile, () => { return previousState == CollectPrintInformationState.ConfirmInput ? CollectPrintInformationState.SearchModel : CollectPrintInformationState.EnteringObjectName; })
                .Permit(Trigger.FileAvailable, CollectPrintInformationState.WaitForFile)
                .Permit(Trigger.STLFileAvailable, CollectPrintInformationState.EndWithSTL)
                .Permit(Trigger.GCodeFileAvailable, CollectPrintInformationState.EndWithGcode);

            machine.Configure(CollectPrintInformationState.EnteringObjectName)
                .OnEntryFromAsync(Trigger.NoFile, async () => await SendMessageAsync(resourceManager.GetString("WhatToPrint", currentCulture)))
                .OnEntryFromAsync(Trigger.InputCorrect, async () => await SendMessageAsync(resourceManager.GetString("WhatToPrint", currentCulture)))
                .OnEntryFromAsync(Trigger.NewSearchTerm, async () => await SendMessageAsync(resourceManager.GetString("NewSearchTerm", currentCulture)))
                .OnExit(() => previousState = machine.State)
                .Permit(Trigger.InputEntered, CollectPrintInformationState.ConfirmInput);

            machine.Configure(CollectPrintInformationState.SearchModel)
                .OnEntry( () => StartSearchDialog())
                .OnExit(() => previousState = machine.State)
                .Permit(Trigger.AutoTransition, CollectPrintInformationState.End);

            machine.Configure(CollectPrintInformationState.Abort);

            machine.Configure(CollectPrintInformationState.EndWithSTL)
                .OnEntry(() => StartSlicing?.Invoke(this, selectedModelUrl));

            machine.Configure(CollectPrintInformationState.EndWithGcode)
                .OnEntry(() => StartPrinting?.Invoke(this, selectedModelUrl));

            machine.Configure(CollectPrintInformationState.WaitForFile)
                .OnEntryAsync(async () => await SendMessageAsync(resourceManager.GetString("SendFile", currentCulture), new ReplyKeyboardRemove()))
                .Permit(Trigger.STLFileAvailable, CollectPrintInformationState.EndWithSTL)
                .Permit(Trigger.GCodeFileAvailable, CollectPrintInformationState.EndWithGcode);

            #endregion

        }

        private void StartSearchDialog()
        {
            StartModelSearch?.Invoke(this, printObject);
        }


        private async Task SendConfirmationQuestionAsync()
        {
            var message = String.Format(resourceManager.GetString("InputConfirmation", currentCulture), printObject);

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
                case CollectPrintInformationState.Start:
                    break;
                case CollectPrintInformationState.ConfirmInput:
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
                case CollectPrintInformationState.EnteringObjectName:
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

                        await machine.FireAsync(Trigger.InputEntered);
                        break;
                    }

                case CollectPrintInformationState.AskForFile:
                    {

                        if(update.Message.Document != null)
                        {
                            if(update.Message.Document.FileSize >= 20000000) //20MB
                            {
                                await SendMessageAsync(resourceManager.GetString("FileTooBig", currentCulture));
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
                                        //await SendMessageAsync("Okay. Schick mir die Datei bitte zu.", new ReplyKeyboardRemove());
                                        await machine.FireAsync(Trigger.FileAvailable);
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
                        break;
                    }
                case CollectPrintInformationState.WaitForFile:
                    {
                        if (update.Message.Document != null)
                        {
                            if (update.Message.Document.FileSize >= 20000000) //20MB
                            {
                                await SendMessageAsync(resourceManager.GetString("FileTooBig", currentCulture));
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
                                selectedModelUrl = Path.GetFullPath(path);
                                await SendMessageAsync("got gcode");
                                await machine.FireAsync(Trigger.GCodeFileAvailable);
                            }
                            else
                            {
                                await SendMessageAsync("other file");
                            }
                        }
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
                await machine.FireAsync(Trigger.AskForFile);
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

        public CollectPrintInformationDialogDataProvider(string path)
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
