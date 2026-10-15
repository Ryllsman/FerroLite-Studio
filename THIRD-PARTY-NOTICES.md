# Third-party notices

FerroLite Studio links or uses the components below. Each remains under its own
license; nothing here changes the terms of the FerroLite Studio license in
[LICENSE.txt](LICENSE.txt).

## Nothing is bundled

**FerroLite Studio does not redistribute any driver binary.** The optional
virtual-controller drivers are listed so the installer can download them
directly from their publishers' official GitHub releases at install time, and
each downloaded installer is verified with **Authenticode signature and
publisher check** before it is executed. If a download is missing, corrupted,
or signed by anyone other than the expected publisher, setup refuses to run it.

The NuGet packages below are resolved from nuget.org at build time in the normal
way; they are not checked into this repository.

---

## Driver components (downloaded at install time, not bundled)

| Component | Publisher | Purpose | Project |
|---|---|---|---|
| **ViGEmBus** | Nefarius Software Solutions e.U. | Virtual Xbox 360 pad | <https://github.com/nefarius/ViGEmBus> |
| **HidHide** | Nefarius Software Solutions e.U. | Kernel device cloak | <https://github.com/nefarius/HidHide> |
| **vJoy** | vJoy project (maintained fork by jshafer817) | Legacy virtual-controller fallback | <https://github.com/jshafer817/vJoy> |

These are separate works with their own licenses. Installing them is optional
and subject to whatever terms their publishers set. FerroLite neither owns nor
relicenses them, and it does not modify them.

The `Nefarius.ViGEm.Client` library below is the client-side API used to talk to
ViGEmBus; the driver itself is a separate download.

---

## .NET packages

### Ferrolite.Core

| Package | Version | Purpose | License |
|---|---|---|---|
| `Nefarius.ViGEm.Client` | 1.21.256 | ViGEmBus client API (virtual pad output) | Nefarius Software Solutions — see project |
| `OpenCvSharp4` | 4.9.0.20240103 | Template matching / screen vision | Apache-2.0 |
| `OpenCvSharp4.Extensions` | 4.9.0.20240103 | OpenCV extensions | Apache-2.0 |
| `OpenCvSharp4.runtime.win` | 4.9.0.20240103 | Native OpenCV runtime for Windows | Apache-2.0 |
| `Microsoft.Data.Sqlite` | 10.0.11 | SQLite support for the `db_*` script functions | MIT |

### Ferrolite.UI

| Package | Version | Purpose | License |
|---|---|---|---|
| `AvalonEdit` | 6.3.1.120 | Code editor control | MIT |

### Ferrolite.Core.Tests

| Package | Version | Purpose | License |
|---|---|---|---|
| `xunit` | 2.9.3 | Test framework | Apache-2.0 |
| `xunit.runner.visualstudio` | 3.1.4 | Test runner | Apache-2.0 |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | Test host | MIT |
| `coverlet.collector` | 6.0.4 | Coverage collection | MIT |

Test-only dependencies are not part of the shipped application.

---

## Assets

The application icon and brand mark are original works of the FerroLite project.

## Upstream redistributions

If you redistribute FerroLite Studio, your distribution must keep this file and
must not imply that the third-party publishers endorse your distribution. The
driver installers remain the property of their respective publishers and should
be obtained from the official releases linked above.
