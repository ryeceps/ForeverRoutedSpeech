# ForeverRoutedSpeech

**A fork of [samsbase/SpeakForever](https://github.com/samsbase/SpeakForever), adding local audience routing to controller speech-to-chat drafts.** SpeakForever supplies the Windows app, controller capture and speech foundation. ForeverRoutedSpeech adds a persistent fastText classifier, a game-context addon and guarded chat drafts, and simplifies speech recognition to **Whisper Turbo q5_0 only**. Original MIT attribution and Git history are preserved; this is an independent fork, not an official upstream or Blizzard release.

## Download and preview status

Get the Windows x64 ZIP from [Releases](https://github.com/ryeceps/ForeverRoutedSpeech/releases). The downloadable Preview 1 uses external manual paste; the new third-click paste is currently source-only. The first release is an **unsigned experimental prerelease**, not a validated gameplay release. Windows Smart App Control blocked final runtime tests on the development PC. Earlier component tests passed; final runtime validation, code signing and live Forever compatibility remain outstanding. Do not disable Windows security to run it. See [test results](docs/TEST-RESULTS.md).

Extract the entire ZIP and run `Start.cmd`. Windows 10 2004 or newer is required. The package contains the app, .NET runtime, native libraries, Turbo model, routing classifier, addon and Microsoft's signed VC runtime prerequisite. The launcher installs that prerequisite only if needed; Windows may request elevation. No model picker, API key or separate model download is required. Normal speech processing stays local and offline. GPU inference uses Vulkan where available, with CPU fallback. Wait for Ready after initial model loading.

## Controller workflow

1. Open the game's chat input using your mapped controller button.
2. Click the right stick to start recording, speak, then click it again to stop. Recording is capped at 30 seconds.
3. Whisper Turbo transcribes the recording. fastText and the routing rules select an available audience. The companion previews the editable message, destination and routing reason, then copies a valid draft.
4. Once the draft is ready, **click the right stick a third time to paste**. Press the game's mapped send button separately to send. Wait for Ready between the second and third click; a double-click is not required.

The third controller click generates one Ctrl+V shortcut. It does not open chat, select or clear existing text, or press Enter. Paste requires fresh addon context, the same active chat destination/field, focused WoW, and the unchanged copied clipboard. Failed or partial input is not replayed automatically. You can still paste manually. Keyboard dictation retains its original cancel-ready behavior. Unbind the game's right-stick click action (normally Ping in beta) so it does not fire alongside dictation. Microphone and recording bindings remain adjustable.

## How audience routing works

- **Explicit instruction first:** `Tell guild ...`, `Say to everyone around me ...`, or `Ask in trade ...` selects that audience and removes only the recognized instruction. An unavailable explicit destination keeps the draft waiting for another selection.
- **Confident inference next:** trained guild-address intent can choose Guild. Public-channel inference for General, Trade and LFG is disabled in this preview until held-out precision meets the target. Explicit joined public channels still work; merely mentioning an item or guild does not establish an audience.
- **Otherwise use the active chat panel, then Say:** a known, available active destination is preserved, including a joined numbered/custom channel. Group membership alone no longer changes the audience. Explicit instructions and confident classifier inference still take priority in chat.

Editing the message or destination updates the clipboard when the draft is valid. Silence, transcription errors, unknown fields, invalid context and oversized messages do not replace it. Explicit destinations require confirmation with missing or stale context; ordinary speech can use standalone Say; oversized drafts require editing rather than truncation. Default operation retains no audio or transcript history and does not retrain during play.

## Standalone testing

Without game context, ordinary speech can preview and copy `/say <message>` as a standalone draft. No group/channel inference or controller paste is allowed in this state. The 4096-byte draft cap is not a verified game message limit. Explicit Guild/Trade/custom requests still require confirmation and are never silently redirected to Say. A previously observed stale search field remains blocked instead of being reinterpreted as chat. Manual paste remains available for testing.

## Addon and game setup

The companion cannot read live SavedVariables. The addon renders a framed, checksummed pixel status strip four times per second; the companion captures that region to read group/guild state, joined channel names and current numbers, verified chat prefixes/limits, and heartbeat. Context becomes stale after two seconds. The addon preview can show text in a native field after you physically paste; the companion provides the pre-paste draft preview.

Install the addon with `scripts/Install-Addon.ps1`, supplying the actual client's AddOns directory and Interface number. Follow [compatibility probe instructions](docs/COMPATIBILITY.md) before enabling destinations: Forever APIs, prefixes, limits and field recognition have not been verified on the actual client. Unsupported destinations stay disabled.

Capture defaults are game window `World of Warcraft`, client position `(16,64)` and a cell pitch of 4 physical pixels. Adjust `%LOCALAPPDATA%/ForeverRoutedSpeech/capture.json` if placement or scaling differs. No calibration wizard is included yet. Window movement, multiple monitors, scaling, occlusion and minimization require live testing.

**Auction House and other search boxes:** when fresh addon context identifies a registered, verified focused search field, dictation preserves the whole transcript as plain text with no chat prefix. Search text bypasses the chat classifier, so even words like “Tell guild” remain part of the query. Paste through your controller mapping. Unknown text fields are blocked; AH support still needs client verification.

Blizzard approval for this particular integration has not been established. See [policy notes](docs/POLICY.md). Whispers, automatic sending, continuous listening and assistant answers are outside this preview.

## Development and provenance

Forked from upstream commit `41f212558f4e9ba378ec44e9e6b9e44177b6a7be`. Upstream C# namespaces and some project filenames retain SpeakForever names. This fork uses its own app/settings identity. Upstream auto-updates are disabled.

```powershell
./scripts/Setup-Tools.ps1
# CMake 3.31.6 must be available; Python 3.12 is used for training/Lua tests.
./scripts/Build.ps1
```

Dependencies, NuGet graphs, native commits and model checksums are pinned. The normal build verifies downloads, builds native libraries, runs core/routing/classifier tests and packages one self-contained ZIP. `scripts/Train.ps1` retrains explicitly using held-out paraphrase families. See [controller tests](docs/CONTROLLER-TEST.md) and [measured component results](docs/TEST-RESULTS.md).

The original upstream `build.ps1`, installer, CLI and website are retained for provenance; use this fork's `scripts/Build.ps1`. CI compiles and tests source without automatically publishing installers. Prereleases are published separately with their validation status and checksums.