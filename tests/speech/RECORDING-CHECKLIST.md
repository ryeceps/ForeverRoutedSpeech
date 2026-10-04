# WoW speech recording checklist

Put recordings in:

`C:\Users\leroy\Documents\ForeverRoutedSpeech Recordings`

Use your usual gaming microphone and normal speaking voice. Record one sentence per clip, with about one second of silence before and after speaking. Name clips `01.wav`, `02.wav`, etc. If your recorder produces M4A or MP3, keep that format: conversion can happen before testing. There is no need to install an audio editor or choose a sample rate.

If separate clips are inconvenient, record one file named `wow-test` and read the numbered sentences in order, leaving two seconds between them. You can stop after the first ten and return later. Read the sentence, without its number. Do not force a pronunciation just to match the spelling.

These are voluntarily retained test recordings, separate from normal app operation. Do not include private conversations, passwords, or other players' voice chat. Recordings stay local and are not committed to Git or uploaded automatically.

## First ten: places

01. Can anyone give me a portal to Stormwind?
02. Meet me outside the bank in Ironforge.
03. I am heading back to Orgrimmar.
04. Where is the auction house in Darnassus?
05. We are questing in Teldrassil near Shadowglen.
06. I need a group for Deadmines in Westfall.
07. Can someone summon me to Shadowfang Keep?
08. Looking for a healer for Blackfathom Deeps.
09. Does anyone want to run Gnomeregan?
10. We are going to Stratholme after Scholomance.

## More places

11. Meet me in Thunder Bluff, then we can travel to the Undercity.
12. I am in Elwynn Forest near Goldshire.
13. We are leveling in Dun Morogh and Loch Modan.
14. Looking for a tank for Zul'Farrak.
15. Who has the quest for Maraudon?
16. We need another healer for Molten Core and Onyxia.

## People and character names

17. Where can I find Thrall in Orgrimmar?
18. I am looking for Jaina Proudmoore.
19. Does Sylvanas Windrunner have a quest for us?
20. Meet me near Magni Bronzebeard in Ironforge.
21. Is that Tyrande Whisperwind or Malfurion Stormrage?
22. I was talking about Anduin Wrynn and Varian Wrynn.
23. Who knows the story of Arthas Menethil and Uther?
24. I am looking for Mankrik's wife.

These names test recognition; they do not imply that each character or quest is available in this game version.

## Shorthand: say it the way you normally would

25. LFG Deadmines, DPS ready.
26. LF1M tank for Scarlet Monastery.
27. WTS copper ore, five gold per stack.
28. WTB linen cloth and wool cloth.
29. I am OOM, wait before the next pull.
30. Can we get a rez? The healer has aggro.
31. Is that BoE or BoP? I need it for my alt.
32. Please CC the caster and LOS the next pack.

Write how you said abbreviations in `notes.txt`, for example: "25: said the letters L F G" or "26: said looking for one more". Both are useful. The written phrase is a target meaning, not a requirement to expand or shorten your words.

## Audience and text preservation

33. Tell guild we are meeting in Ironforge.
34. Say to everyone around me we need help.
35. Ask in trade if anyone is selling runecloth.
36. I bought copper ore yesterday, but I am not selling anything.
37. My guild is in Stormwind, but I am talking to my party.
38. Hey, what's going on?

For these clips, the evaluator must supply simulated game context; audio alone does not establish which channels are available. Clips 36 and 37 should not infer Trade or Guild just because those words occur. Clip 38 uses the configured current-chat/Say fallback. Explicit instructions are removed only after recognition and routing, so raw Whisper output should still include them.

## Your own difficult names

39. Add a sentence containing a place that Whisper usually gets wrong.
40. Add a sentence containing your character or a friend's character name.

For 39 and 40, write exactly what you said and the intended name spelling in `notes.txt`. Add as many extra clips as you like. Unusual player names may need a personal vocabulary list; no recognizer can infer every invented spelling from audio alone.

## Evaluation

Keep original recordings unchanged. Compare raw Whisper Turbo transcripts against intended names, then evaluate routing separately with declared contexts. Report name recognition and mistakes, including silence or invented text. If recordings are used to adjust vocabulary, reserve a fresh second take with different sentences for evaluation; do not report the tuning clips as an independent accuracy test.

This kit prepares recordings only. No audio recognition result is claimed until the clips have actually been run through the model.
