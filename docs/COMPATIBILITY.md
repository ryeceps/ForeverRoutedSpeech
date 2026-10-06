# Current addon compatibility checks

The default implementation resolves game context inside the addon and receives drafts through an invisible input inbox. It creates no visible status strip, captures no game pixels, and does not read live SavedVariables. No `/wvr verify`, `/wvr rendered`, `/wvr limit` or calibration commands are required.

Local APIs read group category, guild membership, joined channel IDs/names, zone/subzone/city/resting and the currently focused edit box. Unsupported APIs or unavailable explicit destinations refuse delivery. Drafts have a conservative 200 UTF-8 byte cap; the exposed native field limits and complete text are also checked.

The new path depends on the client accepting the internal override shortcut, an alpha-zero EditBox taking keyboard focus, native chat destination setters, and optional native Enter callbacks. Those are covered with real Lua source in mocks, but require live Forever acceptance tests. The app has no reverse acknowledgement and must not report successful routing/send based on Windows accepting its shortcut alone.

Install app and addon together, reload once, then follow [controller checks](CONTROLLER-TEST.md). Do not run the retired pixel probe commands to repair this path. Legacy pixel fixtures remain in `tests/fixtures` for historical tests; they are not packaged as addon modules.