# data/raw/ — every raw record of the run, compressed

> Everything the experiment wrote, beside the summaries in `../arms/`, `../judges/` and `../metrics.json`, so that any
> number in [../../tables.md](../../tables.md) can be traced to the session that produced it. Parts 1–4 (the 18 arms
> V, C, C2, C3, C4, VT, Q1–Q4, H1–H4, T1–T4); part 5's arms are added when they end. Check the files with
> `sha256sum -c SHA256SUMS`; unpack one with `tar -xJf raw-<part>.tar.xz`.

| Archive | Unpacked | What is in it |
|---|---|---|
| `raw-results.tar.xz` | ~380 MB | `results/<arm>/`: every step attempt's full Claude Code event stream (`<step>.a<N>.jsonl` — its `result` event holds the host's cost, duration and tokens per model), its stderr and the exact prompt it was given (`<step>.a<N>.prompt.md` / `.brief.md`), `timing.jsonl`, the objective checks' full logs and CTRF test reports (`objective/`), the fill and survival reports, the T arms' per-task diffs and `before` commits, the mutation runs of C2 and T3; `results/base/`: the start commit's baseline build and test logs |
| `raw-transcripts.tar.xz` | ~205 MB | The Claude Code session transcripts of every arm (and of the T arms' task worktrees): every message, tool call and tool result as the agents saw them — the source the metrics read for crash-interrupted attempts |
| `raw-judges.tar.xz` | ~25 MB | The group judge (`judge-v1/`, superseded; `judge/`) and the absolute judge (`judge-abs/`): each session's brief, the redacted diff and objective file it was given, its full event stream and its verdict; the label keys |
| `raw-fillers.tar.xz` | ~6.6 MB | `vllm/work/<arm>/`: every prompt sent to the local model or to Haiku, its answer, its reasoning (when thinking was on) and its metadata; the region context packs of the skeleton arms; the T arms' task lists; `requests-<arm>.jsonl` (one line per filler request); the vLLM warm-ups |
| `raw-notes.tar.xz` | ~5 MB | Every note the coders, fixers and reviewers wrote, including the test and format logs some of them saved |
| `raw-logs.tar.xz` | < 1 MB | The chains' log (`chain.log`), the warm builds of each clone, the base clone's preparation, the pushes, the keep-awake processes, the judge runs' summaries, the metrics output |

**Changed before archiving — two things, nothing else:**

- **The owner's two email addresses are masked** as `<email-work>` and `<email-personal>` (they appeared in git author
  lines the agents printed); 0 remain.
- Nothing else was removed. A scan for secret-like strings (API keys, tokens, private keys, JWTs) found 7 distinct strings,
  all in test output: 5 appear verbatim in coai's own test sources, and 2 are synthetic values its tests build at run time
  (`sk-abcdefg` repeated five times, and `sk-` followed by 47 `k`s). None is a real credential.

**Not here:** the arms' clones and worktrees (the branches `exp/kimi-e56-*` in `dew_flow_connect_other_ais` hold every
arm's commits).
