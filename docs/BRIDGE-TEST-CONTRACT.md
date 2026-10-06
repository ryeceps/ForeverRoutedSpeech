> Historical pixel-bridge contract. The default path now uses [addon-local routing](ADDON-ROUTING.md); these checks remain legacy coverage.

# Addon connection regression contract

Connection changes must exercise the actual addon geometry and the detector used by Windows capture. A decoder-only test with perfectly placed synthetic bits is insufficient.

| Requirement | Check |
| --- | --- |
| One physical pixel per cell across resolutions and UI scales | `tests/addon_probe_test.py`: actual Lua frame scale, 5 resolutions × 3 UI scales |
| Both PixelUtil and physical-screen fallback work | Lua checks, including world-entry and display/UI events |
| Production detector finds the Lua output | `ActualLuaOutputIsFoundAtEveryResolutionAndUiScale` uses committed Lua wire/geometry fixtures projected into screen pixels |
| Original failure remains reproducible | `PreviousScaleFormulaReproducesMissingConnectionAt1200PixelsHigh` fails discovery at the previous 1.5625-pixel pitch, succeeds with corrected geometry |
| Scene colors, corruption and occlusion cannot connect | `DamagedOrCoveredSignalCannotBecomeConnectedContext` |
| Clipped window edge stays inside capture search | `InsetBridgeRemainsVisibleAndInsideAutomaticSearch` |
| Live connection is genuinely updating | Passive `--watch-context` requires stable session plus two advancing, checksummed heartbeats |

Run Lua checks with Python and `lupa==2.6`, then the core and routing suites:

```powershell
python tests/addon_probe_test.py
dotnet run --project tests/SpeakForever.Core.Tests -c Release
dotnet run --project tests/VoiceRouter.Tests -c Release
```

The Build workflow runs Lua checks before core tests. `--write-geometry` regenerates fixtures after intentional addon changes; review the change rather than updating fixtures to silence failures. `--addon-source <file>` permits proving the checks fail against an earlier addon revision.

After installation and a normal WoW UI reload, run the passive check while the game is foreground:

```powershell
dotnet run --project tests/Routing.Smoke -c Release -- --watch-context
```

This reads context only. It never focuses the game, presses keys, pastes or sends messages. Passing mock/raster tests establishes code behavior; it must not be reported as verified live controller behavior. Record live connection and third-click paste separately.
