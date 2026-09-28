# MU Online BMD translation rules (machine translation, all target languages)

You translate classic MU Online game strings from English into ONE target language
named in the batch file (`language` field). The strings feed fixed-size binary game
files, so the rules below are MANDATORY.

## Input / output contract
- Input batch JSON: `{"locale","language","chunk","out","strings":[{id,en,budget,kinds}]}`
- For EVERY input string produce a translation. Write EXACTLY this JSON to the path
  given by `out` (create folders), UTF-8, no BOM:
  `{"results":[{"id":<same id>,"tx":"<translation>"}, ...]}`
- `id`s must match the input 1:1, same count, no extras, no omissions, same order.
- Write the file with Python: `json.dump(obj, f, ensure_ascii=False)`. NEVER use shell
  redirection / PowerShell Out-File / console echo to write it (encoding corruption).
- After writing, validate:
  `python D:\openmu自用\_locwork\validate_batch.py <input.json> <out.json>`
  and fix every reported problem until it prints OK.

## Invariants that MUST be preserved character-for-character
1. Placeholders exactly, same count and relative order: `%d` `%s` `%f` `%0.2f`
   `%0.2%%` `%%` `{0}` `{1}`. Note `%%` is a literal percent — keep both percents.
2. Separator counts identical to English: `;` (quest dialogue speaker/branch split),
   `#` (master-skill tooltip line break), `/` (buff option separator).
3. Numbers, level ranges `(15-80)`, coordinates `(119,112)`, Roman numerals, and
   tokens HP/MP/AG/SD/XP/PvP/PvE/3D stay as-is. `Zen` (the currency) stays "Zen".
4. Do NOT add quotes, notes, romanization or explanations; output translation only.
5. Internal map ids keep their suffix: translate the name, keep digits/underscores
   (e.g. `Atlans2`, `Kanturu_ruin_island` style tokens keep `2`/`_...`).

## Hard byte budget
- `budget` = maximum UTF-8 BYTES (not characters). Latin script ≈ 1 byte/char,
  Cyrillic = 2, Japanese/Chinese = 3. The validator rejects overflow.
- Item/skill/option names (budget 29/31/49) must be SHORT: drop articles, compress
  ("1st Lucky Armor Ticket" style names are names, not sentences). Prefer compact
  game-style naming over literal translation. Descriptions (budget 99/255) may be
  full sentences but stay within budget; trim filler rather than overflow.
- If a literal rendering overflows, shorten naturally; never drop a placeholder or
  separator to make room.

## Terminology (keep consistent across ALL your batches)
- Classes: Dark Knight/Blade Knight, Dark Wizard/Soul Master, Fairy Elf/Muse Elf,
  Magic Gladiator/Duel Master, Dark Lord/Lord Emperor, Summoner/Bloody Summoner,
  Rage Fighter/Fist Master, Rune Wizard, Gun Crusher, Illusion Knight — use the
  standard localized class names conventional for your target language; if unsure,
  a compact established translation is fine, but use it EVERYWHERE consistently.
- World maps are proper nouns — use the conventional target-language form and keep
  it identical everywhere: Lorencia, Noria, Devias, Dungeon, Lost Tower, Atlans,
  Tarkan, Icarus, Elbeland, Kalima, Kanturu, Karutan, Raklion, LaCleon, Aida,
  Vulcanus, Valley of Loren, Crywolf, Acheron, Balgas Barracks, Arena.
- Events: Blood Castle, Chaos Castle, Devil Square, Illusion Temple, Doppelganger,
  Empire Guardian, Santa Town, Ordeal, the Hatchery — translate the descriptor
  consistently (or keep the canonical name if that is your locale's convention).
- Jewels: Jewel of Bless/Soul/Chaos/Life/Creation/Harmony; Potion, Scroll, Key,
  Ticket, Ring, Pendant, Necklace.
- Stats: Strength, Dexterity/Agility, Vitality, Energy, Leadership, Life/HP, Mana,
  Defense, Defense Rate, Attack, Damage, Attack Speed, Wizardry/Magic Power,
  Durability, Excellent, Luck, Option, Critical/Excellent/Double Damage,
  Ignore defense, Reflect damage, Damage reduction.
- NPCs: Chaos Goblin, Charon, Lahap, Hanzo, Leina, Archangel, Guardsman, Lugard,
  Moss, Barmaid, Gatekeeper, Gens Duprian/Vanert. One-off personal names: natural
  transliteration, used once.
- "Shadow Phantom Soldier/unit" is a recurring quest NPC — give it ONE consistent
  target-language name and reuse it everywhere.

## Tone
- Item/skill/buff/option text: terse game phrasing (fragment style is OK), like the
  original classic client. Master-skill tooltips: short mechanical descriptions.
- NPC/quest dialogue (chunk g1/g2): natural, polite spoken language in your locale;
  keep the meaning and any (Level xx-yy) requirement clauses.
- Do NOT leave English words in the translation except: Zen, HP/MP/AG/SD, PvP/PvE,
  numbers, and proper nouns your locale conventionally leaves in English.

## Per-language notes
- de (German): formal "du"-style game tone; correct umlauts ä ö ü ß; NO CJK chars.
- pl (Polish): correct diacritics ą ę ć ń ó ł ś ź ż; NO CJK.
- ru (Russian): natural Russian, Cyrillic only (+ё where standard); NO CJK ideographs.
- uk (Ukrainian): natural Ukrainian (і ї є ґ), Cyrillic; never Russian spellings; NO CJK.
- id (Indonesian): natural Bahasa Indonesia, Latin only; "kamu/kau" dialogue tone.
- tl (Tagalog/Filipino): natural Filipino, Latin only, may use common game English
  loanwords familiar to Filipino players but sentence structure in Filipino.
- ja (Japanese): desu/masu polite dialogue; katakana for loanwords/events/classes;
  kanji+kana mixed natural Japanese; item names compact (budget 29 bytes ≈ 9 chars).
