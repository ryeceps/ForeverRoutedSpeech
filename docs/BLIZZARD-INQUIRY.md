# Unsent policy inquiry

Suggested addon-policy contact: WoWUI@blizzard.com, listed in the official [UI Add-On Development Policy](https://us.forums.blizzard.com/en/wow/t/ui-add-on-development-policy/24534). The [staff reply about voice input](https://us.forums.blizzard.com/en/wow/t/new-tos-update-question/709198) also suggests the Accessibility team. This file is a draft only; no message has been sent.

Subject: WoW Forever — policy clarification for controller-driven local voice dictation

Hello,

I am developing a Windows companion for World of Warcraft: Forever. A controller-stick click starts recording and another stops it. Local whisper.cpp transcribes speech; local fastText proposes a chat audience while preserving the message. The current app copies a draft; the player pastes and sends. Audio stays in memory and normal operation is offline.

There is no process modification, game-memory inspection, network interception, multi-client broadcasting, movement/combat control, continuous listening, scheduled messaging, or purchasing/bidding automation.

Please clarify these mechanisms separately:

1. Standalone local dictation and clipboard drafting for Forever.
2. Separate physical controller inputs to paste and send, with no repeated or scheduled input.
3. Automatic text insertion after transcription (opening chat when necessary, replacing existing text and pasting), followed by a separate physical player input to send. This is not enabled in the current app.
4. A free, unobfuscated Lua addon displaying a pixel status strip containing group category, guild membership, joined channel names/IDs and a verified focused Auction House search-field identity/limit, decoded by the companion for draft routing. It would export no restricted combat information.
5. Automatic sending after transcription if explicitly enabled by the player. This is not enabled in the current app.
6. Additional Forever or beta-specific requirements.

Auction House support would enter search queries only, with no purchases, bids, sales or posting. Please identify mechanisms that must be removed or changed to fit policy. I can provide source code and an exact demonstration. Thank you.
