# Forever compatibility gate

**Policy gate:** Sending has been removed from the current app pending Blizzard authorization. The chat-input probe below is retained for possible future evaluation; it cannot enable sending in this build. API tests do not establish ToS compliance. See [policy review](POLICY.md).

The [Blizzard announcement](https://news.blizzard.com/en-us/article/24301145/world-of-warcraft-at-blizzcon-2026-news-round-up) identifies Forever as a separate client. It does not establish addon API compatibility. **No live Forever client has been tested by this build.** A successful desktop build or mocked Lua test is not client evidence.

1. On the actual Forever client, obtain the interface number using the game's permitted Lua console/chat command for `select(4, GetBuildInfo())`. Install the probe using `scripts/Install-Addon.ps1 -AddOnsDirectory '<Forever client>/Interface/AddOns' -Interface <observed number>`. No retail interface value is baked into the addon.
2. Enable the addon, run `/wvr report`, and capture the displayed build, boolean API results, channel triplet output, and observed slash constants. Confirm that `IsInGroup`, `IsInRaid`, `IsInGuild`, and `GetChannelList` behave across solo, party, raid, and instance transitions. Instance is a candidate only when `LE_PARTY_CATEGORY_INSTANCE` and that form of `IsInGroup` exist. Missing APIs or an unexpected channel shape freeze the heartbeat and make context stale.
3. Verify the rendered strip and its decoding on the companion. Drag it to a clear area. Only then enter `/wvr rendered`. The strip is 128 by 32 cells at four **UI units** per cell. The companion needs its top-left offset relative to the game client and its cell pitch in **physical pixels**; UI scale and monitor DPI affect those values.
4. Manually test each intended slash prefix with a harmless message in its correct audience. Then record `/wvr verify say`, `guild`, `party`, `raid`, or `instance`, individually. Prefix observation alone never enables routing. Unsupported prefixes remain disabled. No probe action sends chat.
5. Join the intended public/custom channels, inspect their actual IDs, and manually test a numbered prefix. Then `/wvr verify custom` enables numbered chat destinations for currently joined IDs. Channel numbers are resolved by name immediately before copying; they are never fixed to General=1 or Trade=2. English public names are conservatively recognized; other names are custom explicit/manual destinations.
6. Measure the actual client message limit with ASCII and multibyte Unicode and verify the entire slash-prefixed draft fits the input control. Record the verified payload limit via `/wvr limit bytes <n>` or `/wvr limit chars <n>`. Character mode counts Unicode code points. If the input control has a lower remaining payload capacity after its command prefix, record that smaller bound. Until a positive limit is recorded, copying is blocked. Nothing is silently truncated.
7. For focused Auction House search dictation, follow [controller testing](CONTROLLER-TEST.md) to verify the keyboard-focus API, named search field identity, and its own input limit. The companion copies plain text only while fresh evidence matches the field captured at recording start. Game paste/submit remain manual; local third-click confirmation cannot enable game sending.

Probe confirmations are SavedVariables **setup evidence**, scoped to the observed build. Live context never comes from SavedVariables. A changed client build automatically clears confirmations; `/wvr reset` clears them manually.

## Live validation checklist

Record actual results and the client build rather than marking this list passed from unit tests:

| Area | Live scenarios | Current evidence |
|---|---|---|
| Group/guild APIs | Solo → party → raid → instance, leave group/guild | Lua mocks and deterministic routing only |
| Channels | Join/leave, city/zone changes, renumber, custom and disabled channels | Mocked triplets; name/ID routing tests |
| Rendering/capture | Window movement, negative monitor coordinates, DPI/UI scaling, minimize, occlude, fullscreen | Bounds and foreground guards; synthetic geometry checks; live untested |
| Chat limits | ASCII, accents, emoji, control characters, full input length | UTF-8 and code-point length checks; client limit unverified |
| Microphone | Permission/driver failure, unplug, silence, cancel, 30-second cutoff | Error-handling code and native PCM/cancel tests; microphone untested |
| Clipboard | Locked clipboard, edits, other app changes | Contention handling; live untested |
| Controller and focused fields | Click ordering, held buttons, reconnect, changed focus, search limits | Deterministic and Lua mocks; physical controller/client untested |
| Performance | Key release → clipboard, inference, memory, FPS/frametime impact | Separate native benchmark; end-to-end/gameplay unmeasured |

## Wire format

All cells are black or white. Row-major bits are least-significant-bit first within each byte. The capacity is 512 bytes. The companion samples each cell center and rejects intermediate or colored values. The client region is configured explicitly; no OCR or full-screen scanning is used.

| Byte offset | Field |
|---|---|
| 0 | ASCII `WVR1` |
| 4 | Payload length, unsigned little-endian 16-bit |
| 6 | Per-load session, unsigned little-endian 32-bit |
| 10 | Heartbeat counter, unsigned little-endian 32-bit |
| 14 | UTF-8 payload, up to 494 bytes |
| After payload | Adler-32 over header and payload, little-endian 32-bit |

Version-1 payloads have nine tab-separated fields: version `1`, client build, group category, guild boolean, message limit, `bytes|chars`, semicolon-separated verified prefixes, semicolon-separated channel `id,kind,percent-encoded-name` records, and `closed|open|unknown` chat input state. UTF-8 channel names percent-encode every non-ASCII byte. Unused cells are zero. Oversized contexts freeze the heartbeat instead of dropping channels. The checksum detects corruption; it is not authentication.

Version 2 keeps the first nine fields (with version `2`) and adds four: focused-field kind (`none|auctionhouse|unsupported`), percent-encoded field identity, field limit, and `bytes|chars`. The addon emits version 2; the companion also accepts version 1 without field evidence. Unknown non-chat fields block routing rather than becoming chat drafts.

Sampling runs at four Hz. Only an advancing heartbeat refreshes freshness; identical decoded frames age out after two seconds. Decode errors invalidate context immediately. Capture requires a visible, restored, unambiguous game window and a valid strip within its client bounds. An unobscured background strip can support clipboard edits while the companion has focus; occlusion fails decoding. Game-mode click recording requires the configured game in the foreground. The third ready-draft controller click requests one guarded Ctrl+V shortcut; the app never sends chat or activates/restores a game window. This source change still requires live verification.

## Active chat and search defaults (protocol 3)

Version 3 extends the version-2 13-field payload with active chat destination and optional numbered-channel ID. The addon reads the active edit box's chatType/channelTarget attributes using protected calls. Verify these attributes on the Forever client while running /wvr chatinput; missing attributes fall back to Say, unsupported known audiences require manual selection. Version 1/2 frames remain readable but cannot report an active audience. Group membership no longer chooses the fallback audience.

Named Auction House and other search fields require their own manually checked limits and /wvr field bytes|chars <limit> registration. They bypass chat classification and keep the exact transcript as plain text. Unknown fields remain blocked. Update the companion and addon together, then reload the addon in the game.
