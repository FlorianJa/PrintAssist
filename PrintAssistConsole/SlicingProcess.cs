using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace PrintAssistConsole
{
    public enum SlicingProcessState : int
    {
        BeforeStart = -1,
        Start,
        StoringFile,
        ModeSelection,
        ExpertModeLayerHeight,
        BeginnerModeQuality,
        ExpertModeInfill,
        ExpertModeSupport,
        ExpertModeAskToChangeOtherParameters,
        AskToPrintNow,
        ExpertModeAskForParameterName,
        EndSlicingWithoutPrinting,
        EndSlicingWithPrinting,
        BeginnerModeSurfaceSelection,
        BeginnerModeMechanicalForce,
        BeginnerModeOverhangs,
        BeginnerModeSummary,
    }

    public class SlicingProcess
    {
        private enum Trigger
        {
            Next, Cancel,
            Start,
            StoreFile,
            SelectingMode,
            SelectingExpertMode,
            SelectingBeginnerMode,
            LayerHeightEntered,
            InfillEntered,
            ExpertModeSupportEntered,
            Yes,
            No
        }

        private readonly StateMachine<SlicingProcessState, Trigger> machine;

        public SlicingProcess(long chatId, ITelegramBotClient bot)
        {
            // Instantiate a new state machine in the Start state
            machine = new StateMachine<SlicingProcessState, Trigger>(SlicingProcessState.BeforeStart);

            #region setup statemachine
            // Configure the before start state
            machine.Configure(SlicingProcessState.BeforeStart)
                .Permit(Trigger.Start, SlicingProcessState.Start);

            machine.Configure(SlicingProcessState.Start)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.No, SlicingProcessState.StoringFile)
                .Permit(Trigger.Yes, SlicingProcessState.ModeSelection);

            machine.Configure(SlicingProcessState.StoringFile);
            //.OnEntryAsync(async () => ) 

            machine.Configure(SlicingProcessState.ModeSelection)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.SelectingExpertMode, SlicingProcessState.ExpertModeLayerHeight)
                .Permit(Trigger.SelectingBeginnerMode, SlicingProcessState.BeginnerModeQuality);

            machine.Configure(SlicingProcessState.ExpertModeLayerHeight)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.ExpertModeInfill);

            machine.Configure(SlicingProcessState.ExpertModeInfill)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.ExpertModeSupport);

            machine.Configure(SlicingProcessState.ExpertModeSupport)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.AskToPrintNow);

            machine.Configure(SlicingProcessState.AskToPrintNow)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.No, SlicingProcessState.EndSlicingWithoutPrinting)
                .Permit(Trigger.Yes, SlicingProcessState.EndSlicingWithPrinting);

            machine.Configure(SlicingProcessState.BeginnerModeQuality)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeSurfaceSelection);

            machine.Configure(SlicingProcessState.BeginnerModeSurfaceSelection)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeMechanicalForce);

            machine.Configure(SlicingProcessState.BeginnerModeMechanicalForce)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeOverhangs);

            machine.Configure(SlicingProcessState.BeginnerModeOverhangs)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.BeginnerModeSummary);

            machine.Configure(SlicingProcessState.BeginnerModeSummary)
                //.OnEntryAsync(async () => ) 
                .Permit(Trigger.Next, SlicingProcessState.AskToPrintNow);

            #endregion
        }
    }
}
