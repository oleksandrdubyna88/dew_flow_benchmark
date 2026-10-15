# Treatment — what each arm receives (coai #751 series)

Written 2026-10-15, after `seed.txt` and `prediction.md`, before the probe and before any cell. Neither of those two files is changed by this one.

Both arms get the same tree: the story's parent commit P (`af7f2856`), the same plan file with its model words struck and withheld, the same
conventions mount built from ONE base, the same adapter hooks, the same audit hook, the same prompt (including the sentence that
independent work may be launched concurrently), the same environment variable names, the same pinned effort (`high` for every model) and the
same cap. They differ in exactly the files below, and `prepare-cell` refuses to build a cell if the difference is anything else.

## The base (both arms)

The conventions at the pin P records for `.agents/conventions`: `bb022429` (2026-10-09), built into a one-commit repository with a fixed
author, date and message (`lib/tree.mjs`, `buildArmMount`). The base is what P's own tests read, so the in-cell suite and the scoring suite
are the same suite. The base already carries the OLD rule: `common/subagent-models.md` blob `6d11121e` ("every subagent you launch runs on
Opus; Fable only for security and the architecture of a new project"). It carries no model guard.

## Arm A

The base, unchanged. The rule a session can read is the old one (blob `6d11121e`); no hook on `Agent|Task`.

## Arm B

The base plus exactly the three files of `story.json` `policyFiles`, taken from conventions `ea3252f2` (the tiers release, 2026-10-14):

| File | Arm A | Arm B |
|---|---|---|
| `common/subagent-models.md` | `6d11121e` (every subagent on Opus) | `f4689729` (the role table: Sonnet scouts and coders, Opus judges, Fable cornerstones) |
| `settings/hooks/subagent-model.mjs` | absent | the guard: refuses a subagent launch that does not name its model as a bare alias, and teaches the role table |
| `settings/settings.json` | the base's | the base's plus the guard's `PreToolUse` entry on `Agent|Task` |

The tree's own `.claude/hooks/*` and the `hooks` key of its `.claude/settings.json` are re-synced from the arm's mount (the conventions' own
adapter rule; `adapter-check` must pass), so arm B wires the guard from its own release and arm A wires none. The release's user-level
installer (`ensure-user-guard.mjs` and its lib) is in neither arm: a measured run must never write the real `~/.claude`.
`common/planning-docs.md` is the base's (`0b3fb778`) in BOTH arms; B does not get the release's version (`6db4c786`).

## The harness's edit bound (arm B only)

Arm B also gets two harness hooks (`edit-bound.mjs`, `edit-bound-core.mjs`) that DENY a main-session `Edit`, `Write`, `MultiEdit` or
`NotebookEdit` inside the tree once the current request would touch more than one file or more than 50 lines, and reset on each prompt. The
deny text is the rule's own wording and tells the session to delegate to a coder with model `sonnet`. The owner asked for this bound "only for
the test": the quick pilot's generation 2 showed that the shipped text alone, loaded and sha-matched, did not make the main session delegate.

This is **not** the shipped rule. The shipped rule (`f4689729`, "Two definitions") makes the 50 lines an eligibility test decided before the
first edit, bounding the WHOLE patch (a test file and a new file each count as a file; a replaced line counts twice), and says "No tool counts
this; the conservative choice is the default. Eligible means MAY." The harness bound is stricter and mechanical: it counts per request and per
tool call, it counts old plus new lines of each edit, and it fires after the fact, as a refusal. So arm B measures *the policy as complied
with under enforcement*, not the policy as shipped. Arm A has no such bound, and the old rule asks for none.

## Known limits of the treatment

- B's new rule points at files the base does not have: `research/RESULTS_subagent_model_tiers.md` (its first paragraph links to it as the
  evidence) and `common/planning-docs.md` § *A plan split into epics and stories* (the rule refers to it for where epics and stories live; the
  base's `planning-docs.md` has no such section). A session in B that follows either pointer finds nothing. This is the price of building both
  arms from P's pin, so that the in-cell suite equals the scoring suite; it is not repaired, and a B session that stumbles on it is evidence
  of the limit, not a defect of the cell.
- The rule text is the only difference a session can READ; the guard and the edit bound are the only differences it can FEEL (a denied
  launch, a denied edit). Neither names the experiment, and the harness files carry no wording that does (a test greps every injected file).
- Fable's price is assumed equal to Opus's in the cost watchdog, because no recorded envelope holds a Fable message; reported costs are always
  the CLI envelope's.
