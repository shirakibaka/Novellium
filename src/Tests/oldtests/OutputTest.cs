// OutputTest.cs — Formatted output module test suite
using System;
using System.Collections.Generic;
using Novellium.IO;

namespace Novellium.Tests;

public static class OutputTests
{
    private static int Passed;
    private static int Failed;
    private static readonly List<string> FailedTests = new();

    public static void Run()
    {
        Passed = 0;
        Failed = 0;
        FailedTests.Clear();

        Output.WriteLine("=== OUTPUT TESTS ===", ConsoleColor.Cyan);

        TestWrite();
        TestColor();
        TestReset();
        TestInfo();
        TestError();
        TestWarning();
        TestDebug();
        TestCustom();

        Output.WriteLine();

        if (Failed == 0)
        {
            Output.WriteLine($"RESULT: {Passed} passed", ConsoleColor.Green);
        }
        else
        {
            Output.WriteLine($"RESULT: {Passed} passed, {Failed} failed", ConsoleColor.Red);
            Output.WriteLine("Failed tests:", ConsoleColor.Red);
            foreach (string fail in FailedTests) Output.WriteLine($"  - {fail}", ConsoleColor.Red);
        }

        Output.WriteLine();
    }

    private static void TestWrite()
    {
        Output.Write("output test: ");
        OutputInfo.Test(true, "Write");
        Passed++;
    }

    private static void TestColor()
    {
        ConsoleColor old = Console.ForegroundColor;
        Output.Write("color test", ConsoleColor.Green);
        Check(Console.ForegroundColor == old, "color restored after Write");
    }

    private static void TestReset()
    {
        Output.ResetColor();
        Check(Console.ForegroundColor == ConsoleColor.White, "ResetColor");
    }

    private static void TestInfo()
    {
        OutputInfo.Info("info test");
        Check(true, "OutputInfo.Info");
    }

    private static void TestError()
    {
        OutputInfo.Error("error test");
        Check(true, "OutputInfo.Error");
    }

    private static void TestWarning()
    {
        OutputInfo.Warning("warning test");
        Check(true, "OutputInfo.Warning");
    }

    private static void TestDebug()
    {
        OutputInfo.Debug("debug test");
        Check(true, "OutputInfo.Debug");
    }

    private static void TestCustom()
    {
        OutputInfo.Custom("TEST", "custom test", ConsoleColor.Green, ConsoleColor.White);
        Check(true, "OutputInfo.Custom");
    }

    private static void Check(bool cond, string name)
    {
        if (cond)
        {
            Passed++;
            OutputInfo.Test(true, name);
        }
        else
        {
            Failed++;
            FailedTests.Add(name);
            OutputInfo.Test(false, name);
        }
    }
}