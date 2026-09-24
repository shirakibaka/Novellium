# Novellium OS

Novellium is an x86_64 operating system built on Cosmos Gen3 (.NET NativeAOT) using the Limine bootloader. It is written in a procedural C-like style with minimal OOP overhead, featuring preemptive multi-threading, an Ext2 virtual filesystem, an autonomous logging daemon (`syslogd`), an interactive shell (`novsh`), and 21 standard CLI utilities.

## Architecture

- **Kernel Entry (`Kernel.cs`, `src/System/Init.cs`):** Subsystem initialization, Ext2 mounting, MOTD display, and shell startup.
- **Process Management (`src/Process/Manager.cs`):** Preemptive multi-threading, process table tracking (`Created`, `Running`, `Terminated`, `Zombie`, `Failed`), signal handling (`KillReq`), orphan reparenting to PID 1, and zombie reaping.
- **Filesystem (`ext2`):** Ext2 driver mounted at `/` with standard hierarchy (`/bin`, `/etc`, `/home/user`, `/var/log`, `/tmp`, `/dev`, `/proc`). System configurations in `/etc/` (`hostname`, `version`, `motd`, `os-release`). Filesystem initialization is idempotent across boots.
- **Syslog Daemon (`src/Services/Syslogd.cs`):** Background logging service flushing to `/var/log/syslog` with a 200-entry in-memory ring buffer accessible via `dmesg`.
- **Shell (`src/Commands/Novsh.cs`, `src/Commands/JManager.cs`):** Interactive prompt (`novellium:/path$ `), foreground/background (`&`) execution, job tracking (`jobs`), and path resolution (`.`, `..`).

## Commands

All commands are implemented in `src/Commands/` and support `-h` / `--help`:

| Command | Description | Key Options |
|---|---|---|
| `ls` | List directory contents | `-a`, `-l`, `-1` |
| `cd` | Change working directory | Relative and absolute paths |
| `pwd` | Print working directory | |
| `cat` | Concatenate and display files | `-n` (line numbers) |
| `touch` | Create empty files | |
| `mkdir` | Create directories | `-p` (parents) |
| `rm` | Remove files | |
| `rmdir` | Remove empty directories | `-p` (parents) |
| `stat` | Display file / directory status | |
| `df` | Disk space usage | `-h`, `-k`, `-m` |
| `free` | Memory usage | `-h`, `-m`, `-k`, `-b` |
| `uname` | Operating system information | `-a`, `-s`, `-r`, `-v`, `-m` |
| `uptime` | System uptime | `-p`, `-s` |
| `dmesg` | Kernel log buffer | `-c` (clear), `-l <level>`, `-n <count>` |
| `ps` | Process table | |
| `jobs` | Background jobs | |
| `kill` | Send termination signal | `-s <signum>`, `-9` |
| `wait` | Wait for process or job | `-t <timeout>`, `-n` |
| `sleep` | Delay execution | Seconds |
| `clear` | Clear terminal screen | |
| `help` | Command reference | `[command]` |
| `test` | Automated test suites | `[all\|process\|command\|system\|ext2\|output]` |

## Pre-built Images & Testing

For immediate testing without building from source:
- **Bootable ISO:** [`dist/Novellium.iso`](file:///home/maidochka/Code/cosmos/Novellium/dist/Novellium.iso) (8.9 MiB).
- **Storage Disk Image:** [`disk.img`](file:///home/maidochka/Code/cosmos/Novellium/disk.img) (512 MiB raw Ext2 disk formatted for AHCI SATA) located in the project root directory alongside `Kernel.cs`.
  - When cloning from Git, unpack the compressed image:
    ```bash
    gzip -d -k disk.img.gz
    ```

## Build & Run

### Prerequisites
- .NET 10.0 SDK
- Cosmos SDK 3.0.88
- Clang / LLVM & `xorriso`

### Build
```bash
PATH="$HOME/.dotnet/tools:$PATH" dotnet build
```
Output ISO: `bin/Debug/net10.0/linux-x64/cosmos/Novellium.iso`

### Run (QEMU)

Launch Novellium with the AHCI SATA storage controller, the 512 MiB disk image, and the bootable ISO:

```bash
qemu-system-x86_64 \
    -enable-kvm \
    -cpu host \
    -smp 2 \
    -m 512M \
    -vga virtio \
    -display sdl,gl=on \
    -device ahci,id=ahci \
    -drive id=disk,file=disk.img,format=raw,if=none \
    -device ide-hd,drive=disk,bus=ahci.0 \
    -cdrom dist/Novellium.iso \
    -boot d
```

> **Display Fallback:** If SDL or OpenGL is not installed on your host, use `-display gtk` or omit the `-display` option.
>
> **Self-Built ISO:** Replace `-cdrom dist/Novellium.iso` with `-cdrom bin/Debug/net10.0/linux-x64/cosmos/Novellium.iso`.
