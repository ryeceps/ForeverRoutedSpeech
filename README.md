# ForeverRoutedSpeech

A fork of [samsbase/SpeakForever](https://github.com/samsbase/SpeakForever), adding local audience routing to controller speech-to-chat. The upstream Windows/controller foundation, MIT attribution and Git history are preserved. This is an independent fork.

## Current build versus public download

**Current source uses addon-side routing with no visible status strip, screen capture, calibration or context verification commands.** The public [Preview 2 download](https://github.com/ryeceps/ForeverRoutedSpeech/releases/tag/v0.2.0-preview.1) predates this redesign. Do not combine that older app with the new addon. The current local development build and installed addon are updated together; a new public archive has not yet been published.

The Windows x64 app uses Whisper **Turbo q5_0 only**, with Vulkan where available and CPU fallback. Speech and classification stay local; no API key or model picker. Normal operation is offline. Model/native dependencies and checksums are pinned. No audio or transcript history is retained by the app.

## Controller flow

1. Click the right stick to start recording. Keep moving with chat closed.
2. Click it again to finish. Wait for Ready. Pause-finishing is available when `FinishOnPause` is enabled.
3. Open chat with WoW’s native controller command (default LB + RB + Down). The ready draft pastes automatically; no extra right-stick paste click is needed.
4. Review the native chat header and press A to send.

For an already focused empty Auction House/search field, click the right stick when Ready to paste, then confirm the search yourself. If chat is already open when your draft becomes ready, the same right-stick paste action is available. Transcription completion alone never triggers delivery.

Left-stick click cancels before delivery. It cannot retract a sent message. Disable WoW's controller ping binding for right-stick click, otherwise each dictation click can ping.

The addon creates no visible box. The companion uses a small, static recording/transcription badge and expands only for the draft preview. Audio cues default off; the optional overlay can be disabled in Settings. Updating addon files requires one normal WoW UI reload or relog; there are no calibration or prefix/limit setup commands.

## How the whole stack works

![How ForeverRoutedSpeech works: controller speech, local transcription, native chat opening with automatic paste, then manual confirmation.](docs/images/how-it-works.svg)

The native chat header shows the final audience; the companion preview shows a suggestion. Install the matching app and addon, then reload WoW once.

### What happens behind the scenes

- The **Windows app** watches the controller and microphone. Whisper Turbo turns audio into text; WoW vocabulary hints help recognition, but real pronunciation accuracy still needs speech testing.
- **fastText** suggests an audience from message text alone. It no longer receives invented group/guild/channel context. Guild suggestions require address language. General, Trade and LFG intent suggestions are enabled normally; weak scores return to live-context defaults.
- The companion shows the original message and suggested audience, and copies human-readable text. It does not claim to know the game's final destination.
- A deliberate native chat-opening action, or a right-stick paste click in an already focused field, requests one delivery. The clipboard stays plain speech. A short Ctrl + Shift / F13–F19 sequence carries only versioned metadata: nonce, intent hint, length and checksum. It never sends Alt, F1–F12 or a send key. The addon assigns these internal bindings automatically.
- The **addon** snapshots the already focused native field, validates the control signal, then waits for the plain paste to settle. It checks UTF-8 byte length, checksum, duplicate ID and expiry. It reads party/raid/instance, guild, joined channel IDs/names and location locally. No game context leaves WoW.
- It removes only recognized spoken routing instructions and updates the native chat audience and text. It never opens chat, takes or clears focus, hides chat, invokes its Enter handler, or sends a message. Search receives the original transcript without routing metadata.
- The native A/send control submits and closes chat through WoW. The old auto-send setting is ignored and removed from Settings.

There is no addon box or pixel strip. The companion has no reverse acknowledgement: accepting Windows input does not establish receipt or successful routing. The new function-key dispatch and destination changes still need player validation on Forever after one addon reload. No live game input was used during development.

## Routing examples

| Speech / live situation | Final addon result |
| --- | --- |
| Solo: `Hey guys`, close friendly player confirmed | Say |
| Solo: `Hey guys`, no close player confirmed and General joined | General |
| Recent General speaker Perilous: `Wow, thanks Perilous` | General reply |
| Clear selling / group-finding message | Trade / LFG suggestion, if joined |
| Party: `Hey guys` | Party |
| Instance group / Raid | Instance first, then Raid, then Party |
| `In General, anyone need a tank?` | Currently joined General ID, with the instruction removed |
| `Ask in trade selling potions` | Currently joined Trade ID; never hardcoded to `/2` |
| `Tell guild hello` | Guild if available; otherwise refuse |
| `Tell everyone around me we need help` | Say even while grouped |
| `In Officers, meeting tonight` | Joined custom channel by explicit name |
| Auction House/search already focused | Plain transcript; never auto-submit |

Explicit requests and manual corrections take priority. A supported selected chat audience suppresses reply guesses; otherwise recent reply context outranks model hints. Qualified model suggestions can override the selected audience. Group defaults are Instance → Raid → Party. Ordinary solo speech prefers Say when a close friendly player is confirmed; otherwise it uses joined General, then Say. Merely mentioning an item or guild is insufficient. Unavailable explicit channels refuse delivery rather than silently changing the audience.

Messages are capped at **200 UTF-8 bytes** in the companion. The addon refuses incomplete or mismatched text and unavailable explicit destinations. Existing field text is protected when the matching pasted body can be identified. A native search field can enforce a smaller limit during paste; the addon cannot recover missing characters and will report a mismatch instead of routing or submitting. Review the field and edit shorter text. No automatic retraining happens during play.

## The routing code and fastText

Whisper supplies a transcript. [DraftRouter.cs](app/Routing/DraftRouter.cs) lowercases/normalizes it and calls the persistent native fastText model. [IntentHints.cs](app/Routing/VoiceRouter.Core/IntentHints.cs) accepts a suggested audience only when its score and lead over the runner-up pass the checks. Scores are model outputs, not measured probabilities of being right. General/Trade/LFG use at least 0.80 with a 0.10 lead; Guild uses 0.60 with the same lead plus guild-address language. A default winning label or weak score emits no audience hint.

The model is trained from labelled WoW phrases in [training/corpus.json](training/corpus.json). It learns numerical representations for words and adjacent word pairs, using 32-dimensional features and a softmax classifier. [training/train.py](training/train.py) fixes the training options; paraphrase families remain separate across train, validation and test. It does not generate or rewrite the message, read the screen, learn during play or see live chat. The authored bootstrap remains small; the original held-out public results were 16 correct routes from 7 families, which does not establish real-player precision.

The addon makes the final contextual decision. [ReplyContext.lua](addon/VoiceRouter/ReplyContext.lua) listens to normal chat events, keeping only up to 32 in-memory speaker/topic cue sets for 45 seconds. It excludes your own messages, custom channels and restricted payloads. A speaker-address match or reply with shared topic words is strong evidence; a generic reply can follow a single recent audience for 25 seconds when solo. Competing audiences fall back. Expired messages, zone changes and departed channels cannot pin an old destination.

[LocalRouter.lua](addon/VoiceRouter/LocalRouter.lua) resolves explicit channel instructions, availability, replies and defaults. It checks friendly player targets, mouseover and available nameplates for a positive close interaction-range result. That is a proximity hint, not a complete player census or a precise Say-range measurement. Missing range support means proximity is unknown, so the routing preference uses General if joined. Nearby NPCs, hostile players and your own character do not select Say. Replies retain their actual channel even when another player stands nearby.

This event-based implementation follows the [Forever chat event definitions](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/ChatInfoDocumentation.lua) and [interaction-range API definitions](https://github.com/Gethe/wow-ui-source/blob/forever/Interface/AddOns/Blizzard_APIDocumentationGenerated/PlayerScriptDocumentation.lua). Source/API presence and passing fixtures still require player validation on the installed beta client.

## Build and installation

Use this fork's scripts, rather than the retained upstream build scripts:

```powershell
./scripts/Setup-Tools.ps1
./scripts/Build.ps1
./scripts/Install-Addon.ps1 -AddOnsDirectory 'C:/Program Files (x86)/World of Warcraft/_classic_beta_/Interface/AddOns' -Interface 16001
```

Use the actual client's AddOns directory and Interface number; 16001 is the observed beta value, not a promise for future builds. The installer removes the legacy renderer and preview, and replaces the old bindings file with an empty loader compatibility file. Reload WoW once after installation. If updating a running client produces a missing-file warning, fully restart WoW to refresh its addon file list. The portable package includes the .NET runtime, native libraries, pinned Turbo model, classifier and VC runtime prerequisite; normal operation needs no separate model download.

[Stack and tests](docs/ADDON-ROUTING.md) explain the implementation and its limits. [Controller acceptance checks](docs/CONTROLLER-TEST.md) cover live testing. [Test results](docs/TEST-RESULTS.md) distinguish mock/native tests from actual client evidence. [Policy notes](docs/POLICY.md) retain the prior review; this redesign is not a claim of Blizzard approval.

Forked from upstream commit `41f212558f4e9ba378ec44e9e6b9e44177b6a7be`. Upstream C# namespaces/project names remain where useful. Upstream auto-updates are disabled. This source is experimental and unsigned; the current function-key transport and native field routing require player validation.
