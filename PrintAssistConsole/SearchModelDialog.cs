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
        Start = 0,
    }

    public class SearchModelDialog
    {


        private enum Trigger
        {
            Start,
            Cancel,
        }
        private SearchModelDialogDataProvider dialogData;
        private long id;
        private ITelegramBotClient bot;
        private StateMachine<SearchModelState, Trigger> machine;


        private List<string> dfContexts = new List<string>() { "startprintprocedure" };
        private List<string> imageUris;
        private int selectedImage;

        public SearchModelDialog(long chatId, ITelegramBotClient bot)
        {
            id = chatId;
            this.bot = bot;
            dialogData = new SearchModelDialogDataProvider();

            // Instantiate a new state machine in the Start state
            machine = new StateMachine<SearchModelState, Trigger>(SearchModelState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(SearchModelState.BeforeStart)
                .Permit(Trigger.Start, SearchModelState.Start);

            machine.Configure(SearchModelState.Start)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));
            #endregion
        }


        public async Task HandleInputAsync(Update update)
        {
            if (machine.State == SearchModelState.Start)
            {
                imageUris = await ThingiverseAPIWrapper.SearchThingsAsync(update.Message.Text);
                selectedImage = 0;
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("<", "<"),
                        InlineKeyboardButton.WithCallbackData("Auswählen", "select"),
                        InlineKeyboardButton.WithCallbackData(">", ">")
                    }
                });


                //foreach (var uri in tmp)
                //{
                //    Console.WriteLine(uri);
                await bot.SendPhotoAsync(chatId: id, photo: new InputOnlineFile(new Uri(imageUris[selectedImage])), replyMarkup: inlineKeyboard);
                //}
            }
            else
            {
                var intent = await IntentDetector.Instance.CallDFAPIAsync(id, update.Message.Text, dfContexts, true);


                if (intent is DefaultFallbackIntent)
                {
                    await SendMessageAsync(((DefaultFallbackIntent)intent).Process());
                    await SendMessageAsync(machine.State);
                }
                else
                {
                    //var outputContexts = ((BaseIntent)intent).response.QueryResult.OutputContexts;

                    //SetDFContext(outputContexts);
                }
            }
        }

        public async Task<Message> SendInlineKeyboard(ITelegramBotClient botClient, Telegram.Bot.Types.Message message)
        {
            //await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //// Simulate longer running task
            //await Task.Delay(500);

            //var inlineKeyboard = new InlineKeyboardMarkup(new[]
            //{
            //        // first row
            //        new []
            //        {
            //            InlineKeyboardButton.WithCallbackData("<", "<"),
            //            InlineKeyboardButton.WithCallbackData("Auswählen", "select"),
            //            InlineKeyboardButton.WithCallbackData(">", ">")
            //        }
            //    });

            //return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
            //                                            text: "Choose",
            //                                            replyMarkup: inlineKeyboard);

            return null;
        }


        private async Task SendMessageAsync(string message)
        {
            await bot.SendChatActionAsync(id, ChatAction.Typing);
            await bot.SendTextMessageAsync(chatId: id,
                        text: message
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
            await machine.FireAsync(Trigger.Start);
        }

        internal async Task HandleCallbackQueryAsync(Update update)
        {
            

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // first row
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("<", "<"),
                        InlineKeyboardButton.WithCallbackData("Auswählen", "select"),
                        InlineKeyboardButton.WithCallbackData(">", ">")
                    }
                });

            //var tmp = new InputOnlineFile(new Uri(imageUris[selectedImage++]));

            var tmp3 = new InputMediaPhoto(new InputMedia(imageUris[++selectedImage]));

            await bot.EditMessageMediaAsync(id, update.CallbackQuery.Message.MessageId, tmp3, inlineKeyboard);


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
