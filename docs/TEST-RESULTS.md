# ForeverRoutedSpeech earlier local verification — October 2, 2026

- 130 upstream/fork core tests passed, including controller edges, microphone endpointing, clipboard handling, settings, downloads and Turbo-only behavior.
- 91 deterministic routing assertions passed against the fork's routing core.
- 19 real-classifier/default checks passed: persistent fastText-only loading, explicit destinations, trained guild address, numbered-channel changes, group transitions, invalid/oversized text, Unicode, AH plain text, missing/stale context, manual confirmation and Turbo-only defaults.
- Lua 5.1 probe and preview mocks passed: framing/checksum, modern and legacy chat focus, API failures, heartbeat, renumbering, Unicode, search-field preview, and no addon clipboard access or sending.
- Training isolation and fail-closed threshold checks passed. Public inferred destinations remain disabled; the bootstrap corpus does not establish 95% held-out precision.
- Actual native routing averaged about 0.05 ms over 100 calls; this excludes screen capture.
- Actual Whisper Turbo q5_0 recognition of a three-second 16 kHz mono JFK sample took 0.355 seconds on Intel UHD Graphics through Vulkan. First model load/warm-up took 48.48 seconds; this is separate from per-recording recognition. Peak process working set was approximately 665 MiB, excluding GPU memory.
- The WinUI app builds and publishes with zero warnings/errors. The package includes its .NET runtime, model and inference dependencies, plus Microsoft's signed, checksummed VC runtime installer for machines that need it.

These are local component/mock results, not a Forever gameplay test. Release-to-copied-draft latency with the actual microphone, gameplay impact, VRAM use, gamepad mapping, Forever addon/API/message-limit compatibility, capture under movement/scaling/occlusion, and visual QA of the running WinUI windows remain unmeasured. Source is published on GitHub. No Blizzard approval or successful final runtime validation is claimed.

## Final validation blocker

After the results above, Windows Smart App Control blocked the rebuilt `SpeakForever.Core.dll` during the final core test run (Code Integrity events 3033/3077, error 0x800711C7). The final source includes later clipboard-error handling and capture-settings cleanup which have not completed runtime validation. The local package is an unsigned preview, not an accepted runnable release. Security policy was not disabled or bypassed. Signing/trust and the live Forever tests remain outstanding.

## Three-click paste source change

Added separate start/finish/paste controller stages, 250 ms click suppression, disconnect/held-reconnect guards, fresh matching focus/context checks, owned-clipboard checks and a single Ctrl+V request without Enter. Added deterministic context-transition assertions. These new tests are compiled only; runtime tests and live paste checks remain outstanding under the existing Smart App Control blocker. Preview 1 does not include this change.
