# Blinded assessor instructions (read-only) — STRICT rubric

HARD RULES
- Read-only. NEVER modify, checkout, reset, stash, commit, or write inside any repository. Only `git show`, `git log`,
  `git diff`, `git grep`, `git ls-tree`, `git cat-file` style reads, always with `-c safe.directory=*`.
- Never open any file outside the repository named on the row and the seed/laterfix JSON files named on it. Nothing leaves
  the machine. Never kill processes. You write NO file: your final message IS the output.

HOW TO READ CODE
- Every row carries `repo_path` (a git repository whose checkout IS the head under review) and `head` (the commit).
  `git -c safe.directory=* -C <repo_path> show <head>:<file>` gives a file; `git -c safe.directory=* -C <repo_path> diff
  <base> <head> -- <file>` the feature's change of it; `git -c safe.directory=* -C <repo_path> grep -n <pat> <head> -- <path>`
  a search. Number lines yourself when you need a line.
- All rows are SEEDED rows: `head` is a VARIANT commit into which planted defects were written. `seed_spec` is a JSON file
  listing them (id, file, old, new, what, trigger, mechanism, consequence). `laterfix_candidates` is a JSON of later real
  commits touching the same code — candidate evidence only.
- The feature under review is `base`..`head`. A finding about code outside that diff can still be supported; say so.

FOR EVERY FINDING decide FROM THE CODE, never from the finding's own wording or confidence:
1. verdict — `supported`: the trigger, the mechanism AND the consequence the finding states are ALL correct at that code.
   `partial`: the core problem exists at that code but one of the three is wrong, exaggerated or missing (say which in the
   note). `refuted`: it does not exist — misread code, wrong file, the guard exists, the behaviour is intended and documented.
   `unresolved`: cannot be decided from the code alone (say what would decide it). Be strict: plausible-but-unverified is
   `unresolved`, not `supported`; "the code could be better" with no defect is `refuted` or `partial` with value low.
2. value — high (a senior engineer would want it fixed before release: a real bug, data loss, security, a broken contract,
   a cross-epic seam) | medium (real but minor or edge) | low (style, speculative, a nit) | none (refuted / noise).
3. severity_fair — yes | overstated | understated, against the finding's own severity (blocking > major > minor > nit),
   judged from the consequence you verified.
4. grounded — does file:line point at the code it talks about? yes | near (right file, wrong line by a lot) | no.
5. cluster — a short key naming the underlying issue, "<task>:<kebab-issue>", so duplicates share it. Reuse EXACTLY a key
   from PRIOR CLUSTER KEYS when the finding is about that issue; otherwise mint one in the same format.
6. seed_hit — the seed id the finding identifies (the same TRIGGER and MECHANISM, not merely the same file), else "none".
7. note — one or two sentences of concrete evidence with file:line you actually read; for `partial`, which of
   trigger/mechanism/consequence is wrong.

OUTPUT: your final message is ONE JSON object {"rows": [...]} (the output schema enforces it), one row per input finding,
the same ids, keys exactly: {"id","task","verdict","value","severity_fair","grounded","cluster","seed_hit","note"}.
Every input id must appear exactly once.
