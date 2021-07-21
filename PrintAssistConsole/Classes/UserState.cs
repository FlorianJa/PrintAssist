using System;

namespace PrintAssistConsole
{
    [Flags]
    public enum UserState
    {
        Idle = 1,
        WaitingForUserName = 2,
        Unknown = 4,
        HardwareTutorial = 8,
        WaitingForConfimationToStartWorkflowTutorial = 16,
        WorkflowTutorial = 32,
        ReceivedStlFile = 64
    }
}