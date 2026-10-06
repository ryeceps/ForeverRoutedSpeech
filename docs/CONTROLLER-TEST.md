# Controller acceptance: native focus-preserving routing

Install the matching current app and addon, then reload WoW once. Public Preview 2 predates this protocol. Disable WoW's RS ping binding. Match the companion Open chat setting to the game's native chat-opening chord; default LB + RB + Down needs no change.

1. With chat closed, click RS and speak while moving.
2. Pause (default 1.5 seconds silence) or click RS again to finish. Wait for Ready.
3. Use the native open-chat chord. The ready draft should paste automatically without another RS click. Check the native audience/header and text, then press A to send.
4. Test selecting Chat from the native radial menu with a ready draft. Opening the menu must preserve it; selecting Chat requests paste.
5. If chat/search is already focused when transcription completes, RS requests paste; completing transcription alone must not paste. Native chat opened before Ready must not receive a delayed unsolicited paste.
6. LS cancels before delivery, including the native-opening settle wait. After delivery use the game's Back control to dismiss the native draft.

## Routing cases

- Solo ordinary speech → Say; party → Party; raid → Raid; instance → Instance.
- Change group before paste → current context.
- In General, anyone need a tank? → currently joined General ID; strip instruction.
- Ask in trade selling potions → joined Trade ID, including renumbering.
- Tell everyone around me we need help → Say even grouped.
- Explicit unavailable General/Trade/Guild → refuse route; choose another destination.
- In Officers, meeting tonight → joined custom channel, no inferred custom intent.
- Mentioning guild or an item alone → ordinary current/group destination.
- Previously selected Guild/numbered chat → retain unless overridden. Newly opened Say does not suppress group defaults.
- Focused empty AH/search → original plain words, manual search confirmation; no chat opening.
- Occupied field → preserve existing text when exact incoming body is found; never submit.

## Failure checks

Switching applications, changing clipboard, cancellation and partial input must stop delivery without retry. Corrupted/incomplete/replayed/expired signals must not route or mark ready. Test native field caps (e.g. shorter search boxes): a truncated paste must report mismatch, never claim complete text or send. Review and shorten manually.

No metadata should appear in the final field; the clipboard is always speech. Without addon/binding support, plain speech may paste without routing. The companion cannot acknowledge receipt. Verify Ctrl + Shift + F13–F19 dispatch, key-event/binding interaction, native header updates, combat behavior and the absence of protected-action warnings on the actual client.

No live game input was executed during development. Automated tests establish code behavior, not Forever's protected execution environment or gameplay impact.
