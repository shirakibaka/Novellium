// State.cs — PState: process lifecycle state enumeration
namespace Novellium.Process;

public enum PState
{
    Created,
    Running,
    Terminated,
    Zombie,
    Failed
}