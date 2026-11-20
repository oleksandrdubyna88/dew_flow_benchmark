# Dated amendments — Stage 1 of the language comparison v2

This file is registered by the pre-registration tag. It only grows: a dated line is appended, and no line is ever edited or
removed (a wrong line is followed by a corrected one, before the first data session only). A line counts only when it
starts with a real calendar date and has one of the forms below; a line of these kinds that is not valid is reported as
malformed and counts for nothing. Prose, like this paragraph, is not read. Every line is public text (D41): counts,
hashes, costs, times, classes and paths with the home written as `~`; never a case id, a trap name, a witness id, case
text or a requirement breakdown per case. Each line reaches the series' branch only from `origin/main`, by `stage-sync`.

The line forms (the fields are separated by ` | `):

- **The owner's go for the pilot** (O2): `YYYY-MM-DD | owner-go | pilot | <the owner's words>`.
- **The owner's go for rounds a to b** (O3): `YYYY-MM-DD | owner-go | rounds:<a>-<b> | <the owner's words>`, where a
  and b are rounds of the five and a is not after b. A go for the pilot covers no round; a go for rounds a to b covers no
  pilot step and no other round.
- **The one change of the wall and the stall**, for both stacks alike, after the pilot:
  `YYYY-MM-DD | caps | wallMs:<n> | stallMs:<n> | <reason, with G-18>`. The cap in dollars is never amended.
- **The pilot's GO**: `YYYY-MM-DD | pilot | GO | verdicts:<12 hex of the sha256 of the GO table's file> | <who>`.
- **A by-hand verdict** (the rows G-10, G-13 and G-16 of the pilot, and the read-back's row G-1 of rounds 1 and 2):
  `YYYY-MM-DD | G-<n> | holds|refuted|explained | <rid> | <evidence path> | <who>`. A `refuted` line makes that session
  `void: harness`.
- **A harness identity accepted** (an instrument fix, before the first data session only):
  `YYYY-MM-DD | instrument | harness:<12 hex> | commit:<40 hex> <reason, with its G-id>`. The 12 hex is the harness
  identity of the fixed folders (never a commit sha); the reason begins with the one token `commit:<40 hex>`, the commit
  of `main` that holds the fix, which `stage-sync --accept-harness <12 hex>` takes.
- **An amended input** (`quiet.json`, `package-flags.json`, and the free fields `t3`, `t5`, `decidedOn` and `reason` of
  `crosscheck-policy.json`; the 12 hex are those of the sha256 of the new content):
  `YYYY-MM-DD | instrument | input:<file name>:<12 hex> | <reason, with its G-id>`. Refused once a data session has been
  planned.

No dated line is written yet.
