You draft the code through a LOCAL model, then you finish it. This is mandatory in this run.

**The model and the wrapper**
- The local model is Qwen3.6-35B-A3B on vLLM.
- The only way to reach it is the wrapper:
  `node /home/jinx/git/coai-exp/vllm/ask.mjs [--no-think] [--max-tokens N] <prompt.md> [<prompt.md> ...]`
- Put your prompt files under `/home/jinx/git/coai-exp/vllm/work/V/`.
- Up to 4 prompt files in one call run in parallel; use this for independent pieces.
- The answer of `X.md` is written to `X.answer.md`.

**What a prompt must hold**
The model cannot read the repository. A prompt must hold everything it needs:
- the relevant source excerpts;
- the scenario rows;
- the exact type, method and test names;
- the conventions to follow (FluentAssertions, xUnit v3, the file's style).

**What you draft, and what you may write yourself**
- Every new or changed production method and every new test method is drafted by the local model first. Then you integrate the draft, compile it and fix it.
- You may not write such a method from scratch. If a draft is still unusable after one re-prompt, you may write that method yourself; record that you did.
- You may do small glue yourself: `using` lines, an entry in a list, JSON rows, formatting, and fixes for compile or test errors.

**Speed**
- The model often writes code that does not compile, so always build.
- Thinking is on by default. One request runs at about 40 tokens/s; 4 at once run at about 28 tokens/s each.
- Thinking can be long. `--no-think` is much faster for simple pieces.

**Record**
In your notes file, for each request: its purpose; whether you used the draft (as is, fixed, or discarded); the compile errors you fixed.

**Commits and code**
Do not mention the local model in commit messages or in the code.
