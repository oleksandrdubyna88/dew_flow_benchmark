# Authoring contract — read this before the group brief

You are writing evaluation questions for a code-retrieval benchmark. The questions measure whether a
retrieval engine can surface the right code and whether a model can answer from what it surfaced.

Target repository: `{{target}}`
Pinned commit: `{{commit}}`
Group: `{{group}}` — {{groupTitle}}
Write exactly {{count}} question(s).

## The repository you are describing is the one you are STANDING IN

Every question, every path, every member name refers to the working tree around you — `{{target}}` at
`{{commit}}`. Nothing else. Not the benchmark that launched you, not a repository you remember, not one
you have seen before with a similar layout.

**List the tree before you anchor anything, and take every path from that listing.** A path you typed from
memory is a path that does not exist here, and an expectation pointing at a file this tree does not contain
is thrown away by a mechanical check before a reviewer ever sees the question — the work you did to write
it is lost with it. This has happened: two questions in one batch anchored at `Bench.Infrastructure/...`,
which is the harness's own code and has never been in a target repository.

## What you must produce

A single JSON array and **nothing else**. No prose before it, no explanation after it, no markdown fence.

```json
[
  {
    "id": "short-kebab-id",
    "prompt": "The question, as a person would ask it.",
    "referenceAnswer": "What a correct answer says. One or two sentences.",
    "seed": { "kind": "commit", "reference": "ad9d2cf", "at": "2026-05-14" },
    "expectations": [
      { "kind": "Member", "file": "src/Path/File.cs", "member": "Type.Member", "start": 75, "end": 111 },
      { "kind": "AnswerContains", "text": "a term a correct answer must use" },
      { "kind": "AnswerExcludes", "text": "the memorised wrong answer's term" }
    ]
  }
]
```

## There is no reader for prose here

**Whatever happens, answer with the array.** If something stops you filling a field — you cannot date a
member, you are unsure of a line number, you have a caveat about the repository — **omit that field and
answer anyway.** Put the caveat in `referenceAnswer` if it belongs to the question.

Nothing downstream reads prose. An answer that opens with an explanation is discarded whole, by a parser,
and the work you did to find the code is lost with it. If you truly cannot write a single question, answer
with an empty array `[]` — that is a legible outcome; a paragraph is not.

## Rules that decide whether a question is accepted

1. **At least one `Member` or `File` expectation, pointing at real code in this tree at this commit.**
   A question with nothing to find measures nothing. Line numbers must be the real ones — if you are not
   certain of them, give the file and the member and set `start` and `end` to 0.

2. **The seed must be a real thing, with its real date.** `seed.kind` is `commit`, `member`, `pr`, `issue`
   or `human`; `seed.reference` names it; `seed.at` is when that thing dates from. This is how the benchmark
   decides whether a subject could have memorised the answer instead of working it out.

   **Prefer `commit`.** A `seed.reference` that is a short sha from the history below is the one kind the
   harness can date for itself — so if you name the change, you do not have to be right about the day. Write
   `at` as a plain calendar date (`"2026-05-14"`), never a timestamp.

   **Never invent a date.** If you do not know it, omit `at` entirely — unknown reads as *may recall*, which
   is honest, while a guessed date reads as *safe*, which is a lie the whole measurement rests on. With a
   `commit` seed, omitting it costs nothing: the repository supplies the day.

3. **A memorisation trap where the group calls for one.** A question whose obvious answer is the widely
   repeated one and whose correct answer, in THIS repository, is not. Express it as an `AnswerExcludes`
   expectation naming the term the memorised answer would use.

4. **Ask about the code, not about the documentation.** A question answerable from a README measures a
   README.

5. **One question, one thing.** A question with two parts scores as one number and cannot say which half
   failed.

## Your reference answer is the FIRST thing your own expectations are tested against

Before you write a question down, check it against its own `referenceAnswer`, because that is what a correct
answer looks like:

- **Every `AnswerContains` term must appear in your `referenceAnswer`, verbatim.** If it does not, then an answer
  as good as your own reference FAILS your check, and the question measures nothing. Twelve questions were thrown
  away for this on 2026-08-18 — a term like `"source line"` required by the check while the reference said
  `"one-line window"`.
- **No `AnswerExcludes` term may appear in your `referenceAnswer`.** This one is easy to trip while being
  helpful: you write the trap as `AnswerExcludes: "AsyncLocal"` and then explain in the reference that this
  repository *"uses scoped DI, not AsyncLocal"* — so the gold answer contains the forbidden word and fails.
  Describe what the code DOES do, and let the excluded term appear nowhere.
- **No `AnswerContains` term may appear in your `prompt`**, unless the group brief says the identifier belongs
  there. A term the question already contains is a term any on-topic answer contains, including a wrong one.

These three are checked mechanically before any reviewer sees your question, and a question that fails them is
never read by anybody. They cost you nothing to check and everything to get wrong.

## What gets a question rejected

- An expectation pointing at a file or member that does not exist in this tree at this commit.
- An expectation pointing outside this repository — a path from the harness, or from any other codebase.
- An `AnswerContains` term so generic that a wrong answer would contain it (`"the"`, `"method"`, `"class"`).
- A prompt that quotes its own answer.
- Anything outside the JSON array.

## The target's change history, gathered for you

You cannot read this repository's history yourself: the tree you are in is a `git worktree`, whose `.git` is a
redirect file your CLI declines to follow. Do not spend a turn trying — it fails, and one author's whole batch
was lost to exactly that attempt.

It is below instead, first-parent and newest first: each change as `<short sha> <date> <subject>`, followed by the
files it touched.

**Take `seed.at` from these dates verbatim, or omit it.** A date you did not read here is a date you invented,
and a guessed seed date reads as *safe* to the memorisation check — the one lie that check cannot survive.

```
{{history}}
```
