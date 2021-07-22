using PrintAssistConsole.Intents;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace PrintAssistConsole
{

    public class Conversation
    {
        private enum Trigger { NameEntered,
            ConversationStarted,
            StartHardwareTutorial,
            Starting,
            HardwareTutorialFinished,
            StartWorkflowTutorial,
            WorkTutorialFinished,
            HardwareTutorialCanceled
        }

        public Int64 Id { get; private set; }
        public string UserName { get; internal set; }
        private Tutorial tutorial;

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
                .Permit(Trigger.StartHardwareTutorial, ConversationState.HardwareTutorial);

            machine.Configure(ConversationState.HardwareTutorial)
                .OnEntryAsync(async () => await StartHardwareTutorialAsync())
                .Permit(Trigger.HardwareTutorialFinished, ConversationState.Idle)
                .Permit(Trigger.StartWorkflowTutorial, ConversationState.WorkflowTutorial)
                .Permit(Trigger.HardwareTutorialCanceled, ConversationState.Idle);


            machine.Configure(ConversationState.WorkflowTutorial)
                .OnEntryAsync(async () => await StartWorkflowTutorialAsync())
                .Permit(Trigger.WorkTutorialFinished, ConversationState.Idle);
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
            await SendMessageAsync("Hi I am your Print assist. I can do stuff for you. How should i call you?");
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
                            var intent = await IntentDetector.Instance.CallDFAPIAsync(Id, update.Message.Text);

                            switch (intent)
                            {
                                case TutorialStartIntent:
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
                                default:
                                    await SendMessageAsync("Intent detected:" + intent.GetType() + ". There is no implementation for this intent yet.");
                                    break;
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
                        break;
                    case ConversationState.StartingPrint:
                        break;
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
