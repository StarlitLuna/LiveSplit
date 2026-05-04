<h1> <img src="https://raw.githubusercontent.com/LiveSplit/LiveSplit/master/res/Icon.svg" alt="LiveSplit" height="42" align="top"/> LiveSplit</h1>

[![GitHub release](https://img.shields.io/github/release/LiveSplit/LiveSplit.svg)](https://github.com/LiveSplit/LiveSplit/releases/latest)
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/LiveSplit/LiveSplit/master/LICENSE)
[![Build Status](https://github.com/LiveSplit/LiveSplit/workflows/Build%20Packages/badge.svg)](https://github.com/LiveSplit/LiveSplit/actions)
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
 2. Clone your forked repo: `git clone https://github.com/YourUsername/LiveSplit.git`
 3. Create your feature/bugfix branch: `git checkout -b new-feature`
 4. Commit your changes to your new branch: `git commit -am 'Add a new feature'`
 5. Push to the branch: `git push origin new-feature`
 6. Create a new Pull Request!

## Building on Windows

Windows builds use the same Avalonia app as Linux.

### Prerequisites

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). The Windows native libraries are vendored in this repository, so no Rust toolchain is needed for a normal Windows build.

### Build and test

```powershell
git clone -b linux-port https://github.com/LiveSplit/LiveSplit.git
cd LiveSplit

dotnet restore LiveSplit.sln
dotnet build LiveSplit.sln -c Release
dotnet test LiveSplit.sln -c Release --no-build
```

The app output is written to `bin\release`. Run it with:

```powershell
bin\release\LiveSplit.exe
```

### Publish a Windows package

```powershell
dotnet publish src\LiveSplit\LiveSplit.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -o dist\LiveSplit-win-x64
```

Zip `dist\LiveSplit-win-x64` if you need a redistributable archive.

## Building on Linux

Supported release artifacts are the Windows ZIP, Flatpak, Fedora RPM, and source builds for other Linux distributions.

### Source build prerequisites

You need three toolchains on the build host:

1. **.NET 8 SDK** - install from your distro's packages, Microsoft's repo, or <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>. Verify with `dotnet --version`; it must report `8.x`.
2. **Rust toolchain** - install `cargo` from your distro or [rustup](https://rustup.rs/). Add the GNU target when using rustup:
   ```sh
   rustup target add x86_64-unknown-linux-gnu
   ```
   Rust builds `liblivesplit_core.so` and `libasr_capi.so`, which the .NET app loads at runtime.
3. **Build essentials** - a C toolchain, `pkg-config`, `git`, and `curl`.
   - Fedora/RHEL: `sudo dnf install @development-tools dotnet-sdk-8.0 cargo pkgconf-pkg-config git curl vlc-libs vlc-plugin-ffmpeg`
   - Other distros: install equivalent .NET 8, Rust/Cargo, C compiler, `pkg-config`, `git`, `curl`, and LibVLC packages.

The Linux port builds from source vendored in this repository; no submodule checkout is required. The native Rust crates live in:

- `lib/livesplit-core/` for the timing/splits-file engine.
- [components/LiveSplit.AutoSplittingRuntime/src/asr-capi](components/LiveSplit.AutoSplittingRuntime/src/asr-capi) for the auto-splitting runtime C API.

### Build from source

```sh
git clone -b linux-port https://github.com/LiveSplit/LiveSplit.git
cd LiveSplit

bash scripts/build-native-linux.sh linux-x64
dotnet restore LiveSplit.sln
dotnet build LiveSplit.sln -c Release
dotnet test LiveSplit.sln -c Release --no-build
dotnet bin/release/LiveSplit.dll
```

### Packaging: Flatpak

Flatpak is the supported cross-distro Linux package. The manifest at [org.livesplit.LiveSplit.yml](org.livesplit.LiveSplit.yml) builds LibVLC, builds the native Rust libraries, and runs `dotnet publish` inside the Flatpak SDK using the `dotnet8` and `rust-stable` SDK extensions.

#### One-time setup

```sh
# Fedora example.
sudo dnf install flatpak flatpak-builder

flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

flatpak install --user flathub \
    org.freedesktop.Platform//24.08 \
    org.freedesktop.Sdk//24.08 \
    org.freedesktop.Sdk.Extension.dotnet8//24.08 \
    org.freedesktop.Sdk.Extension.rust-stable//24.08
```

#### Build

```sh
bash scripts/package-linux.sh
```

This produces `dist/LiveSplit.flatpak`. Install it locally with:

```sh
flatpak install --user dist/LiveSplit.flatpak
flatpak run org.livesplit.LiveSplit
```

Note: the manifest sets `--share=network` during the build phase so cargo and `dotnet restore` can fetch packages. Submitting to Flathub would require offline NuGet and Cargo source manifests under `sources:`.

### Packaging: Fedora RPM

Fedora RPM packaging is built natively on Fedora and targets `linux-x64`. The automated package workflow runs the RPM build inside the currently supported `fedora:latest` container rather than pinning to an end-of-life Fedora release.

```sh
sudo dnf install @development-tools dotnet-sdk-8.0 cargo rpm-build \
    rpmdevtools desktop-file-utils pkgconf-pkg-config git tar gzip \
    vlc-devel vlc-libs vlc-plugin-ffmpeg

bash scripts/package-fedora-rpm.sh
sudo dnf install dist/rpm/livesplit-*.rpm
livesplit
```

#### Autosplitter support

Autosplitters work inside the Flatpak sandbox out of the box, but most distros also enforce a kernel-side gate (yama LSM) that Flatpak permissions can't override.
Fedora and some other distributions default to `kernel.yama.ptrace_scope = 1`, which only permits parent-to-child ptrace. Steam launches the game, not LiveSplit, so they're siblings and reads silently fail with EPERM. Drop `ptrace_scope` to `0` to fix it:

   ```sh
   # one-shot (resets on reboot):
   sudo sysctl kernel.yama.ptrace_scope=0

   # permanent: install the drop-in shipped with the source tree
   sudo cp scripts/99-livesplit-ptrace.conf /etc/sysctl.d/
   sudo sysctl --system
   ```

   Arch and most rolling distros already default to `0`, so no action needed there. SteamOS-3 (Deck) likewise defaults to `0`.

Once both are in place, the modern WASM autosplitter runtime (`livesplit_auto_splitting`) detects Wine processes, exposes the PE name to the splitter, and reads the game's address space exactly as it would natively. Old `.asl` scripts work too for the read-only memory case; scripts that inject code (`WriteDetour` etc.) are not supported on Linux regardless of permissions.

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

The internal LiveSplit Server allows other programs and other computers to control LiveSplit. Cross-platform clients should use raw TCP/IP or WebSocket. The TCP server listens on the configured port, and the WebSocket server is located at `ws://<hostname>:port/livesplit`.

Windows builds also expose a named pipe at `\\<hostname>\pipe\LiveSplit` (`.` is the hostname if the client and server are on the same computer). That path syntax is Windows-specific and is not available to Linux clients. Linux clients should connect through TCP or WebSocket instead.

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
Software that implements [Lua](https://www.lua.org/) is usable as a client. For cross-platform scripts, prefer a TCP or WebSocket Lua library. The lua io example below uses the Windows named pipe and only applies on Windows.

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

1. Update versions of any components that changed to match the new LiveSplit version.
2. Create a Git tag for the new version.
3. Build and verify the release artifacts:
   - Windows ZIP from the `win-x64` publish output.
   - Flatpak from `scripts/package-linux.sh`.
   - Fedora RPM from `scripts/package-fedora-rpm.sh`.
   - Source build instructions for other Linux distributions.
4. Create a GitHub release for the new version and upload the Windows ZIP, Flatpak, Fedora RPM, and source archive.
5. Update the downloads page and any update metadata that applies to the Windows ZIP.

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
