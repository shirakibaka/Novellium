// Stat.cs — stat command: display detailed file or directory status
using System;
using System.Collections.Generic;
using Cosmos.Kernel.HAL.Vfs;
using Cosmos.Kernel.System.Vfs;
using Novellium.IO;
using Novellium.Process;

namespace Novellium.Commands;

public static class Stat
{
    public static void Run(int pid, string[] args)
    {
        var targets = new List<string>();

        for (int i = 1; i < args.Length; i++)
        {
            string a = args[i];
            if (a.StartsWith('-') && a.Length > 1)
            {
                Output.WriteLine($"stat: invalid option -- '{a}'", ConsoleColor.Red);
                Output.WriteLine("Try 'stat --help' for more information.", ConsoleColor.Gray);
                PManager.Exit(pid, 1);
                return;
            }
            targets.Add(a);
        }

        if (targets.Count == 0)
        {
            Output.WriteLine("usage: stat [OPTION]... FILE...", ConsoleColor.Yellow);
            Output.WriteLine("Try 'stat --help' for more information.", ConsoleColor.Gray);
            PManager.Exit(pid, 1);
            return;
        }

        bool hasError = false;
        foreach (string target in targets)
        {
            string path = CManager.ResolvePath(target);
            if (!VfsManager.TryStat(path, out VfsStat st))
            {
                Output.WriteLine($"stat: cannot stat '{target}': No such file or directory", ConsoleColor.Red);
                hasError = true;
                continue;
            }

            string type = st.IsDirectory ? "directory" : (st.IsSymbolicLink ? "symbolic link" : "regular file");
            string mode = Ls.FmtMode(st);
            int octal = (int)st.Mode & 0x1FF;

            Output.WriteLine($"  File: {target}", ConsoleColor.White);
            Output.WriteLine($"  Size: {st.Size,-10} Blocks: {st.Blocks,-8} IO Block: {st.BlkSize,-8} {type}", ConsoleColor.Gray);
            Output.WriteLine($"Device: ext2       Inode: {st.Ino,-9} Links: {st.NLink}", ConsoleColor.Gray);
            Output.WriteLine($"Access: (0{Convert.ToString(octal, 8)}/{mode})  Uid: ({st.Uid})   Gid: ({st.Gid})", ConsoleColor.Gray);
            Output.WriteLine($"Access: {st.Atime.TvSec} (epoch)", ConsoleColor.DarkGray);
            Output.WriteLine($"Modify: {st.Mtime.TvSec} (epoch)", ConsoleColor.DarkGray);
            Output.WriteLine($"Change: {st.Ctime.TvSec} (epoch)", ConsoleColor.DarkGray);
        }

        if (hasError) PManager.Exit(pid, 1);
    }

    public static void Help()
    {
        Output.WriteLine("Usage: stat [OPTION]... FILE...", ConsoleColor.White);
        Output.WriteLine("Display file or file system status.", ConsoleColor.Gray);
        Output.WriteLine();
        Output.WriteLine("Options:", ConsoleColor.White);
        Output.WriteLine("  -h, --help    display this help and exit", ConsoleColor.Gray);
    }
}
