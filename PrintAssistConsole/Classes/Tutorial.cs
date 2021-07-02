using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole.Classes
{
    public interface ITutorialDataProvider
    {
        int GetMessageCount();
        TutorialMessage GetMessageByStepnumber(int stepnumber);
    }

    public class Tutorial
    {
        public bool isFinished { get; private set; }
        ITutorialDataProvider dataProvider;
        private int currentTutorialStep = 0;
        public Tutorial(ITutorialDataProvider dataProvider)
        {
            this.dataProvider = dataProvider;
            isFinished = false;
        }

        public TutorialMessage GetNextMessage()
        {
            if(currentTutorialStep < dataProvider.GetMessageCount())
            {
                
                var message = dataProvider.GetMessageByStepnumber(currentTutorialStep);
                currentTutorialStep++;
                isFinished = message.IsLastMessage;
                return message;
            }
            return null;
        }
    }

    public class TutorialMessage
    {
        public readonly string Text;
        public readonly string PhotoFilePath;
        public readonly string VideoFilePath;
        public readonly IReplyMarkup replyKeyboardMarkup;
        public readonly bool IsLastMessage;

        public TutorialMessage(string text = null, string photoFilePath = null, string videoFilePath = null, bool isLastMessage = false)
        {
            Text = text;
            PhotoFilePath = photoFilePath;
            VideoFilePath = videoFilePath;
            this.IsLastMessage = isLastMessage;
            if (isLastMessage == true)
            {
                replyKeyboardMarkup = new ReplyKeyboardRemove();
            }
            else
            {
                replyKeyboardMarkup = new ReplyKeyboardMarkup(
                        new KeyboardButton[] { "Erklärung abbrechen", "👍" },
                        resizeKeyboard: true
                    );
            }
        }

        //public async Task SendAsync(ITelegramBotClient botClient, Int64 id)
        //{
        //    if (PhotoFilePath != null && File.Exists(PhotoFilePath))
        //    {
        //        await botClient.SendChatActionAsync(id, ChatAction.UploadPhoto);
        //        using FileStream fileStream = new(PhotoFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        //        var fileName = PhotoFilePath.Split(Path.DirectorySeparatorChar).Last();
        //        await botClient.SendPhotoAsync(chatId: id,
        //                                       photo: new InputOnlineFile(fileStream, fileName));

        //    }
        //    //if (PhotoFilePath != null && File.Exists(PhotoFilePath))
        //    //{
        //    //    await botClient.SendChatActionAsync(id, ChatAction.UploadPhoto);
        //    //    using FileStream fileStream = new(PhotoFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        //    //    var fileName = PhotoFilePath.Split(Path.DirectorySeparatorChar).Last();
        //    //    await botClient.SendPhotoAsync(chatId: id,
        //    //                                   photo: new InputOnlineFile(fileStream, fileName));

        //    //}
        //    if (Text != null)
        //    {
        //        await botClient.SendChatActionAsync(id, ChatAction.Typing);
        //        await botClient.SendTextMessageAsync(chatId: id,
        //                    text: Text,
        //                    replyMarkup: replyKeyboardMarkup);

        //    }

        //}
    }


    public class DummyTutorial : ITutorialDataProvider
    {
        private List<TutorialMessage> messages = new List<TutorialMessage>();

        public static DummyTutorial Defaultutorial()
        {
            var tutorial = new DummyTutorial();
            tutorial.messages.Add(new TutorialMessage(text: "Start"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 1"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 2"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 3"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 4"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 5"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 6"));
            tutorial.messages.Add(new TutorialMessage(text: "Schritt 7"));
            tutorial.messages.Add(new TutorialMessage(text: "Ende", isLastMessage: true));
            return tutorial;
        }
        public TutorialMessage GetMessageByStepnumber(int stepnumber)
        {
            return messages[stepnumber];
        }

        public int GetMessageCount()
        {
            return messages.Count;
        }
    }
}
