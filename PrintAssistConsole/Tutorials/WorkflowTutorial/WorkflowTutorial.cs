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

    public enum WorkflowTutorialState : int { BeforeStart = -1,
        Start,
        Model,
        Slicing,
        ParametersIntro,
        LayerHeight,
        Infill,
        Support, 
        SliceOptions,
        UploadOptions,
        PrintTime,
        PostProcessing,
        End,
        Cancel
    }

    public class WorkflowTutorial: Tutorial
    {
        private enum Trigger { Next, Cancel }
        
        private readonly StateMachine<WorkflowTutorialState, Trigger> machine;

        public WorkflowTutorial(long chatId, ITelegramBotClient bot, ITutorialDataProvider tutorialData): base(chatId,bot,tutorialData)
        {
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<WorkflowTutorialState, Trigger>(WorkflowTutorialState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(WorkflowTutorialState.BeforeStart)
                .Permit(Trigger.Next, WorkflowTutorialState.Start);

            // Configure the start state
            machine.Configure(WorkflowTutorialState.Start)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.Model)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the Model state
            machine.Configure(WorkflowTutorialState.Model)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.Slicing)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the Slicing state
            machine.Configure(WorkflowTutorialState.Slicing)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.ParametersIntro)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the ParametersIntro state
            machine.Configure(WorkflowTutorialState.ParametersIntro)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.LayerHeight)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the LayerHeight state
            machine.Configure(WorkflowTutorialState.LayerHeight)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.Infill)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the Infill state
            machine.Configure(WorkflowTutorialState.Infill)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.Support)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the Support state
            machine.Configure(WorkflowTutorialState.Support)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.SliceOptions)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the SliceOptions state
            machine.Configure(WorkflowTutorialState.SliceOptions)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.UploadOptions)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the UploadOptions state
            machine.Configure(WorkflowTutorialState.UploadOptions)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.PrintTime)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the PrintTime state
            machine.Configure(WorkflowTutorialState.PrintTime)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.PostProcessing)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the PostProcessing state
            machine.Configure(WorkflowTutorialState.PostProcessing)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State))
                .Permit(Trigger.Next, WorkflowTutorialState.End)
                .Permit(Trigger.Cancel, WorkflowTutorialState.Cancel);

            // Configure the End state
            machine.Configure(WorkflowTutorialState.End)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));

            // Configure the Cancel state
            machine.Configure(WorkflowTutorialState.Cancel)
                .OnEntryAsync(async () => await SendMessageAsync(machine.State));
            #endregion
        }

        public override async Task<bool> NextAsync()
        {
            await machine.FireAsync(Trigger.Next);
            return machine.State == WorkflowTutorialState.End;
        }

        public override async Task CancelAsync()
        {
            await machine.FireAsync(Trigger.Cancel);
        }
        private async Task SendMessageAsync(WorkflowTutorialState state)
        {
            var message = tutorialData.GetMessage((int)state);
            await SendTutorialMessageAsync(chatId, message);

        }
    }
}
