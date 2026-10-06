# Current addon compatibility checks

The default implementation resolves game context inside the addon and receives drafts through an invisible input inbox. It creates no visible status strip, captures no game pixels, and does not read live SavedVariables. No `/wvr verify`, `/wvr rendered`, `/wvr limit` or calibration commands are required.

Local APIs read group category, guild membership, joined channel IDs/names, zone/subzone/city/resting and the currently focused edit box. Unsupported APIs or unavailable explicit destinations refuse delivery. Drafts have a conservative 200 UTF-8 byte cap; the exposed native field limits and complete text are also checked.

The player's screenshot now confirms a protected gamepad-focus failure: `SetPreferredGamepadInteractTarget()` is forbidden while our addon prepares chat. Automatic sending has been removed, but native chat opening/focus changes also need replacing. The addon stops on a reported protected action rather than claiming readiness. See [input research](ADDON-INPUT-RESEARCH.md) for the Forever-specific source chain and the limits of the current containment fix. No optional native Enter callback remains.

The path still depends on the client accepting the internal override shortcut, an alpha-zero EditBox taking keyboard focus and native chat destination setters. Mock coverage does not establish live support. The app has no reverse acknowledgement and must not report successful routing/send based on Windows accepting its shortcut alone.

Install app and addon together, reload once, then follow [controller checks](CONTROLLER-TEST.md). Do not run the retired pixel probe commands to repair this path. Legacy pixel fixtures remain in `tests/fixtures` for historical tests; they are not packaged as addon modules.
