# Current addon compatibility

The current default uses plain clipboard speech plus a checksummed function-key control signal. Context is resolved inside WoW. No status strip, pixel capture, hidden EditBox, SavedVariables polling or calibration is used. The player opens a native field; the addon never opens, focuses, clears focus, hides or submits it.

The client screenshot confirmed `SetPreferredGamepadInteractTarget()` was forbidden during the previous native-focus handoff. The replacement removes that addon-originated open/focus path and slash callbacks. Tests make those native operations throw, while exercising actual shipped Lua routing. This removes the known path in code; it does not prove live protected-input compatibility.

A ready draft automatically pastes when the companion observes the player's native open-chat chord or Chat radial selection. RS remains available for an already focused field. A sends through WoW. No old setting or packet can enable automatic sending. Header/destination changes and internal function-key dispatch remain client acceptance checks, especially in combat and when edit boxes consume keyboard events.

The app caps speech at 200 UTF-8 bytes. Native field caps are unchanged; truncation is detected as mismatch, never marked ready or submitted. The app has no reverse acknowledgement and reports a request rather than confirmed delivery.

Install app and addon together, reload once, then follow [controller checks](CONTROLLER-TEST.md). Public Preview 2 is older; legacy pixel tests are historical only. [Input research](ADDON-INPUT-RESEARCH.md) records source precedents and their limitations.
