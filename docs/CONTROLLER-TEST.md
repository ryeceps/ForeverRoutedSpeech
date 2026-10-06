# Controller acceptance checks: addon-local routing

Use the matching current app and addon. Public Preview 2 predates the one-way inbox. After updating the addon, reload WoW once. Disable the game's right-stick ping binding. No capture, prefix or channel setup is required.

## Basic chat

1. Keep native chat closed. RS starts recording; move while speaking.
2. RS finishes; wait for the companion's editable, human-readable draft. It shows Auto (addon) or a suggested audience, not confirmed game context.
3. RS delivers once through the invisible inbox. The native chat header must show the final audience. A sends separately.
5. LS cancels recording/ready delivery before it is committed. After a native draft exists, use the game's Back control to dismiss it. An already sent message cannot be retracted.

## Named routing cases

- `solo_default`: Hey guys → Say.
- `party_default`: same words while in a party → Party; joined General alone must not override it.
- `group_transition`: change group after transcription, before final RS → current Instance/Raid/Party, not an old app snapshot.
- `explicit_general`: In General, anyone need a tank? → currently joined General ID; instruction removed.
- `channel_renumbering`: rejoin/renumber General or Trade → actual current ID.
- `explicit_say_override`: Tell everyone around me we need help while grouped → Say.
- `explicit_unavailable`: unjoined Trade/General or absent Guild → no fallback send; choose another route in the companion.
- `custom_channel`: In Officers, meeting tonight → joined Officers; no inferred custom-channel intent.
- `guild_reference`: I mentioned the guild yesterday / Hello friends → group or Say, not Guild.
- `open_chat`: empty supported native chat already focused → preserve that audience unless an explicit/manual/model request overrides it.
- `occupied_field`: existing chat/search text remains unchanged; packet metadata must not enter that final field.
- `search`: already focused empty AH/text field → original plain transcript, including words like Tell guild; A/search confirmation remains manual even with a legacy send flag.

## Failure and lifecycle checks

Changing clipboard or switching away from the recognized foreground game must prevent delivery. A failed/partial Windows shortcut must not retry. Missing addon/unsupported shortcut/invisible-field focus cannot be confirmed by the app because the handoff has no reverse acknowledgement; inspect the in-game field/status, not an app success label.

Damaged, incomplete, expired or recently repeated packets must not route/send. Missing local group/channel APIs must refuse chat. Oversized, multiline or leading-command speech requires editing; no truncation. Unsupported native Enter callbacks/protected actions must preserve a draft for manual send. Only an empty native chat box may be hidden after submission.

Actual Forever keyboard focus, override bindings, combat behavior, protected send, controller cancellation and game performance remain live acceptance work. Tests run Lua mocks and native inference without operating the game. See [stack and regression contract](ADDON-ROUTING.md).
