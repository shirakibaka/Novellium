// Kernel.cs — Novellium OS kernel entry point (lifecycle and initialization)
using System;
using Sys = Cosmos.Kernel.System;
using Novellium.IO;
using Novellium.System;
using Novellium.Process;
using Novellium.Commands;
using Novellium.Tests;

namespace Novellium;

public class Kernel : Sys.Kernel
{
    protected override void BeforeRun()
    {
        Init.Start();
        Init.ShowMotd();

        PManager.Start("novsh", [], Novsh.Run, 1);
    }

    protected override void Run()
    {
        PManager.ReapOrphans();
        Sys.Power.Halt();
    }

    protected override void AfterRun()
    {
        Output.WriteLine();
        OutputInfo.Info("Novellium stopped.");
    }
}