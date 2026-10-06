# Three-click controller acceptance tests

Preview 2 implements RS start -> RS finish -> wait for Ready -> RS open/paste. By default, press the game's A/send control separately. With **Auto-send on final stick click** enabled in Settings, the final RS click also sends after the addon confirms the pasted text and audience. Search submission remains manual. This is not a timed double-click.

**Disable controller pings in WoW's settings before playing.** Unbind the game's right-stick click ping action; otherwise recording, finishing, and pasting can each ping. Remove any conflicting left-stick action too: left-stick click cancels the companion's recording or ready draft.

Current source automatically discovers the visible addon strip if its saved location stops decoding (one background search at most every 30 seconds). No prefix verification or channel configuration is required with the default Classic mode. Routing uses explicit instructions first, then supported model inference, an open chat audience, Instance/Raid/Party membership, the selected chat audience retained while solo, and finally Say. Joining General alone does not make every message public. The addon reads current joined-channel IDs; do not configure General as always `/1` or Trade as always `/2`.

The latest source reads a fixed dark bottom-edge bridge automatically and handles physical display coordinates, clipped borderless-window bounds, and scaling. No calibration button or stored position is needed for the new addon. A freshly loaded addon reconnects on normal polling. A detached standalone draft can be rerouted from its retained transcript on the next deliberate click once fresh context arrives, without recording again or overwriting a clipboard changed by another program. Loading an updated addon still requires the game's normal UI reload/relog; the companion does not reload the game automatically.

The app has launched on the development PC and the user has tested the basic chat flow. These are acceptance checks for additional clients and controller configurations, not a claim that every case has been verified. Do not disable security or elevate the app to bypass restrictions.

- Starting with the stick held or reconnecting while held must not trigger recording or paste.
- Holding a button produces one action. Clicks within 250 ms are ignored; clicks during transcription never queue a paste.
- Third click pastes only a ready, owned clipboard draft into a foreground Wow/WowB window with fresh addon context and a verified focused chat/AH field.
- Changed clipboard, channel numbering, group/session, field, prefixes/limits, stale heartbeat, minimized game or another foreground app refuses paste and preserves the draft.
- Successful paste occurs once. A failed/partial Windows shortcut never retries automatically; inspect the field and paste manually if needed.
- Existing text is not selected or cleared. With auto-send off, paste leaves the game send control to the user. With it on, chat submits once only after matching text and audience readback. A search field must never auto-submit.
- Auto-send never starts when transcription finishes. Left-stick cancellation before submission stops the workflow; it cannot retract an already sent message. Chat cleanup must not discard remaining text or send twice.
- Silence, failed transcription, invalid or oversized draft cannot reach paste. Editing and recopying invalidates the old ready state.
- Back/cancel, closing chat and controller disconnect cancel readiness. Keyboard dictation keeps manual Ctrl+V and cancel-ready behavior.

The companion previews the draft before paste. The addon observes the native field after paste; it does not read the clipboard or synthesize game keys. Classic chat defaults use conventional prefixes and a conservative 200-byte cap without setup verification commands. Joined-channel numbers come from live context; unjoined destinations remain unavailable. Auction House and other search fields still require registered, confirmed field context. Policy approval remains unverified; see POLICY.md.

The optional large addon preview is hidden by default (`/wvr preview on|off`). Chat focus can be established automatically when the keyboard-focus object matches the active chat edit box; API failures or unfocused chat block paste. Temporary unverified-Say mode requires the focused audience to be Say at paste time. Opening Say after recording is allowed; switching to a different audience is blocked.
