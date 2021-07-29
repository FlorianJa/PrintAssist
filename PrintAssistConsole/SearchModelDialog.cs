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
using PrintAssistConsole.ThingiverseAPI;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public enum SearchModelState : int
    {
        BeforeStart = -1,
        EnteringSearchTerm = 0,
        ModelSelection = 1,
        SearchCompleted = 2,
    }

    public class SearchModelDialog
    {


        private enum Trigger
        {
            Start,
            Cancel,
            SearchTermEntered,
            RestartSearch,
            ModelSelected,
        }
        private SearchModelDialogDataProvider dialogData;
        private long id;
        private ITelegramBotClient bot;
        private StateMachine<SearchModelState, Trigger> machine;


        private List<string> dfContexts = new List<string>() { "selectingModel" };
        private Things things;
        private int currentImage;
        private int pageCount = 10;
        private int currentSearchPage = 1;
        private string searchTerm;
        private int selectionMessageId;
        private int fileId;
        private string imageUrl;
        private string modelUrl;
        private string modelName;

        public event EventHandler SearchAborted;
        public event EventHandler<Tuple<string,string>> SearchCompleted;

        public SearchModelDialog(long chatId, ITelegramBotClient bot, string searchTerm = null)
        {
            id = chatId;
            this.bot = bot;
            dialogData = new SearchModelDialogDataProvider();
            this.searchTerm = searchTerm;
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<SearchModelState, Trigger>(SearchModelState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(SearchModelState.BeforeStart)
                //.OnExitAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Start, SearchModelState.EnteringSearchTerm)
                .Permit(Trigger.SearchTermEntered, SearchModelState.ModelSelection);

            machine.Configure(SearchModelState.EnteringSearchTerm)
                //.OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.SearchTermEntered, SearchModelState.ModelSelection);

            machine.Configure(SearchModelState.ModelSelection)
                .Permit(Trigger.RestartSearch, SearchModelState.EnteringSearchTerm)
                .Permit(Trigger.ModelSelected, SearchModelState.SearchCompleted);

            machine.Configure(SearchModelState.SearchCompleted)
                .OnEntry(() => SearchCompleted(this, new Tuple<string,string>(modelName,modelUrl)));
            #endregion
        }


        public async Task HandleInputAsync(Update update)
        {

            switch (machine.State)
            {
                case SearchModelState.BeforeStart:
                    break;
                case SearchModelState.EnteringSearchTerm:
                    {
                        searchTerm = update.Message.Text;
                        await InitalThingiverseAPIReqeustAsync();
                        break;
                    }
                case SearchModelState.ModelSelection:
                    {
                        if(update.Type == UpdateType.CallbackQuery)
                        {
                            await HandleCallbackQueryAsync(update);
                        }
                        else // type == message
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContexts, true);

                            switch (intent)
                            {
                                case NewSearchTerm:
                                    {
                                        await bot.EditMessageReplyMarkupAsync(id, selectionMessageId);
                                        await SendMessageAsync("Okay.", new ReplyKeyboardRemove());
                                        await machine.FireAsync(Trigger.RestartSearch);
                                        break;
                                    }
                                case AbortSearch:
                                    {
                                        await SendMessageAsync("Okay.", new ReplyKeyboardRemove());
                                        break;
                                    }
                                default:
                                    break;
                            }
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        private async Task InitalThingiverseAPIReqeustAsync()
        {
            
            await SendMessageAsync($"Ok, ich suche nach: *{searchTerm}*", ParseMode.Markdown);

            things = await ThingiverseAPIWrapper.SearchThingsAsync(searchTerm, currentSearchPage, pageCount);
            if (things != null)
            {
                await SendMessageAsync($"Ich habe {things.TotalHits} Ergebnisse gefunden. Benutze die Pfeil-Buttons unter dem Bild um durch die Suchergebnisse zu navigieren. Wenn dir ein Modell gefällt, klicke auf \"Auswählen\".", CustomKeyboards.AbortSearchNewSearchTermKeyboard);
                currentImage = 0;

                (fileId, imageUrl) = await ThingiverseAPIWrapper.GetImageURLByThingId(things.Hits[0].Id);


                var tmp = await bot.SendPhotoAsync(chatId: id, photo: new InputOnlineFile(new Uri(imageUrl)), caption: things.Hits[currentImage].Name, replyMarkup: CustomKeyboards.SelectNextInlineKeyboard);
                selectionMessageId = tmp.MessageId;
            }
            else
            {
                await SendMessageAsync($"Ich habe keine Ergebnisse für *{searchTerm}* gefunden. Probier es mit einem anderen Begriff", ParseMode.Markdown);
            }

            await machine.FireAsync(Trigger.SearchTermEntered);
        }

        internal async Task HandleCallbackQueryAsync(Update update)
        {
            await bot.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            InlineKeyboardMarkup keyboard = null;

            if (update.CallbackQuery.Data == "select") //select
            {
                (modelName,modelUrl) = await ThingiverseAPIWrapper.GetDownloadLinkForFileById(things.Hits[currentImage].Id, fileId);
                await SendMessageAsync("Okay.");
                await machine.FireAsync(Trigger.ModelSelected);
            }
            else if (update.CallbackQuery.Data == "<") //previous
            {
                if (currentImage == 0 && currentSearchPage > 1) //get next page
                {
                    currentImage = 9;
                    currentSearchPage--;
                    things = await ThingiverseAPIWrapper.SearchThingsAsync(searchTerm, currentSearchPage, pageCount);
                    keyboard = CustomKeyboards.PreviousSelectNextInlineKeyboard;
                }
                else if (currentImage == 1 && currentSearchPage == 1)
                {
                    currentImage--;
                    keyboard = CustomKeyboards.SelectNextInlineKeyboard;
                }
                else
                {
                    currentImage--;
                    keyboard = CustomKeyboards.PreviousSelectNextInlineKeyboard;
                }
            }
            else if (update.CallbackQuery.Data == ">") //next
            {
                if (currentImage + (currentSearchPage - 1) * pageCount == things.TotalHits - 2) // -2 weil beim letzten bild kein weiter pfeil angezeigt werden soll.
                {
                    keyboard = CustomKeyboards.PreviousSelectInlineKeyboard;
                    currentImage++;
                }
                else if (currentImage == pageCount - 1) //get next page
                {
                    currentImage = 0;
                    currentSearchPage++;
                    things = await ThingiverseAPIWrapper.SearchThingsAsync(searchTerm, currentSearchPage, pageCount);
                    keyboard = CustomKeyboards.PreviousSelectNextInlineKeyboard;
                }
                else
                {
                    currentImage++;
                    keyboard = CustomKeyboards.PreviousSelectNextInlineKeyboard;
                }
            }

            try
            {
                (fileId, imageUrl) = await ThingiverseAPIWrapper.GetImageURLByThingId(things.Hits[currentImage].Id);
                if(!string.IsNullOrEmpty(imageUrl))
                {
                    var newPhoto = new InputMediaPhoto(new InputMedia(imageUrl));
                    await bot.EditMessageMediaAsync(id, update.CallbackQuery.Message.MessageId, newPhoto);
                    await bot.EditMessageCaptionAsync(id, update.CallbackQuery.Message.MessageId, things.Hits[currentImage].Name, replyMarkup: keyboard);
                }
                //if (things.Hits[selectedImage].PreviewImage.ToLower().EndsWith(".jpg") ||
                //    things.Hits[selectedImage].PreviewImage.ToLower().EndsWith(".png") ||
                //    things.Hits[selectedImage].PreviewImage.ToLower().EndsWith(".bmp"))
                //{
                    
                //}
            }
            catch (Exception ex)
            {

            }
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
        private async Task SendMessageAsync(SearchModelState state)
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
            if (string.IsNullOrEmpty(searchTerm))
            {
                await machine.FireAsync(Trigger.Start);
            }
            else
            {
                await InitalThingiverseAPIReqeustAsync();
                //await machine.FireAsync(Trigger.SearchTermEntered);
            }


        }

        
    }




    public class SearchModelDialogDataProvider : ITutorialDataProvider
    {
        public Dictionary<StartPrintProcessState, Message> messages { get; set; }

        public SearchModelDialogDataProvider()
        {
            // deserialize JSON directly from a file
            using (StreamReader streamReader = System.IO.File.OpenText(@".\BotContent\SearchModelDialog.json"))
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
