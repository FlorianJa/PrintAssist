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
        StartingPrint,
        Unknown,
        STLFileReceived,
        Printing
    }
}