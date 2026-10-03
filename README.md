# ForeverRoutedSpeech

Speak. Choose the audience automatically. Paste and send with your controller.

This is a public fork of [SpeakForever](https://github.com/samsbase/SpeakForever), based on commit `41f212558f4e9ba378ec44e9e6b9e44177b6a7be`, with its MIT notice preserved. It adds a persistent fastText classifier after transcription. Source is published at [ryeceps/ForeverRoutedSpeech](https://github.com/ryeceps/ForeverRoutedSpeech). No binary release is published.

## Preview status

The final test run was blocked by **Windows Smart App Control**, which rejected the newly built unsigned `SpeakForever.Core.dll`. Earlier tests and the Turbo benchmark passed. The final preview must not be treated as a validated runnable release on this PC. A properly signed/trusted build is needed; Windows security settings have not been changed. No signing certificate is configured in this development workspace.

## One package, one speech model

Extract `ForeverRoutedSpeech-windows-x64.zip` and double-click `Start.cmd`. The launcher installs the included Microsoft runtime only when needed (Windows may ask for elevation), then opens `ForeverRoutedSpeech.exe`. Windows x64, Windows 10 2004 or newer. The portable package includes .NET, the native inference libraries, Microsoft's signed runtime prerequisite, the classifier, **Whisper Turbo q5_0**, and the addon probe. No model picker, API key, cloud transcription, or separate model download is needed. GPU inference uses the available Vulkan driver, with CPU fallback. First model loading can take time; wait for Ready.

With game chat open, click the right stick to record, speak, then click it again to finish. Recording waits for that click, with a 30-second cap. The overlay shows the message, destination, and reason. Use your physical controller button mapped to Ctrl+V to paste, then the game's send button. Clicking dictation while a copied draft waits cancels it. Microphone and controller bindings remain adjustable.

The app copies text; it never generates game key presses or sends chat automatically. Blizzard approval for this particular integration has not been established; see [policy notes](docs/POLICY.md).

## Routing and game verification

Explicit instructions such as `Tell guild ...` or `Ask in trade ...` take precedence. Confident trained guild intent can select Guild. Ordinary conversation falls back through available Instance, Raid, Party, then Say. Trade/General/LFG inference remains disabled until held-out precision meets the target; explicit joined channels work. Custom channels use explicit names or manual confirmation.

The addon status strip supplies group membership, joined channels and current numbers, verified prefixes and limits, and a heartbeat. Missing, invalid, or stale context preserves the clipboard and shows a confirmation panel. It does not assume a Forever message limit. Oversized messages need editing; they are not truncated. Transcripts are not written to the default log, and recordings are not retained.

**This is an early test build. The actual Forever client compatibility probe, controller/microphone flow, UI scaling and occlusion tests, and live gameplay performance remain unverified.** Install the included addon using `scripts/Install-Addon.ps1` with the client's actual AddOns directory and Interface number. Follow the addon's probe instructions to verify destinations and message limits. These developer checks must be completed before a zero-configuration public release.

Capture defaults match the addon strip's initial placement: game window `World of Warcraft`, client coordinates `(16,64)`, cell pitch 4 physical pixels. If scaling changes those values, adjust `%LOCALAPPDATA%/ForeverRoutedSpeech/capture.json`; a calibration wizard is not included yet. Settings are independent of upstream SpeakForever.

Auction House dictation outputs plain text when the addon identifies a registered, verified focused search field. It adds no chat prefix. Unknown fields are blocked. The companion overlay previews text before paste; the addon preview shows the native text box after your physical paste.

## Development

From this fork's directory:

```powershell
./scripts/Setup-Tools.ps1
# CMake 3.31.6 must be available. Python 3.12 is used for training/Lua tests.
./scripts/Build.ps1
```

Dependencies and NuGet graphs are pinned. Setup verifies the SDK/compiler checksums. Build verifies the pinned Turbo download, runs core and real-classifier tests, then produces one self-contained ZIP. Native dependencies use pinned commits. For retraining, run `scripts/Train.ps1`; training uses held-out paraphrase families and writes the model hash and inference policy together. Retraining never runs during play.

The original upstream `build.ps1`, installer, CLI and website are retained for source provenance; the fork workflow only builds and tests and never publishes releases; use the fork's `scripts/Build.ps1` for this package. Upstream auto-updates are disabled. The legacy WPF experiment in the enclosing workspace is not this fork's app.
