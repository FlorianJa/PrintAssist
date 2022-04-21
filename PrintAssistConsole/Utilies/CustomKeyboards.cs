using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{
    public class CustomKeyboards
    {
        public static ReplyKeyboardMarkup NoYesKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "Nein", "Ja" }
                        )
                        {
                            ResizeKeyboard = true,
                            OneTimeKeyboard = true
                        };
            }
        }

        public static ReplyKeyboardMarkup AbortSearchNewSearchTermKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "Suche abbrechen", "Neuen Suchbegriff eingeben" }
                        )
                {
                    ResizeKeyboard = true
                };
            }
        }

        public static ReplyKeyboardMarkup ExpertBeginnerKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "Expert", "Beginner" })
                {
                    ResizeKeyboard = true
                };
            }
        }

        public static ReplyKeyboardMarkup LayerHeightKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "0,07 mm", "0,1 mm", "0,15 mm", "0,2 mm", "0,25 mm", "0,3 mm" })
                {
                    ResizeKeyboard= true
                };
            }
        }
        public static ReplyKeyboardMarkup InfillKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "15%", "20%", "25%", "50%", "75%", "100%" }
                            )
                {
                    ResizeKeyboard=true
                };
            }
        }

        public static InlineKeyboardMarkup PreviousSelectNextInlineKeyboard
        {
            get
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🡸", "<"),
                        InlineKeyboardButton.WithCallbackData("Auswählen", "select"),
                        InlineKeyboardButton.WithCallbackData("🡺", ">")
                    }
                });
            }
        }

        public static InlineKeyboardMarkup SelectNextInlineKeyboard
        {
            get
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Auswählen", "select"),
                        InlineKeyboardButton.WithCallbackData("🡺", ">")
                    }
                });
            }
        }

        public static InlineKeyboardMarkup PreviousSelectInlineKeyboard
        {
            get
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("🡸", "<"),
                        InlineKeyboardButton.WithCallbackData("Auswählen", "select")
                    }
                });
            }
        }
        public static InlineKeyboardMarkup DontShowAgain
        {
            get
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Hinweis in Zukunft nicht mehr anzeigen.", "DontShowAgain")
                    }
                });
            }
        }

        public static InlineKeyboardMarkup SkillInformationInlineKeyboard
        {
            get
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Drucker erklären", "printer"),
                    },
                    new []
                    {
                    InlineKeyboardButton.WithCallbackData("Workflow erklären", "workflow")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Modell suchen", "search")
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("Druck starten", "start")
                    }
                });
            }
        }
    }
}
