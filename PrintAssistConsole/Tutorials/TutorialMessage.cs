using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public class TutorialMessage
    {
        public string Text { get; set; }
        public List<string> PhotoFilePaths { get; set; }
        public List<string> VideoFilePaths { get; set; }
        public List<string> KeyboardButtons { get; set; }

        public IReplyMarkup ReplyKeyboardMarkup
        {
            get
            {
                if (KeyboardButtons != null && KeyboardButtons.Count > 0)
                {
                    var keyboard = new ReplyKeyboardMarkup();
                    var buttons = new List<KeyboardButton>();
                    foreach (var name in KeyboardButtons)
                    {
                        buttons.Add(new KeyboardButton(name));
                    }

                    keyboard.Keyboard = new List<List<KeyboardButton>> { buttons };
                    keyboard.ResizeKeyboard = true;
                    return keyboard;
                }
                else
                {
                    return new ReplyKeyboardRemove();
                }
            }
        }


        public TutorialMessage(string text = null, List<string> photoFilePath = null, List<string> videoFilePath = null, List<string> keyboardButtons = null)
        {
            Text = text;
            PhotoFilePaths = photoFilePath;
            VideoFilePaths = videoFilePath;
            KeyboardButtons = keyboardButtons;
        }

        public TutorialMessage()
        {

        }
    }
}
