﻿using PrintAssistConsole.Intents;
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
                            new KeyboardButton[] { "Nein", "Ja" },
                            resizeKeyboard: true
                        );
            }
        }

        public static ReplyKeyboardMarkup ExpertBeginnerKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "Expert", "Beginner" },
                            resizeKeyboard: true);
            }
        }

        public static ReplyKeyboardMarkup LayerHeightKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "0,07 mm", "0,1 mm", "0,15 mm", "0,2 mm", "0,25 mm", "0,3 mm" },
                            resizeKeyboard: true);
            }
        }
        public static ReplyKeyboardMarkup InfillKeyboard
        {
            get
            {
                return new ReplyKeyboardMarkup(
                            new KeyboardButton[] { "15%", "20%", "25%", "50%", "75%", "100%" },
                            resizeKeyboard: true);
            }
        }
        

    }

    public class Conversation
    {
        private enum Trigger { NameEntered,
            ConversationStarted,
            StartHardwareTutorial,
            Starting,
            HardwareTutorialFinished,
            StartWorkflowTutorial,
            WorkTutorialFinished,
            HardwareTutorialCanceled,
            WorkTutorialCanceled,
            STLFileReceived,
            StartSlicing,
            Cancel
        }

        public Int64 Id { get; private set; }
        public string UserName { get; internal set; }
        private Tutorial tutorial;
        private SlicingProcess slicingProcess;
        private string selectedModel;
        private readonly StateMachine<ConversationState, Trigger> machine;
        protected readonly ITelegramBotClient bot;

        public ConversationState CurrentState
        { 
            get
            {
                return machine.State;
            }
        }

        public Conversation(Int64 id, ITelegramBotClient bot)
        {
            Id = id;
            this.bot = bot;

            // Instantiate a new state machine in the Start state
            machine = new StateMachine<ConversationState, Trigger>(ConversationState.Connected);
            SetUpStateMachine();
        }

        public async Task StartAsync()
        {
            //await machine.FireAsync(Trigger.Starting);
        }

        private void SetUpStateMachine()
        {
            machine.Configure(ConversationState.Connected)
                .Permit(Trigger.Starting, ConversationState.ConversationStarting);

            machine.Configure(ConversationState.ConversationStarting)
                .OnEntryAsync(async () => { await SendWelcomeMessageAsync(); await machine.FireAsync(Trigger.ConversationStarted); })
                .Permit(Trigger.ConversationStarted, ConversationState.EnteringNamen);

            machine.Configure(ConversationState.EnteringNamen)
                .OnExitAsync(async () => await SendGreetingWithNameAsync())
                .Permit(Trigger.NameEntered, ConversationState.Idle);

            machine.Configure(ConversationState.Idle)
                .OnEntryAsync(async () => await SendMessageAsync("What can i do for you?"))
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.StartHardwareTutorial, ConversationState.HardwareTutorial)
                .Permit(Trigger.STLFileReceived, ConversationState.STLFileReceived);

            machine.Configure(ConversationState.HardwareTutorial)
                .OnEntryAsync(async () => await StartHardwareTutorialAsync())
                .Permit(Trigger.HardwareTutorialFinished, ConversationState.Idle)
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.HardwareTutorialCanceled, ConversationState.Idle);

            machine.Configure(ConversationState.WorkflowTutorial)
                .OnEntryAsync(async () => await StartWorkflowTutorialAsync())
                .Permit(Trigger.WorkTutorialCanceled, ConversationState.Idle)
                .Permit(Trigger.WorkTutorialFinished, ConversationState.Idle);

            machine.Configure(ConversationState.STLFileReceived)
                .OnEntryAsync(async () => await AskForSlicingNowAsync())
                .Permit(Trigger.Cancel, ConversationState.Idle)
                .Permit(Trigger.StartSlicing, ConversationState.Slicing);

            machine.Configure(ConversationState.Slicing)
                .OnEntryAsync(async () => await StartSlicingAsync())
                .Permit(Trigger.Cancel, ConversationState.Idle);
                
        }

        private async Task StartSlicingAsync()
        {
            this.slicingProcess = new SlicingProcess(Id, bot, selectedModel);
            await slicingProcess.StartAsync();
        }

        private async Task AskForSlicingNowAsync()
        {
            await SendMessageAsync("I got your model. Should I slice it for you now?", CustomKeyboards.NoYesKeyboard);
        }

        private async Task StartWorkflowTutorialAsync()
        {
            ITutorialDataProvider data = new WorkflowTutorialDataProvider();
            var tutorial = new WorkflowTutorial(Id, bot, data);
            this.tutorial = tutorial;
            await tutorial.StartAsync();
        }

        private async Task StartHardwareTutorialAsync()
        {
            ITutorialDataProvider data = new HardwareTutorialDataProvider();
            var tutorial = new HardwareTutorial(Id, bot, data);
            this.tutorial = tutorial;
            await tutorial.StartAsync();
        }

        private async Task SendGreetingWithNameAsync()
        {
            await SendMessageAsync($"Hi {UserName}.");
        }

        private async Task SendWelcomeMessageAsync()
        {
            await SendMessageAsync("Hi I am your print assistant. I can do stuff for you. How should i call you?");
        }
        private async Task SendMessageAsync(string text, IReplyMarkup replyKeyboardMarkup = null)
        {
            replyKeyboardMarkup ??= new ReplyKeyboardRemove(); // if null then
            
            await bot.SendTextMessageAsync(chatId: Id,
                            text: text,
                            replyMarkup: replyKeyboardMarkup);
        }

        public async Task HandleUserInputAsync(Update update)
        {
           
            if (update.Type == UpdateType.Message) //only react on normal messages, ignores edited messages.
            {
                switch (machine.State)
                {
                    case ConversationState.Connected:
                        {
                            await machine.FireAsync(Trigger.Starting);
                            break;
                        }
                    case ConversationState.ConversationStarting:
                        break;
                    case ConversationState.EnteringNamen:
                        {
                            AssignUserName(update.Message.Text);
                            await machine.FireAsync(Trigger.NameEntered);
                            break;
                        }
                    case ConversationState.Idle:
                        {
                            if (update.Message.Document != null)
                            {
                                var path = update.Message.Document.FileName;
                                using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
                                {
                                    var tmp = await bot.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, fileStream);
                                }

                                if (Path.GetExtension(update.Message.Document.FileName) == ".stl")
                                {
                                    selectedModel = Path.GetFullPath(path); // this should be done after the user confirmed to slice the file now.
                                    await machine.FireAsync(Trigger.STLFileReceived);
                                }
                                else if (Path.GetExtension(update.Message.Document.FileName) == ".gcode")
                                {
                                    await SendMessageAsync("got gcode");
                                }
                                else
                                {
                                    await SendMessageAsync("other file");
                                }
                            }
                            else
                            {
                                var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text);

                                switch (intent)
                                {
                                    case HardwareTutorialStartIntent:
                                        {
                                            await machine.FireAsync(Trigger.StartHardwareTutorial);
                                            break;
                                        }
                                    case DefaultFallbackIntent defaultFallbackIntent:
                                        {
                                            await SendMessageAsync(defaultFallbackIntent.Process());
                                            break;
                                        }
                                    case WelcomeIntent welcomeIntent:
                                        {
                                            await SendMessageAsync(welcomeIntent.Process());
                                            break;
                                        }
                                    case WorkflowTutorialStartIntent:
                                        {
                                            await machine.FireAsync(Trigger.StartWorkflowTutorial);
                                            break;
                                        }
                                    default:
                                        await SendMessageAsync("Intent detected:" + intent.GetType() + ". There is no implementation for this intent yet.");
                                        break;
                                }
                            }
                            break;
                        }
                    case ConversationState.HardwareTutorial:
                        { 
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup");
                            switch (intent)
                            {
                                case TutorialNext next:
                                    {
                                        await tutorial.NextAsync();
                                        break;
                                    }
                                case TutorialCancel:
                                    {
                                        await tutorial.CancelAsync();
                                        await machine.FireAsync(Trigger.HardwareTutorialCanceled);
                                        break;
                                    }
                                case TutorialYes yes:
                                    {
                                        await machine.FireAsync(Trigger.StartWorkflowTutorial);
                                        break;
                                    }
                                case TutorialNo no:
                                    {
                                        await SendMessageAsync("Okay.");
                                        await machine.FireAsync(Trigger.HardwareTutorialFinished);
                                        break;
                                    }
                                case DefaultFallbackIntent defaultIntent:
                                    {
                                        await SendMessageAsync(defaultIntent.Process(), new ReplyKeyboardMarkup(
                                                                                        new KeyboardButton[] { "Erklärung abbrechen", "Weiter" },
                                                                                        resizeKeyboard: true));
                                        break;
                                    }
                                default:
                                    break;
                            }

                            break;
                        }
                    case ConversationState.WorkflowTutorial:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup");
                            switch (intent)
                            {
                                case TutorialNext next:
                                    {
                                        await tutorial.NextAsync();
                                        break;
                                    }
                                case TutorialCancel:
                                    {
                                        await tutorial.CancelAsync();
                                        await machine.FireAsync(Trigger.WorkTutorialCanceled);
                                        break;
                                    }
                                default:
                                    break;
                            }
                            break;
                        }
                    case ConversationState.SendStlFile:
                        break;
                    case ConversationState.SendGcodeFile:
                        break;
                    case ConversationState.Slicing:
                        {
                            await slicingProcess.HandleInputAsync(update);
                            break;
                        }
                    case ConversationState.StartingPrint:
                        break;
                    case ConversationState.STLFileReceived:
                        {
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text, "TutorialStarten-followup"); //reuse the Yes No intents from the tutorial
                            switch (intent)
                            {
                                case TutorialYes:
                                    {
                                        await machine.FireAsync(Trigger.StartSlicing);
                                        break;
                                    }
                                case TutorialNo:
                                    {
                                        await SendMessageAsync("Okay, I will store it for you. You can ask me later to slice it.");
                                        await machine.FireAsync(Trigger.Cancel);
                                        break;
                                    }
                                default:
                                    break;
                            }

                            break;
                        }
                    default:
                        break;
                }
            }
        }

     

        private void AssignUserName(string name)
        {
            UserName = name;
        }
    }
}