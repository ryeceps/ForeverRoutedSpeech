# ForeverRoutedSpeech earlier local verification — October 2, 2026

## October 6: one-way inbox and addon-local context

Paste regression follow-up: the previous inbox rejected an initial partial OnTextChanged callback, dropping focus before the full packet arrived. A fragmented-paste test fails against the committed pre-fix Inbox.lua and passes with the 100 ms quiet-interval fix. The companion also no longer restores clipboard text immediately after queued Ctrl+V; the packet remains stable until the next deliberate copy. These are reproduced code-level failures/risk fixes; the user's exact live event sequence has not been observed.

The default path no longer captures pixels or requires heartbeat/setup evidence. The app produces text-only intent hints and delivers a framed/checksummed packet to an invisible addon inbox on the final physical click. The addon resolves current group/guild, joined channel IDs and focused text targets locally. Legacy pixel renderer/preview files are removed from installation and packaging, and are retained only as historical test fixtures. The normal clipboard stays human-readable; no game context travels back to the app.

221 core tests and 124 deterministic routing assertions passed before final packaging, including cross-language Lua-accepted packet vectors and prepare/paste/cancel workflow guards. The actual shipped Lua modules pass inbox/routing tests for groups changing at delivery, renumbered General/Trade, custom/manual destinations, unavailable explicit refusal, unavailable inferred fallback, occupied fields, corrupted metadata/text, Unicode/limits, unexpected truncation, duplicate IDs, cancellation/expiry and optional native send. Search never auto-submits. Native model checks now cover default addon mode with no context capture and text-only intent, including a guard against routing ordinary 'Hello friends' to Guild.

The classifier was retrained using fixed paraphrase-family splits, text-only features and a direct Guild-address feature. The authored held-out sample emitted 5 Guild routes, all correct, across only 2 families; this is sparse bootstrap evidence, not a player accuracy claim. Public inference remains disabled despite 16/16 candidate held-out routes because the independent-family/emission gate is unmet. Explicit public destinations continue to work. Model checksum and policy were updated together.

Local mocks/native checks do not establish Forever's invisible-edit-box focus, override shortcut or protected Enter-handler compatibility. No computer-use tools or live game input were used. The companion has no reverse acknowledgement channel and reports a delivery request, not confirmed routing/server delivery. Live three-click chat/search behavior requires a player test after the normal addon reload. The public Preview 2 archive still predates this protocol.

Final Windows packaging succeeded. All 30 native routing smoke checks passed: mean 1.123 ms, maximum 16.731 ms, with game capture excluded. This measures routing only, not recognition, end-to-end latency or gameplay impact. The matching addon was installed in the Classic beta AddOns directory and the rebuilt companion restarted; the installed Inbox.lua hash matches the source. One normal game UI reload remains necessary to load the updated addon.

## October 5: normalized UI scale regression reproduced

The earlier placement/palette fixes did not establish a live connection. The addon used `1 / UIParent:GetEffectiveScale()` as if one unscaled UI unit were one screen pixel. Blizzard's PixelUtil source converts pixels to UI units using `768 / physicalHeight`; the omitted factor made a nominal one-pixel cell 1.5625 pixels wide at 1200 pixels high. The fixed addon applies that factor through PixelUtil, with a GetPhysicalScreenSize fallback, and refreshes on display/UI changes and world entry.

Evidence: running the new physical-geometry assertion against the actual pre-fix addon fails. The corrected Lua addon passes at 768, 1200, 1222, 1440 and 2160 physical pixels high, each at UI scales 0.5, 0.75 and 1. Its generated wire frames and frame scales are saved as synthetic fixtures. `BridgeDiscoveryRegressionTests` projects those actual Lua results into simulated screen pixels and runs `BridgeLocator.Find`, the same detector now used by Windows capture. It reproduces a failed connection with the old 1.5625-pixel pitch and succeeds with the corrected output. Corruption, scene colors and occlusion remain rejected.

201 core tests, 124 deterministic routing assertions, Lua checks and packaged native smoke checks passed. CI now runs Lua checks and verifies fixture geometry/wire fields before core tests. Local app and installed addon were rebuilt/updated. These results validate the regression and detector fix; they do not by themselves establish a live Forever-client connection or successful controller paste. The passive live probe additionally requires two valid frames with a stable session and advancing heartbeat.

Reference: [Blizzard UI PixelUtil source](https://github.com/Gethe/wow-ui-source/blob/live/Interface/AddOns/Blizzard_SharedXML/PixelUtil.lua).

## October 5: compact high-contrast bridge

The user's subsequent test still failed: the dim decoder rejected RGB 26,26,26 at bit 31 and the third click remained blocked. The wide 1024 × 16 strip is replaced by a 128 × 32 physical-pixel signal using the original full black/white encoding. This uses one quarter of the previous area and retains the 512-byte capacity, protocol 5 context, checksum, and current routing. A 16-pixel left inset and 32-pixel bottom inset protect against clipped window edges. Automatic discovery recognizes compact and wide layouts; no user calibration is added.

Verification: Lua addon wire/placement checks, 198 core tests (including compact decoding with a raised black level of 26), 124 deterministic routing assertions, and the local packaged native smoke checks. Local app rebuild and installed addon update completed. Actual client capture and third-click paste remain unverified without the user's game test. Public release archives have not been replaced.

## October 5: bridge inset for offscreen client bounds

Reported capture failures used a 1938 × 1222 client whose bottom extended beyond the visible monitor. An addon anchored at the actual client bottom could put all 16 signal rows offscreen; clamping the capture coordinate alone could not repair that. The addon now places the signal 32 physical pixels above its bottom anchor. Capture uses the matching inset and searches the bottom 96 pixels to allow for the difference between client and monitor bounds.

Lua 5.1 addon checks passed; 197 core tests passed, including normal and 22-pixel clipped client placement. The local app was rebuilt and the installed addon updated. Live connection and controller paste still require testing in the game; no computer automation was used to operate the game.

## October 5: automatic edge bridge and detached-draft recovery

The movable checkerboard is replaced by a dark bottom-edge bridge with two-pixel cells. Protocol 5 includes zone, subzone, city and resting-area context, while retaining protocols 1–4. City-map flags are used when available, with an English Classic capital fallback; resting alone does not imply a city. The normal Home workflow no longer shows a calibration button. Capture uses physical coordinates, clips off-screen window bounds to the monitor, searches the bottom edge at supported scaling pitches, and caches a decoded location. UI/display scale events update the addon scale.

A standalone draft created before addon connection can recover its original transcript and routing on a subsequent deliberate controller click. The clipboard is recopied only while it still belongs to the app; a failed clipboard update leaves recovery retryable. Explicit unavailable channels are not converted to Say. Model features remain unchanged pending location-aware training/calibration.

195 core tests, 125 Lua-wire/routing assertions and the native classifier integration checks passed locally; the GUI package was rebuilt. The new edge palette/layout, old bright layout, location decoding, absent legacy location, corruption rejection, scale-event handling, and detached-context recovery are covered. Live third-click paste is **not verified**: computer control was stopped, then prohibited by the user. Foreground-only diagnostic watching saw no foreground game context during its observation window. The public Preview 2 ZIP has not been replaced with this local build.

## October 5: automatic group and selected-channel context

Ordinary closed-chat speech now defaults to Instance → Raid → Party when available. Solo speech retains the selected supported chat audience even with the edit box closed, falling back to Say if no target is selected. Explicit instructions still win and resolve current joined channel IDs. The addon supports both modern chat methods and legacy attributes/last-active APIs; remembered whispers and departed channels do not become targets. The companion searches for a moved/missing strip in the background, at most once every 30 seconds, while rejecting invalid capture context.

191 core tests and 124 deterministic routing assertions passed. Lua 5.1 mocks cover modern/legacy closed-chat selection, General renumbering, channel departure, and unsupported audiences. Native integration checks exercise group transitions with the real classifier. The updated addon requires a UI reload; these tests do not establish live Forever delivery for the new behavior.

## October 4: Classic chat defaults and reasonable draft cap

Added default Classic-style prefix assumptions and a 200 UTF-8 byte app cap, honoring smaller recorded limits conservatively. This removes the setup-skipped draft label with fresh live context. Actual joined channel numbers, membership, focus/readback, session and heartbeat remain required; unjoined destinations stay unavailable. Assumptions are applied locally and do not mark the addon's compatibility evidence as verified. 184 core tests passed, including Unicode boundaries, live renumbering, unjoined destinations, stale context and search-limit preservation.

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
