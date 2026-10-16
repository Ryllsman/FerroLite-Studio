# FerroLite Studio — Official Documentation

**September 2026**

FerroLite Studio is a desktop automation studio for Windows: a script editor,
a macro recorder, a screen-vision engine, and a full gamepad controller stack
in one tool. Scripts are written in the **Ferrolite language** — small,
readable, and purpose-built for automation.

This document covers the architecture, the recorder, the controller chain,
packaging, and troubleshooting. For the language itself, see
**LANGUAGE_REFERENCE.md** in this folder.

---

## 1. What's in the box

| Component | What it does |
|---|---|
| Script editor | Tabs, syntax highlighting, autocomplete, error underlines |
| Terminal | Live output from scripts, clickable error lines |
| Recorder | Captures mouse/keyboard (and gamepad) actions, inserts them as code |
| Timeline | Live view of recorded events with sub-5 ms precision |
| Controller stack | XInput/DualSense input, ViGEm virtual Xbox 360 pad, HidHide cloaking |
| Screen vision | Pixel reads, color search, image matching (OpenCV) |
| GUI overlays | Scriptable on-screen buttons, labels, toggles |

## 2. Architecture overview

```
┌───────────────────────────────────────────────────────────┐
│  Ferrolite.UI (WPF)                                       │
│  Editor · Recorder UI · Timeline · Terminal · Overlays    │
└──────────────────────────┬────────────────────────────────┘
                           │ runs scripts
┌──────────────────────────▼────────────────────────────────┐
│  Ferrolite.Core                                           │
│  Lexer → Parser → Interpreter → StdLib                    │
└───────┬──────────────┬──────────────┬─────────────────────┘
        │              │              │
   WindowsBackend  ControllerBackend  Recorder
   (SendInput,     (XInput/DualSense, (Raw Input,
   GDI pixels,     ViGEm virtual pad, hook capture,
   windows)        HidHide cloak)     event log)
```

- **Interpreter** executes scripts on a dedicated thread; the UI stays
  responsive. Stop is honored between every command.
- **WindowsBackend** is the KBM + screen layer: `SendInput` injection,
  `GetAsyncKeyState`/`GetKeyState` polling, GDI pixel capture.
- **ControllerBackend** unifies physical pads (XInput DLL chain with
  DualSense HID fallback) and virtual pads (ViGEm → vJoy fallback).
- **Recorder** captures hardware-level input via Raw Input (WM_INPUT),
  storing timestamps, deltas, and button states for faithful replay.

## 3. The macro recorder

- **Start recording** (toolbar or Ctrl+Shift+R). A timeline panel appears and
  every captured event shows **immediately** (event-driven, no polling).
- **Stop** with the button, or **Ctrl+Shift+Backspace** from anywhere.
- On stop, a dialog offers: **save** to a `.ferro` recording file, **insert**
  the raw recording into the script as a `play_recording(...)` call, or
  **decouple** — expand the raw events into readable code.
- Playback uses the recorded delays verbatim; no smoothing is applied, so
  replays match your hands exactly.

## 4. The controller chain (physical → virtual)

This is the controller equivalent of keyboard/mouse injection:

```
Physical pad (SCUF/DualSense/Xbox)
   │
   ▼
[HidHide kernel filter]  ← hides the physical pad from every app
   │                        except FerroLite (whitelisted)
FerroLite reads your real inputs at 125 Hz
   │  merges: your hands + script output (controller_press etc.)
   ▼
[ViGEmBus virtual Xbox 360 pad]  ← what the game sees
   │
   ▼
Game
```

- **Arming:** `controller_passthrough(true)` in a script. The command is
  **non-blocking** — it returns at once and the rest of the script keeps
  running (macros merge onto the virtual pad, `virtual_controller_*` reads it
  back). **Disarming is automatic:** the chain is torn down when the script
  ends — normally, on error, or on Stop — so a virtual pad never outlives the
  run that created it. You can also disarm early with
  `controller_passthrough(false)`. Closing FerroLite always disarms.
- **Hiding modes:** on HidHide ≥ 1.6 the session blacklist is used (entries
  vanish automatically even on crash). On HidHide 1.5.x the persistent
  blacklist is used with an ownership record, so only entries FerroLite
  added are ever removed — recovery is automatic on next launch.
- **Visibility while armed:** the status bar shows a **CONTROLLER ARMED**
  chip (orange when the physical pad is hidden, red when it is still visible)
  and the terminal prints an ARMED/DISARMED line, so a run that arms in
  3 ms never reads like a script that died.
- **Administrator:** FerroLite requests elevation at launch (one prompt),
  the same choice DS4Windows makes. HidHide's write path is admin-only, so
  this is what makes hiding work in-process, with no UAC prompt mid-game.
- **What gets hidden, and how it is identified:** every HID interface whose
  PnP ids say game controller (`HID_DEVICE_SYSTEM_GAME` / `_JOYSTICK` /
  `_MULTIAXIS`, `HID_DEVICE_UP:0001_U:0004|0005|0008`), falling back to HID
  caps (UsagePage 1 + Usage Joystick/Game Pad/Multi-axis) and then to the
  Sony VID / `&IG_` path heuristics. Identification is by **device identity,
  not vendor id or path shape**, so a pad in Xbox mode is covered as well as
  a DualSense. Our own virtual pad is excluded structurally (emulated ROOT
  ancestry), and vJoy is excluded from that list but hidden separately while
  ViGEm is the backend — matched by its ids (`VID_1234&PID_BEAD`), because
  vJoy 2.1.9 enumerates as `ROOT\HIDCLASS\0000`, which no path rule catches.
- **Status:** `controller_passthrough_status()` returns
  `{active, hid_hidden, virtual_pad, note, physical_paths, vjoy_paths,
  hidden_paths, hidden_at_arm}`; `controller_hide_report()` adds the driver
  chain, elevation, HidHide mode/failure, XInput-only devnodes, what is NOT
  hidden, and a plain-language `verdict`. `controller_driver()` names the
  chain (e.g. `xinput+vigem+hidhide`).
- **Fallbacks:** no ViGEmBus → vJoy. No HidHide → passthrough still works,
  the game just also sees the physical pad (double input).
- **Xbox/XInput-mode controllers — a hard limit, stated plainly.** A pad that
  presents itself as `Xbox 360 Controller for Windows` (XnaComposite / XUSB
  through `xusb*.sys`) is enumerated by games and browsers through **XInput**,
  a different stack from HID. The cloak is a HID-class filter, so it cannot
  mask such a device: the game keeps seeing the physical pad *and* the virtual
  pad, and a web Gamepad test lists both (e.g. "Xbox 360" plus "Wireless
  Controller"). No user-mode software can avoid this, DS4Windows included.
  Two consequences worth knowing:
  - The pad usually holds XInput **slot 1**, so a game reading the first pad
    still reads the unmodified one — script output only affects the virtual
    pad.
  - Fix: put the controller in its **PS/DualSense (HID) mode**. A HID-only pad
    is fully cloakable, which is the mode where you get exactly one pad.
  `controller_hide_report()` reports this case as `XINPUT LEAK` and lists the
  devnodes (`physical_xinput_pads`).

### Driver components (installer)

| Driver | Purpose | Source |
|---|---|---|
| ViGEmBus | Virtual Xbox 360 controller | Nefarius (official, signature-verified) |
| HidHide | Kernel-level device cloak | Nefarius (official, signature-verified) |
| vJoy | Legacy virtual controller | Official vJoy project |

The installer downloads these during setup and verifies each binary's
Authenticode signature before executing it. Without them, everything else
still works.

## 5. Screen vision

- `get_pixel_color(x, y)` and channel variants read single pixels.
- `find_color` / `find_color_region` search the screen for a color.
- `find_image_on_screen(image, confidence)` uses OpenCV template matching.
- `wait_for_color` / `wait_for_color_gone` block until a condition.
- `save_screenshot("path.png")` captures the full screen.

## 6. Script lifecycle and safety

- Scripts run on a background thread; the editor stays interactive.
- **Stop** (Ctrl+Shift+Backspace) interrupts waits, sleeps, and loops.
- Errors print with `at line N`; clicking the line jumps to it. Unknown key
  names, bad file paths, and type errors are all loud — nothing fails
  silently.
- On any exit path, passthrough, hiding, and GUI overlays are cleaned up.

## 7. Files and folders

| Path | Purpose |
|---|---|
| `%LocalAppData%\Ferrolite\` | Settings, recordings, runtime state |
| `Documents\` (install dir) | This documentation |
| Recordings | Saved from the recorder's stop dialog, `play_recording()` replays them |

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `chain: xinput` only | ViGEmBus not installed — rerun installer with the component ticked |
| `hid_hidden: false` | Physical pad asleep or disconnected; wake it, re-arm |
| Pad invisible in game, no virtual pad | Both virtual drivers missing; install ViGEmBus |
| Double input in game | HidHide absent — game sees physical AND virtual pad |
| Test site still lists my controller | Run `print(controller_hide_report())` — its `not_hidden` list names what leaked, and `verdict` says whether it is a leak or an XInput-only pad |
| Site lists e.g. "Xbox 360" + "Wireless Controller" | The physical pad is in Xbox/XInput mode — XInput cannot be cloaked (see §4). The "Wireless Controller" is FerroLite's own virtual DualShock 4. Put the pad in PS mode, or set the virtual pad type in *Settings* to match |
| Two pads, one of them vJoy | Leftover vJoy device — now identified by id and hidden while ViGEm is armed |
| vJoy shows in joy.cpl after updates | vJoy is a permanent root device; it is cloaked while armed and released on disarm. A vJoy in *Error* state is a broken driver install, not FerroLite |
| `[`/`]` not working in old scripts | Update; older builds resolved them to the wrong keys |
| `is_key_toggled("caps")` never true | Update; older builds polled the wrong state |
| Recorder misses fast movement | Use the built-in Raw Input recorder (default); it captures hardware deltas |

## 9. Keyboard shortcuts

See LANGUAGE_REFERENCE.md §Shortcuts — the editor, runner, and debugger
shortcuts are listed there.
