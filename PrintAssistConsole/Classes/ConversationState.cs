using System;

namespace PrintAssistConsole
{
    [Flags]
    public enum ConversationState
    {
        Connected,
        ConversationStarting,
        EnteringNamen,
        Idle,
        HardwareTutorial,
        WorkflowTutorial,
        SendStlFile,
        SendGcodeFile,
        Slicing,
        CheckBeforePrint,
        Unknown,
        STLFileReceived,
        Printing,
        SearchModel,
        AskToSliceSelectedFile,
        CollectDataForPrint
    }
}