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
    public enum HardwareTutorialState : int { BeforeStart = -1,
        Start,
        Printer,
        Filament,
        Hotend,
        FirstLayer,
        Extruding, 
        Buildplate,
        End,
        Cancel 
    }

    public class HardwareTutorial:Tutorial
    {
        private enum Trigger { Start, Next, Cancel }

       
        private readonly StateMachine<HardwareTutorialState, Trigger> machine;


        public HardwareTutorial(long chatId, ITelegramBotClient bot, ITutorialDataProvider tutorialData) : base(chatId, bot, tutorialData)
        {
            // Instantiate a new state machine in the BeforeStart state
            machine = new StateMachine<HardwareTutorialState, Trigger>(HardwareTutorialState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(HardwareTutorialState.BeforeStart)
                .Permit(Trigger.Start, HardwareTutorialState.Start);

            // Configure the start state
            machine.Configure(HardwareTutorialState.Start)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Printer)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Printer state
            machine.Configure(HardwareTutorialState.Printer)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Filament)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Filament state
            machine.Configure(HardwareTutorialState.Filament)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Hotend)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Hotend state
            machine.Configure(HardwareTutorialState.Hotend)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.FirstLayer)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the FirstLayer state
            machine.Configure(HardwareTutorialState.FirstLayer)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Extruding)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Extruding state
            machine.Configure(HardwareTutorialState.Extruding)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.Buildplate)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the Buildplate state
            machine.Configure(HardwareTutorialState.Buildplate)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, HardwareTutorialState.End)
                .Permit(Trigger.Cancel, HardwareTutorialState.Cancel);

            // Configure the End state
            machine.Configure(HardwareTutorialState.End)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));

            // Configure the Cancel state
            machine.Configure(HardwareTutorialState.Cancel)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));
            #endregion

        }

        public override async Task StartAsync()
        {
            await machine.FireAsync(Trigger.Start);
        }

        public override async Task<bool> NextAsync()
        {
            await machine.FireAsync(Trigger.Next);
            return machine.State == HardwareTutorialState.End;
        }

        public override async Task CancelAsync()
        {
            await machine.FireAsync(Trigger.Cancel);
        }

        private async Task SendMessageAsync(HardwareTutorialState state)
        {
            var message = tutorialData.GetMessage((int)state);
            await SendTutorialMessageAsync(chatId, message);
                
        }
    }
}
