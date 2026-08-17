# Group brief — semantic-intent

Questions asked in BEHAVIOUR, never naming the thing that implements it.

This is the group the whole retrieval argument turns on: if a question contains the identifier, a text search
answers it and semantic retrieval has nothing to prove. So the prompt must describe what the code DOES and
avoid the words the code uses.

- Ask "how does this system decide when to give up retrying", not "what does RetryHelper do".
- **Name no identifier from the target code in the prompt.** If the member is `DecorrelatedJitterBackoffV2`,
  neither "decorrelated" nor "jitter" may appear in the question.
- A memorisation trap belongs here when the behaviour has a famous textbook answer that differs from this
  repository's: express it as `AnswerExcludes` on the textbook term.
- The `referenceAnswer` should read like an engineer explaining the mechanism, not like a docstring.
