# data/raw/ — every raw record of the run, compressed

> Everything the experiment wrote, beside the summaries in `../arms/`, `../judges/` and `../metrics.json`, so that any
> number in [../../tables.md](../../tables.md) can be traced to the session that produced it. `raw-*.tar.xz`: parts 1–4
> (the 18 arms V, C, C2, C3, C4, VT, Q1–Q4, H1–H4, T1–T4); `raw-part5-*.tar.xz`: part 5 (O1–O4, S1–S4, D1–D4) and the
> mutation checks of all 30 arms; `raw-part5b-*.tar.xz`: part 5b (SR1–SR4, DR1–DR4) with their mutation checks. Check the files with `sha256sum -c SHA256SUMS`; unpack one with `tar -xJf <file>`.

| Archive | Unpacked | What is in it |
|---|---|---|
| `raw-results.tar.xz` | ~380 MB | `results/<arm>/`: every step attempt's full Claude Code event stream (`<step>.a<N>.jsonl` — its `result` event holds the host's cost, duration and tokens per model), its stderr and the exact prompt it was given (`<step>.a<N>.prompt.md` / `.brief.md`), `timing.jsonl`, the objective checks' full logs and CTRF test reports (`objective/`), the fill and survival reports, the T arms' per-task diffs and `before` commits, the mutation runs of C2 and T3; `results/base/`: the start commit's baseline build and test logs |
| `raw-transcripts.tar.xz` | ~205 MB | The Claude Code session transcripts of every arm (and of the T arms' task worktrees): every message, tool call and tool result as the agents saw them — the source the metrics read for crash-interrupted attempts |
| `raw-judges.tar.xz` | ~25 MB | The group judge (`judge-v1/`, superseded; `judge/`) and the absolute judge (`judge-abs/`): each session's brief, the redacted diff and objective file it was given, its full event stream and its verdict; the label keys |
| `raw-fillers.tar.xz` | ~6.6 MB | `vllm/work/<arm>/`: every prompt sent to the local model or to Haiku, its answer, its reasoning (when thinking was on) and its metadata; the region context packs of the skeleton arms; the T arms' task lists; `requests-<arm>.jsonl` (one line per filler request); the vLLM warm-ups |
| `raw-notes.tar.xz` | ~5 MB | Every note the coders, fixers and reviewers wrote, including the test and format logs some of them saved |
| `raw-logs.tar.xz` | < 1 MB | The chains' log (`chain.log`), the warm builds of each clone, the base clone's preparation, the pushes, the keep-awake processes, the judge runs' summaries, the metrics output |
| `raw-part5-results.tar.xz` | ~183 MB | `results/<O, S, D arm>/` as above (every step attempt's event stream, prompt, stderr, timing, objective logs, the task arms' per-task diffs and `before` commits, the mutation runs); `results/mutation-<earlier arm>/`: the mutation runs of the 18 earlier arms |
| `raw-part5-transcripts.tar.xz` | ~106 MB | The session transcripts of the 12 part 5 arms and of their parallel task worktrees |
| `raw-part5-judges.tar.xz` | ~12 MB | The absolute judge's sessions of the 12 part 5 arms (brief, redacted diff, objective file, event stream, verdict); the key and all 30 arms' scores |
| `raw-part5-fillers.tar.xz` | < 1 MB | `vllm/work/<O, S arm>/`: the task lists and task documents |
| `raw-part5-notes.tar.xz` | ~8 MB | Every note of the part 5 coders, reviewers and fixers, with the test and format logs they saved |
| `raw-part5-logs.tar.xz` | < 1 MB | `chain.log` through the end of the mutation checks, keep-awake, the pushes, the metrics output, `fix-outcomes.json` |
| `raw-part5b-results.tar.xz` | ~136 MB | `results/<SR, DR arm>/` as above, plus each arm's Haiku review diff (`<step>-hreview.diff`) and its `before` commit |
| `raw-part5b-transcripts.tar.xz` | ~91 MB | The session transcripts of the 8 part 5b arms and of the SR arms' parallel task worktrees — the source of the notes-leak finding ([../../incidents.md](../../incidents.md) §2e) |
| `raw-part5b-judges.tar.xz` | ~9 MB | The absolute judge's sessions of the 8 part 5b arms; the key and all 38 arms' scores |
| `raw-part5b-notes.tar.xz` | ~7 MB | Every note of the part 5b coders, Haiku reviewers, Opus reviewers' JSON and fixers, with their saved logs |
| `raw-part5b-logs.tar.xz` | ~5 MB | `chain-y.log`, the chain script as it ran, the judge's summary, the metrics output, keep-awake, the warm builds; `aborted-2026-10-27-1621/`: the results, notes and markers of the SR4 and DR4 runs stopped at the operator's pause (their clones, 6 GB, not kept here) |
| `raw-part5b-fillers.tar.xz` | < 1 MB | `vllm/work/<SR arm>/`: the task lists and task documents |

**Changed before archiving — two things, nothing else:**

- **The owner's two email addresses are masked** as `<email-work>` and `<email-personal>` (they appeared in git author
  lines the agents printed); 0 remain, in both sets.
- Nothing else was removed. A scan for secret-like strings (API keys, tokens, private keys, JWTs) found 7 distinct strings,
  all in test output: 5 appear verbatim in coai's own test sources, and 2 are synthetic values its tests build at run time
  (`sk-abcdefg` repeated five times, and `sk-` followed by 47 `k`s). None is a real credential. The same scan on part 5
  found the same strings and one more, `sk-abcdefghijklmnop`, which is in coai's test sources too.
- Part 5b: the same two addresses masked (0 remain). The secret scan found only strings that are in coai's own test
  sources (`xoxb-abcdefghijklmnop`, two `ghp_0123…` values, `sk-live-0123456789abcdefghijklmnop`, `sk-abcdefghijklmnop`,
  an RSA private-key header from `SecretCheckTests.cs`) and the two synthetic values above; other `sk-` hits are parts of
  words (`task-…`) and random ids.

**Not here:** the arms' clones and worktrees (the branches `exp/kimi-e56-*` in `dew_flow_connect_other_ais` hold every
arm's commits).
