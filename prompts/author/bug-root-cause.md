# Group brief — bug-root-cause

Questions that describe a SYMPTOM and expect the member responsible for it.

The retrieval this measures is the one that matters most in practice: a person arrives with a stack trace or a
misbehaviour, not with a member name. The question should read like a bug report, and the answer should be the
place a maintainer would actually go.

- Ask "requests occasionally hang when the client disconnects mid-stream — where is that handled".
- The prompt describes the symptom in the user's words. It must not name the member, the type, or the file.
- Anchor at the member a fix would touch, not at the throw site, when the two differ — and say which in the
  `referenceAnswer`, because that distinction is the answer.
- A trap fits well here: the obvious suspect and the real cause are often different members.
