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
    public enum HardwareTutorialState : int { BeforeStart = -1, Start, Printer, Filament, Hotend, FirstLayer, Extruding, Buildplate, End, Cancel }

    public class HardwareTutorial
    {
        private enum Trigger { Next, Cancel }

        private readonly long chatId;
        private readonly ITelegramBotClient bot;
        private readonly StateMachine<HardwareTutorialState, Trigger> machine;
        private readonly ITutorialDataProvider tutorialData;

        public HardwareTutorial(long chatId, ITelegramBotClient bot, ITutorialDataProvider tutorialData)
        {
            this.chatId = chatId;
            this.bot = bot;
            this.tutorialData = tutorialData;

            // Instantiate a new state machine in the Start state
            machine = new StateMachine<HardwareTutorialState, Trigger>(HardwareTutorialState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(HardwareTutorialState.BeforeStart)
                .Permit(Trigger.Next, HardwareTutorialState.Start);

            // Configure the start state
            machine.Configure(HardwareTutorialState.Start)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Printer)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Printer state
            machine.Configure(HardwareTutorialState.Printer)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Filament)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Filament state
            machine.Configure(HardwareTutorialState.Filament)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Hotend)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Hotend state
            machine.Configure(HardwareTutorialState.Hotend)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.FirstLayer)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the FirstLayer state
            machine.Configure(HardwareTutorialState.FirstLayer)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Extruding)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Extruding state
            machine.Configure(HardwareTutorialState.Extruding)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Buildplate)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Buildplate state
            machine.Configure(HardwareTutorialState.Buildplate)
                .OnEntryAsync(async () => await SendMessage(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.End)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the End state
            machine.Configure(HardwareTutorialState.End)
                .OnEntryAsync(async () => await SendMessage(machine.State));

            // Configure the Cancel state
            machine.Configure(HardwareTutorialState.Cancel)
                .OnEntryAsync(async () => await SendMessage(machine.State));
            #endregion

        }

        public async Task<bool> Next()
        {
            await machine.FireAsync(Trigger.Next);
            return machine.State == HardwareTutorialState.End;
        }

        public async Task Cancel()
        {
            await machine.FireAsync(Trigger.Cancel);
        }
        private async Task SendMessage(HardwareTutorialState state)
        {
            var message = tutorialData.GetMessage((int)state);
            await SendTutorialMessageAsync(chatId, message);
                
        }

        private async Task SendTutorialMessageAsync(Int64 id, TutorialMessage message)
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
    }

    
}
