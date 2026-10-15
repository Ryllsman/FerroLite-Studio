# FerroLite Studio — Installation Guide

**September 2026**

---

## Requirements

- Windows 10 (64-bit) or Windows 11
- ~350 MB free disk space (app + bundled runtime)
- Internet connection **only** if you tick the optional driver components
  (they download from their official publishers during setup)

## Installing

1. Run `FerroLiteStudio_win-x64.exe`.
2. Choose your language and accept the license.
3. **Components page:**
   - **FerroLite Studio** (required) — the app itself.
   - **ViGEmBus (recommended)** — virtual Xbox 360 controller driver.
     Games then see a genuine "Xbox 360 Controller" for macro output.
   - **HidHide (recommended)** — kernel device cloak. While a passthrough is
     armed, your physical pad is hidden from every app except FerroLite, so
     the game sees exactly one controller with no double input.
   - **vJoy (legacy)** — only for tools that specifically require it.
4. Finish, and launch FerroLite Studio.

## First launch

1. New tab opens ready to edit — type and press **Ctrl+Shift+G** to run.
2. Try the classic first script:
   ```
   print("hello, ferrolite")
   ```
3. Open this `Documents` folder any time for the full language reference,
   documentation, and license.

## Verifying the controller chain (optional)

With a controller connected (wake it with the PS/home button or USB):

```ferrolite
print("chain: " + controller_driver())
controller_passthrough(true)      # arms and returns — script keeps going
print("status: " + str(controller_passthrough_status()))
wait(3)
controller_passthrough(false)     # optional: also disarms when the script ends
```

Expected output with everything installed and the pad awake:

```
chain: xinput+vigem+hidhide
status: {active: true, hid_hidden: true, virtual_pad: vigem}
```

Check `joy.cpl` (Win+R → joy.cpl) while armed: the physical pad disappears
and one "Xbox 360 Controller" appears. When the script finishes (or you press
Stop), both reverse cleanly.

## Silent installation (advanced)

```
FerroLiteStudio_win-x64.exe /S
```

Installs to the default location with previously chosen components.

## Uninstalling

Use **Apps & Features** in Windows Settings, or run
`Uninstall FerroLite Studio` from the Start menu. The uninstaller removes
the app, shortcuts, and (optionally) the downloaded driver components stay
untouched unless you remove them via their own uninstallers.

## Upgrading

Run the new installer over the old one — settings, recordings, and scripts
are preserved. Close FerroLite Studio before upgrading.
