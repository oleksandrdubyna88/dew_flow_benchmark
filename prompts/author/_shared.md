# Authoring contract — read this before the group brief

You are writing evaluation questions for a code-retrieval benchmark. The questions measure whether a
retrieval engine can surface the right code and whether a model can answer from what it surfaced.

Target repository: `{{target}}`
Pinned commit: `{{commit}}`
Group: `{{group}}` — {{groupTitle}}
Write exactly {{count}} question(s).

## What you must produce

A single JSON array and **nothing else**. No prose before it, no explanation after it, no markdown fence.

```json
[
  {
    "id": "short-kebab-id",
    "prompt": "The question, as a person would ask it.",
    "referenceAnswer": "What a correct answer says. One or two sentences.",
    "seedKind": "member",
    "seedReference": "Type.Member or a PR/issue reference",
    "seedAt": "2026-05-14",
    "expectations": [
      { "kind": "Member", "file": "src/Path/File.cs", "member": "Type.Member", "start": 75, "end": 111 },
      { "kind": "AnswerContains", "text": "a term a correct answer must use" },
      { "kind": "AnswerExcludes", "text": "the memorised wrong answer's term" }
    ]
  }
]
```

## Rules that decide whether a question is accepted

1. **At least one `Member` or `File` expectation, pointing at real code in that repository at that commit.**
   A question with nothing to find measures nothing. Line numbers must be the real ones — if you are not
   certain of them, give the file and the member and set `start` and `end` to 0.

2. **The seed must be a real thing, with its real date.** `seedKind` is `member`, `pr`, `issue` or `human`;
   `seedReference` names it; `seedAt` is when that thing dates from. This is how the benchmark decides
   whether a subject could have memorised the answer instead of working it out. **Never invent a date.** If
   you do not know it, say `"seedAt": ""` — unknown reads as *may recall*, which is honest, while a guessed
   date reads as *safe*, which is a lie the whole measurement rests on.

3. **A memorisation trap where the group calls for one.** A question whose obvious answer is the widely
   repeated one and whose correct answer, in THIS repository, is not. Express it as an `AnswerExcludes`
   expectation naming the term the memorised answer would use.

4. **Ask about the code, not about the documentation.** A question answerable from a README measures a
   README.

5. **One question, one thing.** A question with two parts scores as one number and cannot say which half
   failed.

## What gets a question rejected

- An expectation pointing at a file or member that does not exist at that commit.
- An `AnswerContains` term so generic that a wrong answer would contain it (`"the"`, `"method"`, `"class"`).
- A prompt that quotes its own answer.
- Anything outside the JSON array.
