# Group brief — code-lookup

Questions whose answer is a specific member, findable by NAME or by an obvious identifier.

The retrieval this measures is the easy case, deliberately: a name in the question, a name in the code. It is
the control the harder groups are compared against, so these must be clean — one member, unambiguous, and
answerable by anyone who found the right file.

- Ask "where is X implemented", "what does X return", "which type owns X".
- The `Member` expectation is the point of the question, not decoration.
- No memorisation trap here. This group is not trying to catch a model out; it is establishing the floor.
- Prefer members with distinctive names over `Handle`, `Run` or `Process` — a name three hundred types share
  measures the tie-breaking rule, not retrieval.
