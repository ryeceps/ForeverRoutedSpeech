# Controller open-and-paste test contract

The controller sequence is record, stop/transcribe, then open/paste. Sending is a separate physical press of the game's confirmation button. These unit tests inject context and input operations; they never send Windows input, record audio, or send a chat message.

Source: `SpeakForever.Core.Tests/OpenPasteWorkflowTests.cs`.

| Test | Required behavior |
| --- | --- |
| ClosedChatOpensWaitsPastesAndStops | Request OpenChat once, wait for confirmed focus, request Paste once, then stop. |
| AlreadyOpenChatOnlyPastes | Request Paste only; no Enter request. |
| FocusedSearchOnlyPastesWithoutEnter | Paste plain text into a supported focused search field; no chat opening or submission. |
| StaleContextRequestsNoInput | Reject stale context before any keyboard operation. |
| FailureAfterOpeningDoesNotPaste | Context failure after opening stops the operation before paste. |
| OpenTimeoutDoesNotPasteOrRetryEnter | After 30 checks (normally 1.5 seconds), stop; do not repeat opening or paste. |
| CancelAfterOpeningDoesNotPaste | Cancellation prevents the next operation. |
| OpeningAllowsNewActiveChatButKeepsSessionGuard | Opening may select the game's last supported chat audience; changed game session or stale context still blocks. |
| TemporarySayDraftRequiresSayAfterOpening | An unverified temporary Say draft may open chat, but may paste only into confirmed focused Say. |
| RejectedChatOpenDoesNotPaste | A failed or partial chat-open shortcut stops before paste. |
| PartialPasteIsNeverRepeated | A partial paste is reported once and never replayed. |
| UnknownFocusAndChangedGroupOrFieldRemainBlocked | Unknown focus, changed group, unsupported panel, or a newly focused search field blocks input. |

Run from the repository with the pinned .NET 10 SDK:

```powershell
../../.tools/dotnet10/dotnet.exe run --project tests/SpeakForever.Core.Tests -c Release
../../.tools/dotnet10/dotnet.exe run --project tests/VoiceRouter.Tests -c Release
```

Unit tests establish the workflow decisions, not that WoW accepts external input. Live checks must confirm: movement during recording/transcription, third-click focus/paste, A sends exactly once and closes chat, supported search-field paste, and focus/context-loss refusal. No live send should be triggered by the test runner.
