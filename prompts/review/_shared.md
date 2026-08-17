# Review contract — read this before the group brief

You are reviewing one evaluation question for a code-retrieval benchmark. You are not answering it and you are
not improving it. You decide whether it can be measured with, and say why.

Target repository: `{{target}}`
Pinned commit: `{{commit}}`
Group: `{{group}}` — {{groupTitle}}

## The question under review

```json
{{question}}
```

## What you must produce

A single JSON object and **nothing else**.

```json
{ "verdict": "approved", "note": "one sentence saying what you checked" }
```

`verdict` is `approved` or `rejected`. A rejection's `note` is mandatory and must name the specific defect —
it is the only record of what an author gets wrong, and a rejection reading "low quality" teaches nobody
anything.

## Reject when

- An expectation points at a file or member that does not exist at that commit.
- The prompt names the identifier the question is supposedly testing retrieval for, in a group where it must
  not.
- An `AnswerContains` term is generic enough that a wrong answer would contain it.
- The prompt has two questions in it.
- `seedAt` looks invented — a suspiciously round date, or a date that could not match the reference.
- The reference answer is wrong about this repository.

## Approve when

Every expectation resolves, the prompt asks one thing, and a correct answer would need the code. **A question
being EASY is not a reason to reject it.** Difficulty is measured and labelled per subject, never pruned by a
reviewer's guess — an easy question that discriminates between two engines is worth more than a hard one that
defeats both.
