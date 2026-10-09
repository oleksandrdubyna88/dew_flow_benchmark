# RESULTS — NVIDIA Nemotron as coai question consultants

> Status: **measured 2026-10-09; the answers were read by hand and carry no rubric score.**
>
> - **Models:** Nemotron 3.5 Lightning, 3 Super and 3 Ultra through OpenRouter, with Codex `gpt-6-astra` as the
>   reference.
> - **Product:** coai `ask_consultants` (mcp 0.45.0, origin/main), prompt `question-opinion` (capability `none`).
> - **Endpoints:** paid. The `:free` ones were overloaded on the same day
>   ([RESULTS_gate_feature_s73.md](RESULTS_gate_feature_s73.md), § *Nemotron 3 Ultra and 3 Super*).
>
> Related: [module_probes.md](module_probes.md), [PLAN_question_consultant_probes.md](PLAN_question_consultant_probes.md).

## The question

Can one of the Nemotron models sit in the question consultant's row list beside or instead of a Codex seat? The
feature-gate run had already ruled them out as reviewers. A consultant answers one question in a few sentences, which is
a different job, so it was measured separately.

## Reachability — `bench probes`, `api-reachable` × 3

Run `01a121a8`: 12 cells, all settled. Each probe goes through coai's `--probe-api`.

| subject | model | reachable | account out |
|---|---|---|---|
| or-nemotron-lightning-free | `nvidia/nemotron-3.5-lightning:free` | **0 / 3**: all timed out | — |
| or-nemotron-lightning | `nvidia/nemotron-3.5-lightning` | 3 / 3 | no |
| or-nemotron-super-free | `nvidia/nemotron-3-super-120b-a12b:free` | 3 / 3 | no |
| or-nemotron-super | `nvidia/nemotron-3-super-120b-a12b` | 3 / 3 | no |

Ultra was not probed. Its feature-gate cells on the paid endpoint (7 of 7 valid) already showed that it is reachable.

## The answer test — five real questions

Each question came from this benchmark's own work and carried the context a developer would give. Every model answered
it through the product path, `ask_consultants`: one coai-mcp over stdio, an isolated data directory, and one row per
model. Lightning, Super and Astra shared one run. Ultra had a run of its own with the same questions and the same
prompt.

1. **q1 — assessor turns:** raise the blind assessor's one-turn ceiling, or wait three days for the Codex assessor?
2. **q2 — shared pool:** a model that returns upstream 429 for two days. Retry, add a vendor key (the owner does not
   want to buy one), or drop it?
3. **q3 — missing cells:** a usage window cuts off a campaign's last tasks every time.
4. **q4 — a 200 with an error body:** how to classify it, and is a retry safe? This is coai issue #721.
5. **q5 — a backup before a Windows update:** a Postgres dump and a 735 MB artefact folder with its key file, to a
   NAS. `docker cp` to the mapped drive exited 0 and wrote nothing.

The time per answer, in seconds:

| model | q1 | q2 | q3 | q4 | q5 |
|---|---|---|---|---|---|
| Astra (`codex`, reference) | 26 | 25 | 24 | 25 | 26 |
| Nemotron 3 Ultra | 12 | 11 | 8 | 64 | 12 |
| Nemotron 3 Super | 64 | 150 | 70 | 158 | **failed** after 468 |
| Nemotron 3.5 Lightning | 56 | 15 | 68 | 3 | 6 |

Super's q5 spent 5,749 reasoning tokens and returned no message (`exit 70`).

### What each answer got right and wrong

For q2 and q3 the decision was later taken on other grounds, so each has a known right answer: mistral-large was
dropped, and `bench gate run --tasks` was built (#64).

**Astra: the best on every question.**
- It answered every question with the decision first and stayed inside the stated constraints. In q2 it was against
  a vendor key, since a key is a separate account and bill.
- It caught the subtle point each time:
  - q1: keep the Claude-assessed results apart from the Codex-assessed ones.
  - q3: choosing which cells run must not change the scope they belong to.
  - q5: the key file, and a custom-format dump that can be restore-tested.

**Ultra: the only Nemotron worth a seat.**
- It is fast and short, and it decides.
- q2: it respects the constraint (no vendor key) and drops the model. The replacements it names, the vendor's medium
  model or codestral, had already been measured, as the context said.
- q3: right: run only the named tasks.
- q4: the classification is right, with a transient inner error retried and the rest failed. Its code pointers name
  the CLI reviewer layer (`ReviewerExecutor`, `ReviewerRuntime`) instead of the API reader where the defect lives.
- q1 has an error of reasoning. It picks 10 turns "now" and argues that waiting for Codex would make the results
  incomparable with the five Codex-assessed reviewers. The opposite is true: switching the assessor is what breaks the
  comparison.
- q5: it is right not to trust `docker cp` to a mapped drive, and it verifies on the NAS. It proposes
  `pg_restore --list`, which fails on a plain-SQL dump, and it does not name the key file.

**Super: acceptable content, but slow and unreliable.**
- It is verbose, and two to six times slower than Astra.
- q2: it recommends buying the vendor key, against the stated constraint.
- q5: it produced no answer.

**Lightning: not recommended.**
- q1 is one line with no reasoning.
- q2 contradicts itself: it says "drop the model", then explains how to configure a vendor key. It also claims that
  every model of the vendor gets the same 429, which the context refutes.
- q5 proposes `pg_restore` on a `.sql` dump and says nothing about verifying the copy.

## What follows

- **Keep Astra (or another Codex seat) as the first consultant.**
- **Nemotron 3 Ultra is the cheapest useful second opinion measured so far.** It answers in about 10 s on the paid
  endpoint. Treat its reasoning as a suggestion to verify: in one of five answers the reasoning ran backwards from its
  premise.
- **Do not add Lightning or Super.** Lightning's `:free` endpoint was unreachable, and its paid answers were wrong
  twice. Super was slow and failed one of five.
- **The product defect** that made the free endpoints look empty, a 200 with an error object read as empty content, is
  coai issue #721.

## What this does and does not show

- Five questions, one answer per model each, read by one person (the session's own model) with the known outcomes of
  q2 and q3. It is an impression with reasons, not a rate.
- One prompt (`question-opinion`, no repository access asked for). With a disk prompt the ranking could differ.
- One transport and one setting per model: OpenRouter paid, the vendor's default thinking, an 8,192-token output cap.
- Ultra ran on a different hour from the other three. The cost per answer was not captured.
