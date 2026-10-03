# Controller click test

Run `artifacts/app/VoiceRouter.App.exe`. Local test mode is enabled by default. Select the microphone in Setup, then save and load models. Models remain loaded across recordings.

1. Click the right stick once: recording starts. F8 is the alternative toggle shortcut; releasing either button does nothing.
2. Speak, then click again: recording stops, whisper transcribes, fastText and routing prepare the draft. A ready draft replaces the test field's existing text and is copied to the clipboard.
3. Wait for Ready, then click again: the local test pad confirms chat or the search query. The next click starts another recording. Confirmation never happens automatically or from a click made during transcription.

Back or Escape cancels. Disconnecting the controller cancels. The 30-second cap stops recording even if game focus has changed. A held or bouncing button must not trigger repeated actions.

Choose Party chat or Auction House search before recording. Party chat supplies **simulated** available destinations and limits. Auction House search has a **test-only** 63-character limit and keeps the complete transcript without `/p` or another chat command. It confirms a search query only; it does not represent a purchase or bid. Changing the target discards the old prepared draft.

Without a microphone, edit the typed test phrase and click **Prepare typed test phrase**. This runs the routing stage (including fastText when loaded), stages the result, then lets a right-stick click or **Click stick (simulate)** confirm. Try explicit guild instructions, ordinary conversation, an unavailable Trade destination, and oversized search text. The test pad's visible result stays in memory; it is not written to a transcript log.

Direct controller input supports Windows XInput devices. For a different controller, map a physical right-stick click to F8 using your existing controller mapping and uncheck direct XInput reading to avoid duplicate events. Keyboard shortcuts are observed and passed through; the companion does not inject input or alter game controller bindings. Pick an unused shortcut and check game bindings before live recording.

## Game mode boundary

The addon now includes a read-only preview of the currently active native chat field, or a registered Auction House search field. In WoW Key Bindings, Voice Router exposes **Open chat / show draft preview**. Bind that game action to your preferred controller input using the client's supported gamepad binding mechanism. Opening preserves an already active field and its contents. The separate controller-to-Ctrl+V mapping is assumed configured externally: the addon observes the resulting text, not raw Windows keys, and cannot identify whether a change was typed or pasted. Its preview refreshes four times per second and clears when the supported field closes. Confirm/send still uses the game's own control; the preview never sends, replaces field text, or accesses the clipboard. This addon behavior is Lua-mock tested only and must be verified on Forever, including combat restrictions.

Unchecking Local test mode uses real addon context and requires the configured game window in the foreground to begin or finish a recording. After transcription, a valid draft is copied. The third click revalidates and copies it, but **does not paste or press Enter in WoW**. Those game input operations remain disabled pending the policy review described in [POLICY.md](POLICY.md). The working local test is not evidence of Blizzard approval or a tested Forever integration.

For manually replacing existing text, use Ctrl+A then Ctrl+V, followed by your physical send/submit input. Ctrl+X would replace the clipboard with the old field contents and could lose the prepared transcript. The local test pad replaces its own text directly; it does not generate synthetic Ctrl+A, Ctrl+V, or Enter.

The addon can report a named focused field using the candidate `GetCurrentKeyBoardFocus` API. Focus an Auction House **search** edit box and allow one strip update, manually verify its input limit, then record `/wvr field chars <limit>` or `/wvr field bytes <limit>`. This is build-scoped manual evidence, not automatic API verification. Only explicitly registered field identities receive plain-text drafts; other observed non-chat fields are unsupported. `/wvr reset` clears registration. Missing focus APIs, unnamed controls, rendering, scaling, minimization, and changed fields still require actual Forever-client tests. Version-1 strips do not provide focused-field evidence.

## Local evidence

The packaged WPF build compiles. The deterministic suite includes click ordering, ignored early confirmation, cancellation generations, invalid edits, button edges/bounce/reconnect, plain-text preservation, changed field identity, Unicode limits, and protocol version 2. Lua 5.1 mocks exercise field registration and reset. Hardware microphone/controller interaction and real game behavior remain untested.
