// Novsh.cs — novsh interactive shell command line interpreter
using System;
using System.Text;
using System.Threading;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Novsh
{
    public static void Run(int pid, string[] args)
    {
        Output.WriteLine($"[NOVSH] PID: {pid}");

        while (!PManager.IsKillReq(pid))
        {
            string? input = ReadLine(pid);
            if (input == null || PManager.IsKillReq(pid)) break;
            if (string.IsNullOrWhiteSpace(input)) continue;

            int child = CManager.Execute(input, pid, out bool bg);
            if (child <= 0) continue;

            if (bg)
            {
                PInfo? p = PManager.Get(child);
                if (p != null) JManager.Add(child, p.Value.Name);
                continue;
            }

            PManager.Wait(pid, child, out _);
        }

        Output.WriteLine("[NOVSH] terminated");
    }

    private static string? ReadLine(int pid)
    {
        JManager.Update(pid);

        Output.Write("\nnovellium:", ConsoleColor.Cyan);
        Output.Write(CManager.CurrentDirectory, ConsoleColor.White);
        Output.Write("$ ", ConsoleColor.Gray);

        StringBuilder sb = new();
        while (!PManager.IsKillReq(pid))
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Enter)
                {
                    Output.WriteLine();
                    return sb.ToString();
                }

                if (k.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        Output.Write("\b \b");
                    }
                    continue;
                }

                if (k.KeyChar != '\0' && !char.IsControl(k.KeyChar))
                {
                    sb.Append(k.KeyChar);
                    Output.Write(k.KeyChar.ToString());
                }
            }
            else
            {
                if (JManager.Update(pid) > 0)
                {
                    Output.Write("\nnovellium$ ", ConsoleColor.Gray);
                    if (sb.Length > 0) Output.Write(sb.ToString());
                }
                Thread.Sleep(20);
            }
        }
        return null;
    }
}