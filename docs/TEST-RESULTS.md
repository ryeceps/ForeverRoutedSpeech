# ForeverRoutedSpeech earlier local verification — October 2, 2026

## October 4: close chat after optional submission

Auto-send now waits for chat closure after its one submit request. A fresh, changed heartbeat reporting an empty still-focused chat permits one Escape fallback. Remaining text, changed focus/context, cancellation, or stale evidence stops the close action; submission is never repeated. 174 core tests passed, covering natural closure, the empty-chat Escape fallback, remaining text, and focus loss after submission. Live game closure remains to be tested.

## October 4: optional final-click auto-send

Added an off-by-default Settings toggle. A deliberate final controller click can request chat opening, paste, and one submission after protocol-4 addon readback matches the text and intended channel. Transcription completion never sends. Search fields remain manual. Clipboard ownership, focused client, freshness/session, empty-field, echo and cancellation guards remain active. There is no retry after partial input.

Left-stick click cancels recording, transcription, a ready draft, or pending submission. It is reserved from dictation/open-chat rebinding. Disabling auto-send also cancels pending submission. An already issued Enter cannot be undone.

Core tests include the exact open/paste/submit sequence, old-addon rejection, existing text, partial paste, text mismatch, focus change, cancellation, readback timeout, Unicode, numbered audience, default-off behavior, and protocol-4 decoding. Lua mocks verify the new echo fields. Live auto-send is not yet tested; no Blizzard approval or server delivery is claimed. Earlier manual-only sections below describe previous builds.

## October 4: explicit numbered-channel speech

Leading `In General, ...`, `In Trade, ...`, `In LFG, ...`, and named custom-channel instructions now select the current joined channel number and remove only the instruction. Leading `General, ...` and `Say in Trade ...` also work. References elsewhere in the message and negated instructions remain ordinary speech. Missing/stale context and unjoined destinations do not fall back to Say.

The existing compatibility-skip opt-in now permits explicit joined numbered-channel drafts as well as Say. Prefix and game limit remain unverified in this temporary mode, public inference remains disabled, and the fallback draft cap is 4096 bytes. Normal verified routing still uses the measured client limit.

159 core tests passed, including 17 explicit-channel test cases. The 124 deterministic routing assertions and native integration smoke checks passed, including Trade renumbering, explicit Trade in temporary mode, and rejection of unjoined General. These are local component tests; live General/Trade delivery needs a player test.

## October 4: open chat and paste, separate A confirmation

The local app now records/transcribes with chat closed. A third deliberate controller click requests chat opening only if the addon reports closed chat, waits for confirmed focus, then requests one paste. A separate game confirmation press sends the text. Already focused chat/search fields receive paste only. There is no post-paste Enter or automatic submission setting in this build.

- 142 core unit tests passed, including 12 named open/paste workflow and context tests; see [the test contract](../tests/OPEN-PASTE-TESTS.md).
- 124 deterministic routing assertions passed.
- Lua 5.1 addon probe mocks passed.
- Release GUI build completed with zero warnings/errors, and the local self-contained package was rebuilt.

These results validate local decisions and simulated input requests. Live movement, chat opening/paste, and physical A confirmation still need testing in the running game. The older GitHub release ZIP has not been replaced by this local build. The deferred automatic-submit prototype was archived outside the build; it is not included.

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

## Active-panel/Say and search update

112 deterministic routing/protocol assertions passed, including active-chat defaults, Say while grouped, unsupported audiences, changed-panel paste guards and search transcript preservation. Real fastText/default smoke checks passed (19 checks including model/default checks; routing mean 0.035 ms, screen capture excluded). Lua 5.1 addon protocol and preview mocks passed. These runs completed normally with Windows security unchanged; the historical Smart App Control block above was from the earlier final run. Live Forever API/focus and controller paste verification remain outstanding. The addon and companion must be updated together for protocol 3.

