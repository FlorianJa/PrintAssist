using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public class Message
    {
        public string Text { get; set; }
        public List<string> PhotoFilePaths { get; set; }
        public List<string> VideoFilePaths { get; set; }
        public List<List<string>> KeyboardButtons { get; set; }
        public List<List<string>> InlineKeyboardButtons { get; set; }


        public IReplyMarkup ReplyKeyboardMarkup
        {
            get
            {
                if (KeyboardButtons != null && KeyboardButtons.Count > 0)
                {
                   
                    var keyboardButtons = new List<List<KeyboardButton>>();

                    foreach (var row in KeyboardButtons)
                    {
                        var rowButtons = new List<KeyboardButton>();
                        foreach (var name in row)
                        {
                            rowButtons.Add(new KeyboardButton(name));
                        }
                        keyboardButtons.Add(rowButtons);
                    }

                    var keyboard = new ReplyKeyboardMarkup(keyboardButtons);
                    //keyboard.Keyboard = keyboardButtons;
                    keyboard.ResizeKeyboard = true;
                    return keyboard;
                }
                else
                {
                    return new ReplyKeyboardRemove();
                }
            }
        }


        public Message(string text = null, List<string> photoFilePath = null, List<string> videoFilePath = null, List<List<string>> keyboardButtons = null)
        {
            Text = text;
            PhotoFilePaths = photoFilePath;
            VideoFilePaths = videoFilePath;
            KeyboardButtons = keyboardButtons;
        }

        public Message()
        {

        }
    }
}
