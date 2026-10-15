# Security policy

FerroLite Studio automates input, talks to kernel-level device drivers, and
contains an elevated component. That makes its security model worth stating
plainly rather than assuming.

## Reporting a vulnerability

**Please report privately, not in a public issue.**

Use GitHub's private vulnerability reporting on this repository:
**Security** tab → **Report a vulnerability**. That opens a private advisory
visible only to the maintainers, and it will be answered there.

Please include what you can: affected version or commit, Windows version, the
steps that reproduce it, and what you expected to happen instead. If a
vulnerability involves a third-party driver (ViGEmBus, HidHide, vJoy), report it
to that project as well — FerroLite does not own or patch those components.

There is no bug bounty. This is a small project; reports are handled on a
best-effort basis, and credit is given in the advisory unless you ask otherwise.

## What FerroLite does with elevated privileges

This is the part that matters for judging risk:

- **It does not install a service, a scheduled task, a driver, or a background
  process of its own.** There is nothing of FerroLite's running when the app is
  closed.
- **`--hidhide-broker` is the only elevated component**, and it is the *same*
  executable launched with a different argument. It is started on demand — with
  a normal Windows elevation prompt — only while a controller passthrough is
  armed, and it exits when the app does. Its entire job is: write the device
  blacklist entry, then watch the app and remove that entry if the app dies for
  any reason.
- **Device hiding is reversible by design.** Before any hide is written, an
  ownership record is stored so a later run can undo it; every startup clears
  recorded leftovers before doing anything else; and `--recover-hidhide`
  restores visibility with no UI.
- **Nothing is downloaded and executed silently.** The optional drivers
  (ViGEmBus, HidHide, vJoy) are fetched from their publishers' official GitHub
  releases and each is verified by **Authenticode signature and publisher**
  before it is run. A failed or mismatched check refuses the install.
- **No telemetry, no network calls on its own behalf.** The HTTP functions in
  the scripting language only run URLs that a script asks for.

## Out of scope

- **Antivirus / anti-cheat detections.** A virtual controller and synthetic
  input are detectable by design; that is inherent to the feature, not a
  vulnerability. Using FerroLite where macros are prohibited is a user decision,
  described in the README.
- **The "unknown publisher" warning.** Released installers are not code-signed
  (§ *Known limitations*), so Windows may warn. That warning is expected, not a
  compromise.
- **Anything requiring an attacker who is already an administrator** on the
  machine, or who can write to the install directory or `%LOCALAPPDATA%`.

## Known limitations

- **Released installers are not code-signed.** Windows may show a SmartScreen or
  "unknown publisher" prompt. The mitigation is to build from source, which
  needs no trust in any published binary.
- **HidHide's signed release (v1.5.230) has no session-scoped hide API**, so a
  hide that is interrupted can outlive the app; the ownership records, the
  startup sweep, the broker watchdog, and `--recover-hidhide` exist specifically
  to make that recoverable. Details in the README's troubleshooting section.
- **HidHide keeps a whitelist entry for the executable after uninstall.** That
  entry only *allows* the app to see hidden devices; it never hides anything,
  and it is inert once the app is gone. It is disclosed here rather than removed
  because removing a shared driver's entries can affect other tools.
