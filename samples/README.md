# samples/

Suites you can run. Two files, and they are not the same kind of thing.

| File | What it is |
|---|---|
| [polly-smoke-suite.json](polly-smoke-suite.json) | **A real suite against a real target.** Three questions, every anchor verified by reading the tree at a pinned commit |
| [demo-suite.json](demo-suite.json) | **A shape example, not a suite.** Its anchors point at `src/Orders/OrderService.cs` and friends, which exist in no repository. It shows the file FORMAT and nothing else — running it measures nothing |

## The smoke suite

```bash
./hosts/Cli/bin/Release/net10.0/bench plan \
  --repo https://github.com/App-vNext/Polly.git \
  --commit a603169f460df708206ecf907096848f584c9003 \
  --suite-file samples/polly-smoke-suite.json \
  --subjects qwen3-coder@local --lanes native --repeats 2 --engine NoRetrieval
```

**What it is for.** Getting the harness through a real end-to-end run — checkout, engine, model, trace,
scoring, storage — against a tree nobody here wrote. That is the moment the infrastructure becomes a
benchmark. It is deliberately three questions, which is enough to exercise every link and far too few
to rank anything.

**What it is NOT.** A measurement. Three questions cannot separate two configurations, and this file
does not pretend otherwise. The report's own guards say so independently: with `n` repeats overlapping
and a three-question split, nothing here reaches the bar to crown a winner.

### The target, and why this one

`App-vNext/Polly` at `a603169f460df708206ecf907096848f584c9003`.

Chosen against the founding plan's open question 1: **the first serious target should be a large,
unfamiliar C# codebase**, because every finding carried into this project came from one small repository
the same team wrote — a confound large enough to invert the headline result. Polly is real, public
(BSD-3-Clause), moderately large, and **no `dew_flow_*` repository depends on it**, so nothing here has
ever read it in anger.

It is not yet the *large* codebase that question calls for. It is the honest first step: a tree we did
not write, small enough that ground truth can be verified by hand in an afternoon.

### Scoring needs no judge, on purpose

Every expectation is mechanical — `Member`, `AnswerContains`, `AnswerExcludes`. A first measurement that
depended on a second model would be a measurement of two things at once, and the arbiter is the one you
cannot check by reading. The judge arrives later, for the fix lane, where "was the diagnosis right"
genuinely has no assertion.

### How the ground truth was verified

Every anchor was read at the pinned commit, not inferred from a name:

| Question | Anchor | Verified |
|---|---|---|
| `retry-jitter-formula` | `RetryHelper.DecorrelatedJitterBackoffV2` | `src/Polly.Core/Retry/RetryHelper.cs` 75–111 — signature on 75, closing brace on 111 |
| `circuit-open-condition` | `AdvancedCircuitBehavior.OnActionFailure` | `.../Controller/AdvancedCircuitBehavior.cs` 20–44 |
| `timeout-vs-caller-cancellation` | `TimeoutResilienceStrategy.ExecuteCore` | `src/Polly.Core/Timeout/TimeoutResilienceStrategy.cs` 26–94 |

### Each question resists being answered from memory

That property is not decoration — it is what separates a benchmark from a quiz a model has already seen.

- **`retry-jitter-formula`** — the remembered answer for "exponential backoff with jitter" is AWS-style
  full jitter: multiply the delay by a random factor. Polly does something else for
  `Exponential + jitter` — a decorrelated formula that carries state between attempts — and the required
  `AnswerContains: "DecorrelatedJitter"` is a token you can only produce by reading the file. The plain
  band-around-the-delay jitter exists too, and is used only for `Constant` and `Linear`, so a
  half-remembered answer lands on the wrong one.
- **`circuit-open-condition`** — the remembered answer is "it opens after N consecutive failures". Polly
  v8 has no such rule: it needs a minimum throughput AND a failure ratio in the same sampled window.
  `AnswerExcludes: "consecutive"` is the trap, and a correct answer has no reason to trip it.
- **`timeout-vs-caller-cancellation`** — the obvious answer is "a cancelled call under a timeout throws
  `TimeoutRejectedException`". It does not, when the CALLER cancelled: the strategy checks that the
  caller's own token was not already cancelled before claiming the cancellation as its own.

### What is still missing to run it

The suite is the INPUT to the first live run. `bench run` and the model runtime that executes a leg are
being built separately; until they land, this file is exercised by `bench plan`, which freezes it,
materialises the matrix and validates every expectation it can without executing anything.

`AnswerContains` is a blunt instrument and is meant to be: it is a floor under the answer, while the
`Member` anchors carry the weight. A rambling answer can satisfy a substring it did not earn, which is
one more reason three questions are a smoke test rather than a result.
