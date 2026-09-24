// Process.cs — PInfo: process metadata structure
using System.Threading;

namespace Novellium.Process;

public struct PInfo
{
    public int Pid;
    public int ParentPid;
    public string Name;
    public PState State;
    public int ExitCode;
    public bool KillReq;
    public bool IsWaited;
    public Thread? Thread;
    public string CurrentDirectory;
}