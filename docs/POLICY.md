# Blizzard policy review — October 2, 2026

**This app has no verified Blizzard authorization. It must not be described as ToS-approved.**

## Current review finding

The full integration cannot be validated as compliant from the published sources checked today. Ordinary single-client dictation is not shown to be categorically prohibited. Direct EULA page retrieval returned HTTP 403: its official indexed clauses were inspected, but the entire current revision and any account-specific supplemental/beta terms were not independently retrieved.

[Blizzard staff's November 5, 2020 reply about voice input](https://us.forums.blizzard.com/en/wow/t/new-tos-update-question/709198) distinguishes a described single-client voice-to-key setup from the multi-client broadcasting policy. Staff decline to approve a specific program and suggest the Accessibility team. A [November 17 staff reply](https://us.forums.blizzard.com/en/wow/t/multiboxing-can-i-do-this-without-getting-banned/723917/3) similarly distinguishes broadcasting/gameplay automation but declines to approve a configuration. These are historical clarifications, not Forever-specific permission.

These interpretations replace an overly broad reading that every synthetic input is prohibited. They do not approve our whole integration:

| Mechanism | Assessment |
|---|---|
| Click to start/stop local whisper recording | No specific prohibition on standalone dictation found; controls the companion. |
| fastText proposes a destination and copies a draft | Drafting alone does not control the game; live-context collection is separate. |
| Player physically pastes and sends | Conservative implementation choice, not a no-ban guarantee. |
| Separate physical controller input directly bound to Enter | Distinct from automatic sending; actual mapping/client support matters. Does not authorize previous automatic paste or context-dependent remapping. |
| Automatic opening/replacing/pasting, then player clicks send | Unresolved external input automation. Neither blanket permission nor an explicit ban on all dictation insertion was established. |
| YOLO automatic sending after transcription | Closer to the broad automation restriction; no express authorization found. Not a claim Blizzard adjudicated this exact app. |
| Addon pixel context decoded externally | Deliberate collection of game-generated information; avoiding memory reads does not settle the bridge's status. Unresolved. |
| Auction House search query dictation | Drafting a query is distinct from purchasing/posting. Automatic insertion needs the same clarification as chat. |

The often-repeated one-input/one-action rule is not a blanket EULA exception. A native macro remains subject to the supported API and game restrictions; it does not authorize an external input chain. SpeakForever's existence, software license, or user reports are not Blizzard authorization.

The [August 5, 2025 staff announcement](https://us.forums.blizzard.com/en/wow/t/prohibitions-on-third-party-software/2142972) reiterates enforcement concerning client modification and security bypasses. Player replies below it are not official policy. No official Forever-specific voice-router exemption was located. Terms displayed by the actual client must still be checked for supplemental conditions.

The companion's third-click confirmation currently operates only inside its own test pad. Its game-input restriction remains unchanged. Tests validate behavior, not contractual approval.

## Existing implementation restriction

The [Blizzard EULA](https://www.blizzard.com/en-us/legal/08b946df-660a-40e4-a072-1fbde65173b1/blizzard-end-user-license-agreement), section 1.C.ii, prohibits unauthorized software allowing automated control of a game or part of a game, and broadly addresses unauthorized software facilitating game functionality. The [anti-cheating agreement](https://www.blizzard.com/legal/cd5930c0-2784-420c-a23d-1e0d6ff8599b/anti-cheating-vereinbarung) also addresses unauthorized third-party tools that change gameplay or collect information through the game.

A release-to-send chain that opens chat, selects text, pastes a dynamically routed draft, and presses Enter is external automation. No official exception approving this exact voice/chat design was located. A player starting the recording, using only one account, or accepting a YOLO checkbox does not itself establish Blizzard permission. The [input-broadcasting policy](https://news.blizzard.com/en-us/article/23558957/policy-update-for-input-broadcasting-software) concerns multiple clients; it is not permission for unrelated single-client automation.

The shipped app has therefore been changed to drafting only:

- The sending implementation and its Windows input-injection import have been removed.
- The YOLO checkbox is visibly unavailable pending authorization.
- Legacy `AutoSend=true` settings are ignored and always read back as false.
- The player pastes and sends manually. The app's global keyboard hook observes push-to-talk; it generates no game keystrokes.

Manual clipboard drafting removes the external sending mechanism. That is a narrower design, **not a blanket compliance guarantee**. The addon-rendered status strip decoded by an external companion also needs review because it deliberately transfers game context. Avoid assuming screen-based transport is permitted merely because it does not read game memory.

The official [UI Add-On Development Policy](https://eu.forums.blizzard.com/en/wow/t/wow-user-interface-add-on-development-policy/1642) requires addons to follow the EULA and lists WoWUI@blizzard.com as an addon developer inquiry contact. Request written clarification for the exact Forever design: local push-to-talk recognition, intent classification, addon-displayed group/guild/channel context decoded from screen pixels, clipboard drafting, and the proposed optional synthetic chat-input sequence. Include that there is no memory/process injection, network interception, multi-client broadcasting, continuous listening, gameplay control, or scheduled/repeated message sending. Absence of those mechanisms does not itself prove permission.

No inquiry has been sent. Any authorization would need to cover the actual mechanisms and Forever client, and be reviewed before adding a sending implementation back to the distributed app. API compatibility and policy authorization are separate gates.
