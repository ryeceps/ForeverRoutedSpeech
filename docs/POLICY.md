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

## Current controller paste change

The source now supports three separate physical recording-button clicks: start, finish, then paste after Ready. The third click generates a single Ctrl+V shortcut into the foreground game, guarded by unchanged clipboard ownership and fresh, matching addon context. It never opens chat, selects/clears existing text, presses Enter, repeats input, or retries a partial shortcut. Sending remains a separate physical game input.

This changes the earlier clipboard-only restriction at the user's request. It does not establish Blizzard approval. Companion-generated paste and the addon context bridge remain unresolved policy questions. Preview 1 predates this change; the new source has compiled but has not completed runtime validation because Windows Smart App Control blocked earlier final runtime tests. No security policy was bypassed.

The EULA and policy links above remain relevant; the old blanket statement that this app generates no game input no longer describes the new controller-paste source. Keyboard dictation still supports external manual paste.
