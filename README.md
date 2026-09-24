# Novellium OS

**Novellium** is a lightweight, Unix-inspired 64-bit operating system developed in C# on top of **Cosmos Gen3** (C# Open Source Managed Operating System) and compiled natively to bare-metal machine code using **.NET NativeAOT** with the **Limine** bootloader.

Novellium combines the memory safety and expressiveness of modern C# with a compact, procedural C-like systems architecture, providing a full Unix-style userland, preemptive multi-threading, an Ext2 virtual filesystem, background daemons, and an interactive shell.

---

## Features

### 1. Process & Thread Management (`PManager`)
- **Preemptive Multi-threading:** Thread-backed process abstraction integrated with the Cosmos scheduler.
- **POSIX-like Process States:** `Created`, `Running`, `Terminated`, `Zombie`, `Failed`.
- **Process Hierarchy & Adoption:** Full parent-child tracking. When a parent terminates, child processes are safely adopted by the kernel process (`PID 1`).
- **Signal & Exit Handling:** Process termination requests (`KillReq` / SIGKILL 137), exit code propagation.
- **Synchronous & Asynchronous Waiting:** `PManager.Wait` with timeout and blocking modes, non-blocking reaping, and automatic background orphan reaping.

### 2. Filesystem & VFS (`ext2`)
- **Ext2 Storage Driver:** Full Ext2 driver support registered with Cosmos VFS.
- **Standard Unix Directory Layout:**
  ```text
  /
  ├── bin/
  ├── dev/
  ├── etc/
  │   ├── hostname
  │   ├── motd
  │   ├── os-release
  │   └── version
  ├── home/
  │   └── user/
  ├── proc/
  ├── root/
  ├── tmp/
  ├── usr/
  └── var/
      └── log/
          └── syslog
  ```
- **Boot Idempotence:** Detects previously initialized root filesystem structures across reboots and avoids redundant format or directory recreation.

### 3. System Logging Daemon (`syslogd`)
- **Background Daemon:** Runs as an autonomous system process logging to `/var/log/syslog`.
- **In-Memory Ring Buffer:** Maintains the latest 200 log entries for instant inspection via `dmesg`.
- **Log Levels:** `DEBUG`, `INFO`, `OK`, `WARN`, `ERROR`.
- **Diagnostic Formatting:** Standardized timestamp, facility, and severity tags.

### 4. Interactive Shell (`novsh`) & Job Control
- **Prompt:** Dynamic prompt indicating current directory: `novellium:/path$ `.
- **Command Dispatcher:** Procedural C-style jump table with normalized absolute and relative path resolution (`.`, `..`).
- **Background Execution:** Launch commands in the background with trailing `&` (e.g., `sleep 10 &`).
- **Job Manager (`JManager`):** Tracks background jobs, assigns job numbers (`[1] PID`), and notifies the shell upon completion.

### 5. Standard Utilities (21 Commands)
Every command is isolated in its own file under `src/Commands/` and supports `--help` / `-h`:

| Command | Description | Supported Flags |
|---|---|---|
| `ls` | List directory contents | `-a`, `--all`, `-l`, `-1`, `-h`, `--help` |
| `cd` | Change current working directory | Path resolution, `..`, `.`, `-h` |
| `pwd` | Print working directory | `-h`, `--help` |
| `cat` | Concatenate and display file content | `-n`, `--number`, `-h`, `--help` |
| `touch`| Create empty files or touch timestamps | `-h`, `--help` |
| `mkdir`| Create directories | `-p`, `--parents`, `-h`, `--help` |
| `rm` | Remove files | `-h`, `--help` |
| `rmdir`| Remove empty directories | `-p`, `--parents`, `-h`, `--help` |
| `stat` | Display file or directory status | File size, mode, dates, `-h`, `--help` |
| `df` | Report filesystem disk space usage | `-h`, `-k`, `-m`, `-h`, `--help` |
| `free` | Display system RAM and swap usage | `-b`, `-k`, `-m`, `-h`, `--help` |
| `uname`| Print system architecture and kernel info | `-a`, `-s`, `-n`, `-r`, `-v`, `-m`, `-o` |
| `uptime`| Show system uptime and load | `-p`, `-s`, `-h`, `--help` |
| `dmesg`| View or clear kernel syslog ring buffer | `-c`, `-l <level>`, `-n <count>`, `-h` |
| `ps` | List active processes and states | `-h`, `--help` |
| `jobs` | List active background jobs | `-h`, `--help` |
| `kill` | Send termination signal to process | `-s <signum>`, `-9`, `-h`, `--help` |
| `wait` | Wait for background PID or job | `-t <timeout>`, `-n`, `-h`, `--help` |
| `sleep`| Delay execution for specified seconds | `-h`, `--help` |
| `clear`| Clear the terminal screen | `-h`, `--help` |
| `help` | Display general or per-command help | `[command]`, `-h`, `--help` |
| `test` | Run integrated test suites | `[all\|process\|command\|system\|ext2\|output]` |

### 6. Automated Verification Suites (`src/Tests/`)
Novellium includes comprehensive self-tests running at boot or on-demand:
- **Comprehensive Command Tests:** Validates all 21 utilities, argument parsers, exit codes, and error conditions.
- **Process Tests:** Validates process creation, PID reuse, state transitions, orphan adoption by PID 1, and zombie reaping.
- **System & VFS Tests:** Validates root directory structure, file descriptors, and block devices.
- **Ext2 Driver Tests:** Formats and mounts virtual RAM block devices to test filesystem drivers in memory.
- **Failure Reporting:** If any test fails, a summary list of failed test names is printed at the end of the suite.

---

## Project Structure

```text
.
├── Bootloader/
│   └── limine.conf          # Limine bootloader configuration
├── Fonts/
│   └── zap-vga16.psf        # PSF console font (Zap VGA 16)
├── src/
│   ├── Commands/            # Shell utilities & command manager
│   │   ├── Cat.cs
│   │   ├── Cd.cs
│   │   ├── Clear.cs
│   │   ├── Df.cs
│   │   ├── Dmesg.cs
│   │   ├── Free.cs
│   │   ├── Help.cs
│   │   ├── JManager.cs      # Background job manager
│   │   ├── Jobs.cs
│   │   ├── Kill.cs
│   │   ├── Ls.cs
│   │   ├── Manager.cs       # CManager: command dispatcher
│   │   ├── Mkdir.cs
│   │   ├── Novsh.cs         # novsh interactive shell
│   │   ├── Ps.cs
│   │   ├── Pwd.cs
│   │   ├── Rm.cs
│   │   ├── Rmdir.cs
│   │   ├── Sleep.cs
│   │   ├── Stat.cs
│   │   ├── Test.cs
│   │   ├── Touch.cs
│   │   ├── Uname.cs
│   │   ├── Uptime.cs
│   │   └── Wait.cs
│   ├── IO/
│   │   └── Output.cs        # Thread-safe console & diagnostic output
│   ├── Process/
│   │   ├── Manager.cs       # PManager process supervisor
│   │   ├── Process.cs       # PInfo metadata struct
│   │   └── State.cs         # PState enum
│   ├── Services/
│   │   └── Syslogd.cs       # Syslog daemon service
│   ├── System/
│   │   └── Init.cs          # Kernel initialization & rootfs mounting
│   └── Tests/               # Automated test suites
│       ├── ComprehensiveCommandTests.cs
│       ├── CTest.cs
│       ├── Ext2Tests.cs
│       ├── OutputTest.cs
│       ├── PTest.cs
│       └── STest.cs
├── Kernel.cs                # Kernel lifecycle entry point
├── Novellium.csproj         # Cosmos SDK project file
└── NuGet.Config             # Package restore sources
```

---

## Building

### Prerequisites
- Linux (x86_64)
- .NET 10.0 SDK
- Cosmos SDK 3.0.88 (`Cosmos.Build.Patcher` / `Cosmos.Kernel`)
- LLVM / Clang
- `xorriso` (for Limine ISO creation)

### Static Build
Run the standard build command:
```bash
PATH="$HOME/.dotnet/tools:$PATH" dotnet build
```
This triggers:
1. Assembly patching via `Cosmos.Build.Patcher`.
2. Ahead-of-Time compilation via `ILCompiler` (NativeAOT).
3. Clang C/ASM compilation for low-level architecture bootstrap.
4. ELF linking into `Novellium.elf`.
5. Generation of the hybrid bootable ISO image:
   `bin/Debug/net10.0/linux-x64/cosmos/Novellium.iso`

---

## Running in QEMU

Because Cosmos uses a high-resolution graphical framebuffer (VESA/GOP) with software glyph rendering and page scrolling, hardware virtualization and an accelerated display adapter are highly recommended for smooth interaction.

### Recommended Launch Command:
```bash
qemu-system-x86_64 \
    -enable-kvm \
    -cpu host \
    -smp 2 \
    -m 512M \
    -vga virtio \
    -display sdl,gl=on \
    -cdrom bin/Debug/net10.0/linux-x64/cosmos/Novellium.iso
```

### Options Explanation:
- `-enable-kvm -cpu host`: Enables native hardware CPU acceleration (eliminates software emulation latency).
- `-smp 2`: Multi-core virtual CPU for preemptive thread scheduling and background daemons.
- `-m 512M`: Allocates 512 MiB of RAM.
- `-vga virtio`: High-throughput VirtIO GPU memory mapping.
- `-display sdl,gl=on` (or `-display gtk,gl=on`): GPU-accelerated window presentation.
- `-cdrom ...`: Points to the generated Limine boot ISO.

---

## License

This project is licensed under the MIT License.
