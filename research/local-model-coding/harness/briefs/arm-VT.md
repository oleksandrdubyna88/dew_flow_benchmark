You draft code through a **local model**, then finish it yourself. The local model is Qwen3.6-35B-A3B on vLLM. You reach it
only through this wrapper:

```bash
node /home/jinx/git/coai-exp/vllm/ask.mjs [--max-tokens N] <prompt.md> [<prompt.md> ...]
```

- Write prompt files under `/home/jinx/git/coai-exp/vllm/work/VT/`. Up to **4 prompt files in one call run in parallel**: use
  that for independent pieces. The answer of `X.md` is written to `X.answer.md`.
- The model cannot read the repository. A prompt must carry what it needs: the relevant source excerpts, the scenario rows, the
  exact names and signatures, and the code style to follow.
- Speed: ~40 tokens/s for one request, ~28 tokens/s each for 4. **Thinking is always on in this run** (it cannot be turned off),
  and it can be long: a request often takes several minutes. Keep `--max-tokens` high enough for the thinking and the answer
  (the default is 14000); an answer cut by the limit comes back empty.
- **The rule:** every new or changed production method and every new test method is first drafted by the local model; you then
  integrate it, compile it and fix it. You do not write such a method from scratch. If a draft is unusable after one re-prompt,
  you may write that method yourself, and you record it. Small glue you may do yourself: `using` lines, one entry in a list,
  JSON rows, formatting, and fixing compile errors.
- The model's code often does not compile. Always build.
- Never mention the local model in commit messages or in code.
- In your notes, record per request: its purpose, and whether you used the draft as-is, fixed it, or discarded it, and which
  compile errors you fixed.
