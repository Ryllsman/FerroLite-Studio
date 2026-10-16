# Building FerroLite Studio

FerroLite Studio builds from source with the **.NET SDK 8.0 or newer** (the
projects target `net8.0-windows`; `global.json` sets 8.0 as the floor). No other
tooling is required to build or test the application. (The Windows installer additionally
needs [NSIS](https://nsis.sourceforge.io/) — see the end of this file.)

## Prerequisites

- **Windows 10 or 11, x64**
- **.NET SDK 8.0 or newer** — <https://dotnet.microsoft.com/download/dotnet/8.0>

Verify with:

```powershell
dotnet --version      # 8.0.100 or higher
```

An older SDK is rejected with a clear message rather than a confusing build
error: `global.json` requires at least 8.0.100 and allows any newer version to
roll forward.

## Build and test

From the folder containing `FerroliteStudio.slnx`:

```powershell
dotnet build FerroliteStudio.slnx
dotnet test  Ferrolite.Core.Tests
dotnet run   --project Ferrolite.UI
```

Expected results:

- `dotnet build` — **0 warnings, 0 errors**
- `dotnet test` — **157 tests passed**
- `dotnet run --project Ferrolite.UI` — the application window opens

### The hardware-tagged tests

A few tests capture the real desktop, query the real cursor position, or push an
event through the real input pipeline. They are marked `Category=Hardware`:

```powershell
dotnet test Ferrolite.Core.Tests                                # all 157
dotnet test Ferrolite.Core.Tests --filter "Category!=Hardware"  # 153 — what CI runs
dotnet test Ferrolite.Core.Tests --filter "Category=Hardware"   # the 4 display/input tests
```

CI runs the `!=Hardware` filter deliberately. Those four tests are meaningful on
a real desktop but, on a headless runner, they measure the runner rather than the
code — and a red badge caused by "no display attached" would make the badge
useless as evidence. If you are debugging a capture or input problem, run the
`=Hardware` filter by hand.

`FerroliteStudio.slnx` is the XML solution format, which needs **Visual Studio
17.13 or newer** or the `dotnet` CLI. On older tooling, open the three project
files individually: `Ferrolite.Core/Ferrolite.Core.csproj`,
`Ferrolite.UI/Ferrolite.UI.csproj`, `Ferrolite.Core.Tests/Ferrolite.Core.Tests.csproj`.

## Package versions are pinned

Every project carries a committed `packages.lock.json` recording the exact
version and content hash of every direct and transitive NuGet package. That is
why a fresh clone resolves the same packages as CI's:

```powershell
dotnet restore FerroliteStudio.slnx --locked-mode
```

Locked mode fails if the lock files and the project files disagree — that is the
point. After deliberately changing a package version, regenerate them and commit
the result:

```powershell
dotnet restore FerroliteStudio.slnx --force-evaluate
git add '**/packages.lock.json'
```

## Note on the source that ships with the installer

The copy of the source included with the installer has had its **comments
removed from `.cs`, `.xaml`, `.csproj` and `.slnx` files** — purely to keep the
payload small. Everything else is byte-identical: the same code, the same
project files, the same committed `packages.lock.json` files, `global.json`,
this file, and the license. If you want the commented original (and the exact
source CI builds), build from the repository instead of the installer payload.
Either copy builds and tests identically, and the payload is small enough to
read end to end.

## Driver components (optional)

The virtual-gamepad features need third-party drivers, which FerroLite does
**not** bundle. Install them yourself if you want those features:

| Component | Purpose | Source |
|---|---|---|
| ViGEmBus | Virtual Xbox 360 controller (preferred) | <https://github.com/nefarius/ViGEmBus> |
| HidHide | Kernel device cloak | <https://github.com/nefarius/HidHide> |
| vJoy | Legacy virtual-controller fallback | <https://github.com/jshafer817/vJoy> |

Everything else — keyboard, mouse, windows, screen vision, the scripting
language, the recorder — works with no driver installed at all.

If a controller ever stays hidden after an interrupted session, run:

```powershell
dotnet run --project Ferrolite.UI -- --recover-hidhide
```

That restores visibility and exits without opening the UI.

## Building the installer (maintainers)

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-staging.ps1
& "$env:ProgramFiles(x86)\NSIS\makensis.exe" .\installer\NSIS\FerroLiteStudio.nsi
```

The staging script publishes the app self-contained for `win-x64`, generates
`installer/src-clean/` (the comment-stripped source payload), and stages the
license. `installer/bin/` and `installer/src-clean/` are generated output and
are not committed.

The source payload is meant to be *buildable on its own*: it includes the test
project, the solution file, `global.json`, `BUILD.md`, and `LICENSE.txt`. Before
publishing an installer, extract `installer/src-clean/` to a clean directory and
confirm it builds and tests there — a payload that only builds next to the rest
of the repository is worse than no payload at all.

### Publishing a release

Publish a SHA-256 checksum next to any build artifact so a download can be
verified without trusting the download page:

```powershell
Get-FileHash .\FerroLiteStudio_Setup.exe -Algorithm SHA256 | Format-List
```

## License

See [LICENSE.txt](LICENSE.txt). FerroLite Studio is source-available under a
non-commercial license: you may read, build, modify, and redistribute it, but
not sell it. Third-party components are covered in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
