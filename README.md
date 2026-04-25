<h1> <img src="https://raw.githubusercontent.com/LiveSplit/LiveSplit/master/res/Icon.svg" alt="LiveSplit" height="42" align="top"/> LiveSplit</h1>

[![GitHub release](https://img.shields.io/github/release/LiveSplit/LiveSplit.svg)](https://github.com/LiveSplit/LiveSplit/releases/latest)
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/LiveSplit/LiveSplit/master/LICENSE)
[![Build Status](https://github.com/LiveSplit/LiveSplit/workflows/CI/badge.svg)](https://github.com/LiveSplit/LiveSplit/actions)
[![GitHub issues](https://img.shields.io/github/issues/LiveSplit/LiveSplit.svg?style=plastic)](https://github.com/LiveSplit/LiveSplit/issues)

LiveSplit is a timer program for speedrunners that is both easy to use and full of features.
<p align="center">
  <img src="https://raw.githubusercontent.com/LiveSplit/LiveSplit.github.io/master/images/livesplittimer.png" alt="LiveSplit"/>
</p>

## Features

**Speedrun.com Integration:** [Speedrun.com](http://speedrun.com) is integrated into LiveSplit. You can browse their leaderboards and download splits directly from LiveSplit. You can also show the World Records for the games you run with the World Record Component.

**Accurate Timing:** LiveSplit automatically synchronizes with an atomic clock over the Internet to estimate inaccuracies of the local timer in the PC. LiveSplit's timer automatically adjusts the local timer to fix those inaccuracies.

**Game Time and Auto Splitting:** LiveSplit will automatically detect if Game Time and/or Auto Splitting is available for a game and let you activate it in the Splits Editor. Game Time is automatically read directly from an emulator or PC game, and you can use it by switching to Game Time under Compare Against.

**Video Component:** With the Video Component, you can play a video from a local file alongside your run. The video will start when you start your run and stop whenever you reset. You can also specify at what point the video should start at.

**Comparisons:** In LiveSplit, you are able to dynamically switch between multiple comparisons, even mid-run. You can either compare your run to comparisons that you define yourself or compare it to multiple automatically generated comparisons, like your Sum of Best Segments or your average run.

**Layout System:** Users can modify every part of LiveSplit’s appearance using Layouts. Every user has the ability to add or remove parts along with being able to rearrange and customize each part of LiveSplit. You can even use your own background images.

**Dynamic Resizing:** LiveSplit can be resized to any size so that it looks good on stream. As LiveSplit’s size is changed, all of its parts are automatically scaled up in order to preserve its appearance.

**Sharing Runs:** Any run can be shared via a tweet on [X (Twitter)](https://twitter.com/) or as a screenshot uploaded to [Imgur](http://imgur.com/) or saved as a file.

**Component Development:** Anyone can develop their own components that can easily be shared and used with LiveSplit. Additional downloadable components can be found in the [Components Section](https://livesplit.org/components/).

## Contributing

We need your help!

You can browse the [Issues](https://github.com/LiveSplit/LiveSplit/issues) to find good issues to get started with. Select one that is not already done or in progress, assign yourself, and drag it over to "In Progress".

 1. [Fork](https://github.com/LiveSplit/LiveSplit/fork) the project
 2. Clone your forked repo: `git clone --recursive https://github.com/YourUsername/LiveSplit.git`
 3. Create your feature/bugfix branch: `git checkout -b new-feature`
 4. Commit your changes to your new branch: `git commit -am 'Add a new feature'`
 5. Push to the branch: `git push origin new-feature`
 6. Create a new Pull Request!

## Building on Linux

The instructions above produce a Windows binary (`net8.0-windows`). A Linux port using an Avalonia front-end and a SkiaSharp renderer lives on the `linux-port` branch. The notes below cover building it on any distro and packaging it as a tarball, AppImage, or — recommended for distribution — a Flatpak.

### Prerequisites (all distros)

You need three toolchains on the build host (or, for the Flatpak path, only `flatpak-builder` — see below):

1. **.NET 8 SDK** — install from your distro's package manager (`dotnet-sdk-8.0` on most), Microsoft's repo, or <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>. Verify with `dotnet --version`; it must report `8.x`.
2. **Rust toolchain** — install via [rustup](https://rustup.rs/) and add the target you want to publish for:
   ```sh
   curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
   rustup target add x86_64-unknown-linux-gnu        # required for linux-x64
   rustup target add aarch64-unknown-linux-gnu       # optional, for linux-arm64
   rustup target add i686-unknown-linux-gnu          # optional, for linux-x86
   ```
   Rust is required because LiveSplit's timing and splits-file core (`livesplit-core`) is a Rust crate compiled to a native shared library (`liblivesplit_core.so`) that the .NET side `P/Invoke`s into.
3. **Build essentials** — a C toolchain, `pkg-config`, `git`, and `curl`. Some Rust dependencies and `linuxdeploy` (for AppImages) need them.
   - Debian/Ubuntu: `sudo apt install build-essential pkg-config git curl`
   - Fedora/RHEL: `sudo dnf install @development-tools pkgconf-pkg-config git curl`
   - Arch: `sudo pacman -S base-devel pkgconf git curl`

For an AppImage build, also grab [linuxdeploy](https://github.com/linuxdeploy/linuxdeploy/releases) and put it on `PATH`.

### Step 1 — Clone with submodules

```sh
git clone --recursive -b linux-port https://github.com/LiveSplit/LiveSplit.git
cd LiveSplit
```

If you've already cloned without `--recursive`:

```sh
git submodule update --init --recursive
```

The `lib/livesplit-core` submodule contains the Rust crate; the build will fail without it.

### Step 2 — Build the native shared libraries

LiveSplit `P/Invoke`s into two Rust-built shared libraries:

- `liblivesplit_core.so` — the timing/splits-file engine, sourced from the `lib/livesplit-core` submodule.
- `libasr_capi.so` — the auto-splitting runtime, sourced from [components/LiveSplit.AutoSplittingRuntime/src/asr-capi](components/LiveSplit.AutoSplittingRuntime/src/asr-capi). Without it, LiveSplit still runs but auto-splitters won't load. 64-bit only.

The helper script [scripts/build-native-linux.sh](scripts/build-native-linux.sh) builds both with `cargo build --release` and copies the resulting `.so` files into the `runtimes/<rid>/native/` layout the .NET `NativeLibraryResolver` looks for at startup:

```sh
scripts/build-native-linux.sh                     # default: linux-x64
scripts/build-native-linux.sh linux-arm64
scripts/build-native-linux.sh linux-x86           # skips asr_capi (64-bit only)
```

Run it once per RID you plan to publish for.

### Step 3 — Build the .NET solution

For day-to-day development:

```sh
dotnet build LiveSplit.sln -c Release
```

Output lands under `artifacts/bin/LiveSplit/release/`. Run it with:

```sh
dotnet artifacts/bin/LiveSplit/release/LiveSplit.dll
```

To produce a self-contained build that doesn't require the host to have .NET installed, use the packaging script described next.

### Packaging: tarball and AppImage

[scripts/package-linux.sh](scripts/package-linux.sh) wraps the whole pipeline. It invokes `build-native-linux.sh` for you, runs `dotnet publish --self-contained` for the requested RID, drops a tarball into `dist/`, and optionally produces an AppImage:

```sh
scripts/package-linux.sh                       # → dist/livesplit-linux-x64.tar.gz
scripts/package-linux.sh --appimage            # → also dist/LiveSplit-x86_64.AppImage
scripts/package-linux.sh --rid linux-arm64     # cross-RID build
```

`--appimage` requires `linuxdeploy` on `PATH`; the script generates a minimal `.desktop` entry and bundles `res/Icon.png` if it exists.

### Packaging: Flatpak (recommended for distribution)

Flatpak is the preferred way to distribute the Linux build because the runtime sandbox abstracts away each distro's `glibc`, `libstdc++`, and graphics-stack versions, giving you one artifact that works everywhere. Going this route, you do **not** need .NET or Rust on the host — `flatpak-builder` is the only requirement, since the manifest at [org.livesplit.LiveSplit.yml](org.livesplit.LiveSplit.yml) runs the whole build pipeline (native libs + `dotnet publish`) inside the sandbox using the `dotnet8` and `rust-stable` SDK extensions.

#### One-time setup

```sh
# Install the builder (use your distro's equivalent of apt if needed).
sudo apt install flatpak flatpak-builder

# Add Flathub.
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

# Pull down the runtime + the .NET 8 and Rust SDK extensions used at build time.
flatpak install --user flathub \
    org.freedesktop.Platform//23.08 \
    org.freedesktop.Sdk//23.08 \
    org.freedesktop.Sdk.Extension.dotnet8//23.08 \
    org.freedesktop.Sdk.Extension.rust-stable//23.08
```

#### Build

```sh
scripts/package-linux.sh --flatpak
```

This produces a redistributable `dist/livesplit.flatpak` bundle. Install it locally with:

```sh
flatpak install --user dist/livesplit.flatpak
flatpak run org.livesplit.LiveSplit
```

The bundle installs on any distro with `flatpak install ./livesplit.flatpak`.

Note: the manifest sets `--share=network` during the build phase so cargo and `dotnet restore` can fetch packages — fine for personal use, but Flathub doesn't allow it. Submitting upstream needs NuGet packages and Cargo crates vendored ahead of time and listed under `sources:` in the manifest.

#### Autosplitter support

Autosplitters work inside the Flatpak sandbox out of the box, but most distros also enforce a kernel-side gate (yama LSM) that Flatpak permissions can't override.
Ubuntu and Fedora for example default to `kernel.yama.ptrace_scope = 1`, which only permits parent→child ptrace. Steam launches the game, not LiveSplit, so they're siblings and reads silently fail with EPERM. Drop `ptrace_scope` to `0` to fix it:

   ```sh
   # one-shot (resets on reboot):
   sudo sysctl kernel.yama.ptrace_scope=0

   # permanent: install the drop-in shipped with the source tree
   sudo cp scripts/99-livesplit-ptrace.conf /etc/sysctl.d/
   sudo sysctl --system
   ```

   Arch and most rolling distros already default to `0`, so no action needed there. SteamOS-3 (Deck) likewise defaults to `0`.

Once both are in place, the modern WASM autosplitter runtime (`livesplit_auto_splitting`) detects Wine processes, exposes the PE name to the splitter, and reads the game's address space exactly as it would natively. Old `.asl` scripts work too for the read-only memory case; scripts that inject code (`WriteDetour` etc.) are not supported on Linux regardless of permissions.

## Common Compiling Issues
1. No submodules pulled in when you fork/clone the repo which causes the project not to build. There are two ways to remedy this:
 - Cloning for the first time: `git clone --recursive https://github.com/LiveSplit/LiveSplit.git`
 - If already cloned, execute this in the root directory: `git submodule update --init --recursive`

## Auto Splitters

The documentation for how to develop, test, and submit an Auto Splitter can be found here:

[Auto Splitters Documentation](https://github.com/LiveSplit/LiveSplit.AutoSplitters/blob/master/README.md)

### Writing ASL scripts that work under Wine/Proton

When LiveSplit runs on Linux against a Wine'd Windows game, .NET's stock `Process.GetProcessesByName` matches against `/proc/<pid>/comm` (capped at 15 chars), and Wine's choice of comm string varies by version — sometimes `Game.exe`, sometimes `Game`, sometimes truncated. To paper over that, the Linux port ships [WineAwareProcess](src/LiveSplit.Core/ComponentUtil/WineAwareProcess.cs), a wrapper that tries the bare name, the `.exe`-suffixed name, then walks `/proc/*/maps` looking for the loaded PE.

You don't need to do anything for the **primary** game-attach lookup — the `state("ProcessName") { … }` block at the top of every ASL routes through `WineAwareProcess` automatically. The case that matters for script authors is **auxiliary** process probes inside `update {}` / `start {}` / etc. blocks, which compile against `System.Diagnostics.Process` directly:

```csharp
// Doesn't reliably find the Wine'd helper on Linux:
vars.companion = Process.GetProcessesByName("OtherProcess");

// Reliable on Windows and on Linux/Wine:
vars.companion = WineAwareProcess.GetProcessesByName("OtherProcess");
```

The `LiveSplit.ComponentUtil` namespace is already imported into the ASL compilation context, so `WineAwareProcess` is in scope without any extra `using` directive in your script. On Windows the wrapper is a thin pass-through, so swapping the call is safe for cross-platform splitters.

## The LiveSplit Server

The internal LiveSplit Server allows for other programs and other computers to control LiveSplit. The server can accept connections over either a named pipe located at `\\<hostname>\pipe\LiveSplit` (`.` is the hostname if the client and server are on the same computer), raw TCP/IP, or a WebSocket (WS) server, located at `ws://<hostname>:port/livesplit`.

### Control

The named pipe is always open while LiveSplit is running but the TCP and WS servers **MUST** be started before programs can talk to them (Right click on LiveSplit -> Control -> Start TCP/WS Server). You **MUST** manually start the one you wish to use each time you launch LiveSplit. The TCP and WS servers cannot both run at the same time because the WS server runs on top of TCP/IP.

### Settings

#### Server Port

**Server Port** is the door (one of thousands) on your computer that this program sends data through. Default is 16834. This should be fine for most people, but depending on network configurations, some ports may be blocked. See also https://en.wikipedia.org/wiki/Port_%28computer_networking%29.

#### Startup Behavior

**Startup Behavior** defines whether and which type of Server should be started automatically at the launch of LiveSplit.

### Known Uses

- **Android LiveSplit Remote**: https://github.com/Ekelbatzen/LiveSplit.Remote.Android
- **SplitNotes**: https://github.com/joelnir/SplitNotes
- **Autosplitter Remote Client**: https://github.com/RavenX8/LiveSplit.Server.Client

Made something cool? Consider getting it added to this list.

### Commands

Commands are case sensitive and end with a new line. You can provide parameters by using a space after the command and sending the parameters afterwards (`<command><space><parameters><newline>`).

Some commands will respond with data and some will not. Every response ends with a newline character. Note that since the WS server has a concept of messages, commands and reponses sent over it do not end in newline characters.

All times and deltas returned by the server are formatted according to [C#'s Constant Format Specifier](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-timespan-format-strings#the-constant-c-format-specifier). The server will accept times in the following format: `[-][[[d.]hh:]mm:]ss[.fffffff]`. The hours field can be greater than 23, even if days are present. Individual fields do not need to be padded with zeroes. Any command that returns a time or a string can return a single hyphen `-` to indicate a "null" or invalid value. Commands that take a COMPARISON or a NAME take plain strings that may include spaces. Because it is used as a delimiter to mark the end of a command, newline characters may not appear anywhere within a command.

Commands that generate no response:

- startorsplit
- split
- unsplit
- skipsplit
- pause
- resume
- reset
- starttimer
- setgametime TIME
- setloadingtimes TIME
- addloadingtimes TIME
- pausegametime
- unpausegametime
- alwayspausegametime
- setcomparison COMPARISON
- switchto realtime
- switchto gametime
- setsplitname INDEX NAME
- setcurrentsplitname NAME
- setcustomvariable JSON([NAME, VALUE])

Commands that return a time:

- getdelta
- getdelta COMPARISON
- getlastsplittime
- getcomparisonsplittime
- getcurrentrealtime
- getcurrentgametime
- getcurrenttime
- getfinaltime
- getfinaltime COMPARISON
- getpredictedtime COMPARISON
- getbestpossibletime

Commands that return an int:

- getsplitindex
(returns -1 if the timer is not running)
- getattemptcount
- getcompletedcount

Commands that return a string:

- getcurrentsplitname
- getprevioussplitname
- getcurrenttimerphase
- getcustomvariablevalue NAME
- ping
(always returns `pong`)

Commands are defined at `ProcessMessage` in "CommandServer.cs".

### Example Clients

#### Python

```python
import socket

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect(("localhost", 16834))
s.send(b"starttimer\n")
```

#### Java 7+

```java
import java.io.IOException;
import java.io.PrintWriter;
import java.net.Socket;

public class MainTest {
    public static void main(String[] args) throws IOException {
        Socket socket = new Socket("localhost", 16834);
        PrintWriter writer = new PrintWriter(socket.getOutputStream());
        writer.println("starttimer");
        writer.flush();
        socket.close();
    }
}
```

#### Lua
Software that implements [Lua](https://www.lua.org/) is usable for as a client. However, the lua io library must be available for the script to use, full documentation available [here](https://www.lua.org/manual/5.3/manual.html#6.8).

```lua
require "io"
self.LSEndpoint = "\\\\.\\pipe\\LiveSplit" --Localhost LiveSplit pipe.
self.LSPipe = io.open(self.LSEndpoint, "w") --Open/start the pipe. Flush is required after every command.
self.LSPipe:write "starttimer\n"
self.LSPipe:flush()
self.LSPipe:close() --This can be left open as needed.
```

#### Node.js

Node.js client implementation available here: https://github.com/satanch/node-livesplit-client

## Releasing

1. Update versions of any components that changed (create a Git tag and update the factory file for each component) to match the new LiveSplit version.
2. Create a Git tag for the new version.
3. Download `LiveSplit_Build` and `UpdateManagerExe` from the GitHub Actions build for the new Git tag.
4. Create a GitHub release for the new version, and upload the LiveSplit build ZIP file with the correct filename (e.g. `LiveSplit_1.8.21.zip`).
5. Modify files in [the update folder of LiveSplit.github.io](https://github.com/LiveSplit/LiveSplit.github.io/tree/master/update) and commit the changes:
    - Copy changed files from the downloaded LiveSplit build ZIP file to the [update folder](https://github.com/LiveSplit/LiveSplit.github.io/tree/master/update).
    - Copy changed files from the download Update Manager ZIP file to replace [`UpdateManagerV2.exe`](https://github.com/LiveSplit/LiveSplit.github.io/blob/master/update/UpdateManagerV2.exe) and [`UpdateManagerV2.exe.config`](https://github.com/LiveSplit/LiveSplit.github.io/blob/master/update/UpdateManagerV2.exe.config).
    - Add new versions to the update XMLs for (`update.xml`, `update.updater.xml`, and the update XMLs for any components that changed).
    - Modify the [DLL](https://github.com/therungg/LiveSplit.TheRun/blob/main/Components/LiveSplit.TheRun.dll) and [update XML](https://github.com/therungg/LiveSplit.TheRun/blob/main/update.LiveSplit.TheRun.xml) for LiveSplit.TheRun in its repo.
    - Update the version on the [downloads page](https://github.com/LiveSplit/LiveSplit.github.io/blob/master/downloads.md).

## License

The MIT License (MIT)

Copyright (c) 2013 Christopher Serr and Sergey Papushin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
