# Chat input research — October 6, 2026

The current replacement is described in [native-field routing](ADDON-ROUTING.md). It uses no hidden edit box or addon-originated native focus/open operation. Native chat opening by the player triggers automatic paste of an already ready draft; sending stays manual. Function-key transport and destination/header changes require Forever player validation. Source precedents below explain the design, not a compatibility or policy guarantee.


## Confirmed function from the player's screenshot

The screenshot reports `SetPreferredGamepadInteractTarget()` with `ADDON_ACTION_FORBIDDEN`, followed by an incorrect "say draft ready" notice. This identifies a gamepad focus/binding restriction, not a SendChatMessage refusal.

The [Forever-specific edit-box source](https://github.com/Gethe/wow-ui-source/blob/a84e2b1b41d3d4137127c07e4da448aa3251d6f1/Interface/AddOns/Blizzard_ChatFrameBase/Shared/ChatFrameEditBox.lua#L397) calls `self.chatFrame:SetGamepadFocus()` on edit focus gain and `ClearGamepadFocus()` on loss. Its [main action-bar source](https://github.com/Gethe/wow-ui-source/blob/a84e2b1b41d3d4137127c07e4da448aa3251d6f1/Interface/AddOns/Blizzard_GamepadActionBars/MainActionBarFrame.lua#L255) calls the named protected function while updating interaction icons. Our inbox steals/restores keyboard focus and opens native chat; these operations can enter that chain. The screenshot alone does not distinguish the first offending operation.

A [first-hand Forever beta report](https://us.forums.blizzard.com/en/wow/t/wow-forever-gamepad-ui-bugs-build-160170170/2370253) describes the same function being blocked after addon chat cleanup and reports IM chat style as a workaround. This is a player report, not a Blizzard-confirmed fix or proof that changing chat style makes our whole bridge work. Ultimate Castbars' [maintainer changelog](https://www.curseforge.com/wow/addons/ultimate-castbars/files/9053859) likewise attributes this warning to clearing Blizzard edit-box focus and restricts its focus cleanup to its own controls.

The follow-up removes the redundant native `SetFocus` during field fill and stops on a reported protected action instead of displaying "draft ready." A production-Lua regression simulates the forbidden event during chat opening and requires no fill, no send and no readiness notice. This is containment, not proof that automatic native chat opening is repaired. Replacing native focus/open manipulation with a player-owned game action remains the needed compatibility change; there is no claim that pcall bypasses protection.

The useful distinction is preparing text versus submitting a message. A callback running after transcription/paste is not evidence of a current hardware-event context. The reported Blizzard disable-addon popup is a protected-action warning; it does not identify the exact failed operation or establish a TOS ruling by itself.

## Source comparisons

| Project / primary source | Observed implementation | What it tells us |
| --- | --- | --- |
| [ConsolePort keyboard](https://github.com/seblindfors/ConsolePort/blob/master/ConsolePort_Keyboard/View/Keyboard.lua) | `Keyboard:Insert` inserts into its focused edit box. `Keyboard:Enter` executes that field's Enter script through its controller action. | Controller text entry and explicit submission are separate actions. Copying its Enter call into our delayed callback does not reproduce its input context. |
| [ConsolePort focus observer](https://github.com/seblindfors/ConsolePort/blob/master/ConsolePort_Keyboard/Core/Observer.lua) | Excludes forbidden and anchoring-restricted focus frames. | We should refuse restricted fields rather than trying to manipulate them. These guards are now in our addon. |
| [Prat CopyChat](https://github.com/Legacy-of-Sylvanaar/prat-3-0/blob/master/modules/CopyChat.lua) | A copy-link action opens chat with text or sets text in the active chat edit box. | Addon draft preparation has precedents. This does not prove our timer-driven invisible-inbox path works on Forever. |
| [MessageQueue](https://github.com/LenweSaralonde/MessageQueue) | Queues restricted chat messages and waits for a hardware event to run them. Its documented public targets include Say, Yell and numbered channels. It notes that capturing gamepad/mouse input can consume gameplay actions. | A later physical action can matter. Do not send a waiting queue on arbitrary movement or import its external AutoHotkey/pixel-trigger mechanism. Its author's policy assertions are not Blizzard approval. |
| [WeakAuras region actions](https://github.com/WeakAuras/WeakAuras2/blob/main/WeakAuras/RegionTypes/RegionPrototype.lua) | Chat actions delegate to its own chat-action handler. | An announcement feature alone does not demonstrate unrestricted sending for every audience or client. WeakAuras is not a way around protected APIs. |
| [SpeakForever](https://github.com/samsbase/SpeakForever/blob/main/README.md) | Local recognition writes clipboard text; the player opens, pastes and sends. Controller paste uses a user-configured mapping. | Upstream avoids our addon inbox and synthesized input path entirely. Its README's policy interpretation is the author's position, not a Blizzard authorization. |

## Blizzard UI code

[ChatFrameUtil](https://github.com/Gethe/wow-ui-source/blob/live/Interface/AddOns/Blizzard_ChatFrameBase/Shared/ChatFrameUtil.lua) separates opening/prefilling chat from submission. [ChatFrameEditBox](https://github.com/Gethe/wow-ui-source/blob/live/Interface/AddOns/Blizzard_ChatFrameBase/Shared/ChatFrameEditBox.lua) performs chat submission through its edit-box send path. [Generated ChatInfo API documentation](https://github.com/Gethe/wow-ui-source/blob/live/Interface/AddOns/Blizzard_APIDocumentationGenerated/ChatInfoDocumentation.lua) marks `SendChatMessage` as restricted and specifies taint-related argument requirements. These are mirrored game-source files for the live branch, not a verified Forever API contract.

## Earlier containment decision and evidence

The addon no longer invokes Enter or sends chat. The companion no longer offers auto-send, ignores old enabled settings, and emits manual-only packets. Old packets requesting send also remain manual drafts. The player presses the native A/send control after preparation; searches likewise stay manual.

Tests cover fragmented paste, explicit/current channel routing, restricted-field refusal, manual-only legacy packets and local protected-action diagnostics. The Windows build passes 222 core tests, 124 deterministic routing assertions and 30 native model checks. These tests do not create Blizzard's protected execution environment.

The addon now reports `ADDON_ACTION_BLOCKED` / `ADDON_ACTION_FORBIDDEN` with the actual function name when attributed to VoiceRouter. That identifies whether the remaining failure is opening chat, changing focus/text, or another operation. It never retries a blocked action. No computer-use tools or live game input were used for this research.

The following containment assessment predates the replacement. The current implementation removes addon-native opening/focus and uses the player's native chat action, metadata keys and plain paste. Preparing a draft is supported by source precedents, but our exact transport remains unverified on Forever. Do not claim that removing auto-send alone guarantees the popup is resolved or that synthetic inputs have Blizzard approval.
