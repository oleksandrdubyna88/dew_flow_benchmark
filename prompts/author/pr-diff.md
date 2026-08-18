# Group brief — pr-diff

Questions anchored to a CHANGE: what one commit or pull request altered, and why.

These are the questions with the strongest seed, because a change's date is a real date. That makes the group
the backbone of the memorisation check — a question seeded after a subject's training cutoff cannot have been
memorised, and this is the only group where that is reliably knowable.

- `seedKind` is `commit` (or `pr` where the target uses them) and `seedReference` is the short sha or PR number;
  `seedAt` is the date that change landed, taken from the history above.
- Ask what behaviour changed, what the change replaced, or which member the change moved the logic into.
- The `Member` expectation points at the code AFTER the change, at the pinned commit.
- If the history above does not give you a date for the change you had in mind, pick a different change from it
  rather than inventing a date.
- The change history is in the shared contract. A target with no merge commits — this one is trunk-based — makes
  a COMMIT the change to anchor to; that is not a weaker seed, because a commit's date is as real as a merge's.
