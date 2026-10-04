# Three-click controller acceptance tests

The current source implements RS start -> RS finish -> wait for Ready -> RS paste. Send remains the game's separate physical input. This is not a timed double-click. Unbind the game's RS ping action before testing. Preview 1 still uses external manual paste and does not contain this source change.

Runtime testing remains blocked on the development PC by Windows Smart App Control. Do not disable security or elevate the app to bypass restrictions. The following live checks remain outstanding:

- Starting with the stick held or reconnecting while held must not trigger recording or paste.
- Holding a button produces one action. Clicks within 250 ms are ignored; clicks during transcription never queue a paste.
- Third click pastes only a ready, owned clipboard draft into a foreground Wow/WowB window with fresh addon context and a verified focused chat/AH field.
- Changed clipboard, channel numbering, group/session, field, prefixes/limits, stale heartbeat, minimized game or another foreground app refuses paste and preserves the draft.
- Successful paste occurs once. A failed/partial Windows shortcut never retries automatically; inspect the field and paste manually if needed.
- Existing text is not selected or cleared. No Enter is generated. The separate game send control sends.
- Silence, failed transcription, invalid or oversized draft cannot reach paste. Editing and recopying invalidates the old ready state.
- Back/cancel, closing chat and controller disconnect cancel readiness. Keyboard dictation keeps manual Ctrl+V and cancel-ready behavior.

The companion previews the draft before paste. The addon observes the native field after paste; it does not read the clipboard or synthesize game keys. Verify the Forever compatibility probe and actual chat/Auction House fields before live use. Policy approval remains unverified; see POLICY.md.

The optional large addon preview is hidden by default (`/wvr preview on|off`). Chat focus can be established automatically when the keyboard-focus object matches the active chat edit box; API failures or unfocused chat block paste. Temporary unverified-Say mode requires the focused audience to be Say at paste time. Opening Say after recording is allowed; switching to a different audience is blocked.
