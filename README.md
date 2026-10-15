<p align="center">
  <img src="Ferrolite.UI/Resources/ferrolite-mark.png" width="112" alt="FerroLite Studio logo">
</p>

<h1 align="center">FerroLite Studio</h1>

[![CI](https://github.com/Ryllsman/FerroLite-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Ryllsman/FerroLite-Studio/actions/workflows/ci.yml)

**Record or write what you want your keyboard, mouse, and controller to do — then replay it into real applications, including games.**

FerroLite is a Windows automation studio built around one readable scripting language with **300+ built-in functions**, a live recorder, a real debugger, and a controller passthrough chain that merges your real hands with macro output into one virtual Xbox 360 pad — the pad disappears from the game's point of view and the virtual one takes over, while your physical controller keeps working.

> Battle-tested through many rounds of fixes: the language, interpreter, recorder, debugger, and the full controller passthrough chain are properly functional and stable in everyday use. Game-specific quirks (Xbox-mode pads, drivers) are handled and clearly reported in-app when something is outside user-mode control. Only reason there is only 4 commits, is as of the fact that I personally tested it multiple times before even commit 1, and during and before commit 2 and 3, i tested it rigorously and found zero to no issues.

---

## Get it running

**Prefer a finished binary?** Download the installer from the [latest release](https://github.com/Ryllsman/FerroLite-Studio/releases/latest) and run it:

1. Run `FerroLiteStudio_win-x64.exe`.
2. On the components page, keep **FerroLite Studio**, and tick **ViGEmBus** and **HidHide** if you want the virtual-controller features. (Skipping them is fine — the rest of the app needs no drivers.)
3. Launch it. A script tab opens ready to type; press **Ctrl+Shift+G** to run.

Windows will warn you that the installer is unsigned ("unknown publisher"). That warning is real — see [How much do you have to trust us?](#how-much-do-you-have-to-trust-us) for why, and what to do instead if you'd rather not accept it.

Step-by-step detail, including silent install and uninstall: [`Documents/INSTALLATION.md`](Documents/INSTALLATION.md).

**Prefer to build it?** Three commands, needs only the [.NET SDK 8.0+](https://dotnet.microsoft.com/download/dotnet/8.0):

```bash
git clone https://github.com/Ryllsman/FerroLite-Studio.git
cd FerroLite-Studio
dotnet run --project Ferrolite.UI
```

Expected: `0 warnings, 0 errors`, then the app window. Full details and troubleshooting: [`BUILD.md`](BUILD.md).

## What FerroLite can actually do

### 🎮 Controller world (the headline act)

Map aliens? No. What you get is a virtual-pad pipeline your script and your hands drive together:

```ferrolite
controller_passthrough(true)   // one call. Your pad + macros = one Xbox 360 pad
wait(0.5)
controller_hold(0, "a", 300)   // macro output merges on top of your real input
print(controller_passthrough_status())
```

- **21 `controller_*` functions** — read buttons/sticks/triggers, inject buttons, rebind (aliases), rumble, battery, and everything the passthrough chain needs.
- **8 `virtual_controller_*` functions** — targeted injections onto the virtual pad itself: state, sticks, triggers, buttons, connection toggle — with or without arming the whole chain.
- **vJoy fallback** where ViGEm won't do (`vjoy_enabled()`).
- **Player 1, taken properly.** XInput pads that would otherwise sit on slot 1 get *moved to player 1* while armed — FerroLite briefly parks your physical pad (via a UAC-prompted helper), lands the virtual pad on slot 0, then hands your pad back. Crash- and kill-proof: a helper watches the app and restores your pad even if FerroLite dies mid-run.
- **125 Hz read loop** on the physical pad, merged with every macro output, written to the virtual pad — your hands and the script play together, in the game, live.

### 🖱️ Real desktop control

`mouse_move`, `mouse_click`, `double_click`, `right_click`, `mouse_down/up/hold`, relative moves, `scroll`, `drag`, `key_press`, `key_down/up/hold`, `key_combo`, `hotkey`, `type_text` (with per-character delay), `paste_text` — everything is injected into whichever real app you focus.

### 👀 Screen vision

```ferrolite
screenshot()                          // full or region capture, saveable
get_pixel_color(100, 200)             // instant pixel checks
find_color("#ff0000", tolerance)      // scan formats for a color
find_image_on_screen("button.png")    // template matching (OpenCV) for UI elements
wait_for_color(...)                   // block until (or while) something appears
color_in_region(...)                  // fast region checks
compare_images(...)                   // diff two captures
```

### 🪟 Window & process control

`find_window`, `focus_window`, `get_active_window`, `move_window`, `minimize/maximize`, `close_window`, `window_exists`, `list_windows`, `list_processes`, `launch`, `kill_process`, `process_exists`, `open_file`, `open_url`.

### 🧠 Data, network, files

```ferrolite
var data = json_parse(http_get("https://api.example.com/rank")["body"])
db_query("SELECT * FROM runs WHERE score > ?", [100])   // real SQLite
csv_read("results.csv")
file_write("out/speedrun.txt", report)
regex_replace(text, pat, sub)     // plus match, match_all, test
base64_encode, sha256, md5, hex_encode, url_encode — the whole utility belt
http_post / http_put / http_delete / http_download
```

### 💬 Surfaces & feedback

- **GUI overlays**: `gui_create_overlay` + labels/buttons/toggles floating over your game — build live HUDs, toggle macros from on-screen buttons, get callbacks in-script.
- `notify` (toast notifications), `beep`, `clipboard_get/set`, `env_get`, timers, `format_*`, date/time helpers.

### ✍️ The recorder (why typing is optional)

Press **Ctrl+Shift+R**, do the thing with your hands, stop. You can save the recording, drop it into a script as a single `play_recording(...)`, or *decouple* it into readable lines you can edit. Playback runs through the same pipeline scripts do, with speed control — and the timeline shows every event so you can prune it.

### 🧪 The Studio itself

- **Editor** with Ferrolite syntax highlighting, go-to-line, find/replace, multi-tab, and *session memory*: your tabs, unsaved scratchpads, carets, and window placement all survive a restart (even a crash).
- **Terminal pane** for script output; **debugger pane** with breakpoints, step over/into/out, live variables, and the call stack; **timeline pane** for recordings.
- **Hotkeys**: `Ctrl+Shift+G` runs the tab, `Ctrl+Shift+R` record/stop, `Ctrl+Shift+Backspace` stop everything.
- **Installer** that fetches optional drivers from their vendors' official releases and verifies signatures before installing them — the app itself needs no drivers at all.

## Your first five minutes

```ferrolite
print("hello, ferrolite")          // Ctrl+Shift+G runs the tab
```

Then try the fun parts:

- **Record instead of typing.** Press **Ctrl+Shift+R**, do the thing with your hands, stop. You can save the recording, drop it into a script as a single `play_recording(...)` call, or *decouple* it into readable lines you can edit.
- **Drive the desktop.** `focus_window("Notepad")`, `type_text("Hello", 0.02)`, `hotkey(["ctrl", "s"])`.
- **Arm the controller chain.** With a pad connected and the drivers installed:

  ```ferrolite
  print("chain: " + controller_driver())
  controller_passthrough(true)      // returns immediately; your script keeps running
  wait(3)
  controller_passthrough(false)     // also disarms automatically when the script ends
  ```

  Expected next to `status:`: `{active: true, hid_hidden: true, virtual_pad: vigem}`. Open `joy.cpl` while armed and you'll see your physical pad hide and one "Xbox 360 Controller" appear — then both reverse cleanly on stop. Your pad re-apppears **exactly as it was** even if FerroLite is killed mid-run.
- **Step through it.** Breakpoints, step over/into/out, and variable/call-stack inspection are in the debugger pane.

More: full function list in [`Documents/LANGUAGE_REFERENCE.md`](Documents/LANGUAGE_REFERENCE.md), guided tour and internals in [`Documents/DOCUMENTATION.md`](Documents/DOCUMENTATION.md), a starter script in [`examples/`](examples).

## The controller chain, in one picture

```
Physical pad ──[HidHide: hidden from every other app]──► FerroLite reads it at 125 Hz
                                                         │ merges your hands + macro output
                                                         │ parks & re-takes slot 0 when needed
                                                         ▼
                                        ViGEm virtual Xbox 360 pad ──► the game
```

While armed, `virtual_controller_*` functions report the merged state the game actually receives. The chain is torn down when the script ends (or on Stop / `controller_passthrough(false)`), so a virtual pad never outlives the run that created it — and ownership/crash recovery means your physical pad is never left invisible.

## What's in the box

| Piece | What it does |
|---|---|
| **Ferrolite language** | Variables, `if`/`while`/`for`, functions, maps, lists, `try`/`catch`, and **300+ built-ins**: input, windows, screen vision, HTTP, SQLite, files, GUI overlays. |
| **Recorder** | Ctrl+Shift+R captures real input and turns it into reusable script. |
| **Controller passthrough** | Hides your physical pad from other apps (HidHide), reads it at 125 Hz, merges macro output, takes/keeps **XInput player 1**, and writes the result to a ViGEm (or vJoy) virtual pad. |
| **Debugger** | Breakpoints, stepping, live variable and call-stack inspection. |
| **Installer** | NSIS installer that fetches optional drivers from their vendors' official releases and verifies their signatures before installing. |

## Requirements

- **Windows 10 or 11, x64**
- ~350 MB free disk space (the installer bundles the .NET runtime)
- **.NET SDK 8.0 or newer** — only if you build from source
- Optional, for virtual-controller features: [ViGEmBus](https://github.com/nefarius/ViGEmBus) (virtual Xbox 360 pad), [HidHide](https://github.com/nefarius/HidHide) (device cloak), [vJoy](https://github.com/jshafer817/vJoy) (legacy fallback)

Everything except the virtual-pad features works with no driver installed at all.

## If your controller stops working outside FerroLite

The passthrough briefly hides your physical pad from every application — that's the point. HidHide's signed release (v1.5.230, May 2024) only ships the *persistent* blacklist, not the crash-proof session API, so an interrupted hide can outlive FerroLite and leave the pad invisible to games and to Windows itself. FerroLite defends against this three ways:

1. The blacklist is never written without an **ownership record** being stored first, by both the in-process path and the elevated broker.
2. **Every startup** removes any recorded leftover entry before anything else touches the pad, and switches HidHide's master flag back off when the blacklist empties.
3. The elevated broker watches FerroLite and restores visibility if it exits for any reason.

To inspect or repair the driver state by hand:

```powershell
dotnet run --project tools/hidhide-probe              # read-only: master switch, blacklist, whitelist
dotnet run --project tools/hidhide-probe -- --repair  # clear leftover controller entries, restore visibility
```

`--repair` only touches controller-class entries (Sony pads, vJoy) and never removes other tools' entries. There's also `Ferrolite.exe --recover-hidhide` for a quick fix. If you build HidHide from upstream source (which does have the session API), FerroLite detects it automatically and uses the crash-proof session path instead.

## How much do you have to trust us?

FerroLite asks for elevated privileges to hide and re-create controllers, so you shouldn't have to take our word for anything. There are three levels here, and you can stop at whichever you're comfortable with:

| | What it costs you | What it proves |
|---|---|---|
| Read the code | nothing | what it does |
| Build it yourself | a .NET SDK | what you run matches the code |
| Take the installer | accepting a Windows warning | nothing — that's the gap |

The installer is the only level that asks for trust, and it's the only one we can't fully fix without paying for a code-signing certificate ([known limitations](SECURITY.md)). Two things soften it: **the complete source ships inside the installer** as a selectable component, and the release notes publish the **SHA-256** of the binary so you can verify the download (`Get-FileHash .\FerroLiteStudio_win-x64.exe -Algorithm SHA256`).

What's already arranged so the honest answer is "don't take my word for it":

- **The build is checked, not asserted.** GitHub Actions builds this exact commit on a clean `windows-latest` runner — locked restore, warnings as errors, then the test suite. The badge at the top is that run and you can re-run it: [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
- **Dependencies are pinned by hash.** Committed `packages.lock.json` files record the exact version and content hash of every direct and transitive NuGet package, so a build isn't at the mercy of what nuget.org serves today.
- **No binaries are bundled.** Setup downloads ViGEmBus / HidHide / vJoy from their publishers' official releases and verifies the Authenticode signature and publisher before executing anything.
- **The security model is written down.** What runs elevated, when, and why — plus what is explicitly out of scope — is in [`SECURITY.md`](SECURITY.md).
- **Nothing is hidden on your machine.** The footprint is the install directory, `%LOCALAPPDATA%\FerroLite` (settings, recordings, recovery state), and two `HKLM` registry keys. Uninstalling removes the program and asks before touching your data.
- **The elevated helper is the same executable.** `--hidhide-broker` and `--slot-park-broker` run only while a passthrough is armed, via a standard elevation prompt. No background service, no second program.
- **One residual, disclosed:** HidHide keeps a whitelist entry for `Ferrolite.exe` after removal. That entry only *allows* FerroLite to see hidden devices; it never hides anything, and a stale entry for an uninstalled executable is inert.

## Building, testing, and packaging

```bash
dotnet build FerroliteStudio.slnx
dotnet test  Ferrolite.Core.Tests
dotnet run   --project Ferrolite.UI
```

Any .NET SDK **8.0 or newer** works — the projects target `net8.0-windows` and `global.json` sets the floor, so an older SDK fails with a clear message instead of a confusing build error. `FerroliteStudio.slnx` is the modern XML solution format and needs **Visual Studio 17.13+** or the `dotnet` CLI; on older tooling, open the three `.csproj` files directly.

A handful of tests capture the real desktop or drive the real input pipeline. They're tagged `Category=Hardware` and run by default on your machine; CI excludes them, because on a headless runner they measure the runner rather than the code:

```bash
dotnet test Ferrolite.Core.Tests                                # everything (157)
dotnet test Ferrolite.Core.Tests --filter "Category!=Hardware"  # exactly what CI runs (153)
dotnet test Ferrolite.Core.Tests --filter "Category=Hardware"   # just the display/input tests
```

Verify a fully pinned restore at any time with:

```bash
dotnet restore FerroliteStudio.slnx --locked-mode
```

Building the installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\build-staging.ps1
& "$env:ProgramFiles(x86)\NSIS\makensis.exe" .\installer\NSIS\FerroliteStudio.nsi
```

The staging script publishes the app, stages the license, then generates `installer/src-clean/` — the comment-stripped copy of the source that ships inside the installer. Generated output (`src-clean/`, `bin/`, `obj/`) is not committed. See [`BUILD.md`](BUILD.md) for prerequisites and troubleshooting.

## A taste of the language

```ferrolite
// Arm the controller chain, then drive it.
controller_passthrough(true)          // returns immediately; script keeps running
wait(0.5)
controller_hold(0, "a", 300)         // merged on top of your real input
print(controller_passthrough_status())

// Or automate the desktop.
focus_window("Notepad")
type_text("Hello from FerroLite", 0.02)
hotkey(["ctrl", "s"])
```

## Project layout

```
Ferrolite.Core/          Language (lexer, parser, interpreter, stdlib) + Windows backends
Ferrolite.UI/            WPF app: editor, terminal, debugger, recorder, installer glue
Ferrolite.Core.Tests/    xUnit suite
Documents/               Language reference, documentation, installation notes
examples/                Sample scripts
installer/               NSIS installer + staging script
tools/                   Developer utilities (e.g. the HidHide state probe)
```

## Responsible use

FerroLite is an input-automation tool for macros, accessibility, testing, and personal automation. It is not designed or intended to bypass anti-cheat or DRM systems, and using it that way is your responsibility; many games prohibit macro input, and a virtual controller is detectable. Use it where you are allowed to.

## Third-party components

FerroLite does not bundle or redistribute any driver binaries. The installer downloads them from the vendors' official releases and verifies their Authenticode signatures before running them:

- **ViGEmBus** and **HidHide** — Nefarius Software Solutions, distributed under their own licenses
- **vJoy** — the official vJoy project (using the maintained `jshafer817/vJoy` fork, because the original has not published signed builds for current Windows)

OpenCV (`OpenCvSharp4`) is used for template matching; SQLite support via `Microsoft.Data.Sqlite`.

## License

See [`LICENSE.txt`](LICENSE.txt). This is a **non-commercial source-available license**: you may read, build, modify, and redistribute FerroLite Studio — including for paid work you do with it — but you may not sell the software itself. Third-party components remain under their own licenses; see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

Yes the readme is AI im a lazy bum, also commits were made by AI, I did the coding. (Yes I copied this from my other Repo, im that lazy)
