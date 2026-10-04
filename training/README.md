# WoW language bootstrap

The corpus includes combat shorthand, loot conversation, guild address versus references, profession advertisements, dungeon recruitment, zone requests, negations and spoken abbreviation forms. Added authored examples do not represent real microphone transcription accuracy. Context expansions are not independent paraphrase families.

Run `scripts/Train.ps1 -FastText native/build/fasttext.exe -OutputDirectory artifacts/wow-language-candidate` to evaluate a candidate without replacing the deployed model. The fastText source commit is pinned in `native/CMakeLists.txt`.

The latest authored evaluation is in `reports/wow-language-bootstrap.json`. The new candidate produced one false Guild route, so the existing deployed model was retained. Public inference stays disabled: both validation and held-out public results have only seven independently emitted families, below the required thirty. Reported model probabilities remain uncalibrated scores.

Whisper vocabulary hints live in `Config.DefaultPrompt`, separately from classifier training. Updated prompts migrate only recognized old defaults; custom prompts remain intact. The prompt is a recognition hint, not Whisper fine-tuning or a transcript rewrite. WoW-specific audio evaluation is still needed before claiming improved recognition.
