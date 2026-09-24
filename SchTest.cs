// SchTest.cs — Cosmos multi-threaded task scheduler test
using System;
using System.Threading;

namespace Novellium;

public static class SchedulerTest
{
    public static void Run()
    {
        Console.WriteLine("Starting scheduler test...");
        Thread a = new(() => { for (int i = 0; i < 20; i++) { Console.WriteLine($"[A] {i}"); Thread.Sleep(50); } });
        Thread b = new(() => { for (int i = 0; i < 20; i++) { Console.WriteLine($"[B] {i}"); Thread.Sleep(50); } });
        a.Start();
        b.Start();
        Console.WriteLine("Threads started.");
    }
}