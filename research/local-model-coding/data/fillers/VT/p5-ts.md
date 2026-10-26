You write one TypeScript test for the node:test runner (`import test from 'node:test'; import assert from 'node:assert/strict';`). Output ONLY one code block with the test, no explanation.

Existing tests in the file `capabilityAdmission.test.ts` (same style, copy it):
```ts
test('a refusal says what cures it: a measurement for an unmeasured pair, another prompt for an unsupported one', () => {
  assert.match(admit('api', 'web').reason, /not been measured/u);
  assert.match(admit('api', 'web').reason, /RESULTS_question_consultant_capabilities\.md/u);
  assert.match(admit('antigravity', 'web').reason, /cannot fetch a page headless/u);
  assert.match(admit('vibe', 'web').reason, /not been measured/u, 'no Vibe cell exercises the web (S3 of PLAN_vibe_and_kimi_runtimes.md)');
});

test('a flagged admission carries the caveat a person must acknowledge (D13)', () => {
  assert.match(admit('codex', 'web').caveat, /can read this machine/u);
  assert.equal(admit('claude', 'web').caveat, '', 'a confined pair has nothing to acknowledge');
});
```
`admit(runtime, capability)` returns an object with `admitted` (boolean), `flag` (string: '' | 'unconfined' | 'default-deny'), `caveat` (string, non-empty for a flagged admission and saying it can read this machine for the unconfined flag), and `reason` (string, for a refusal).

Write ONE test named exactly: `kimi rows admit flagged, web refuses naming its record`
- for `'none'` and `'disk'` (loop): `admit('kimi', capability)` has `admitted === true`, `flag === 'unconfined'`, and its `caveat` matches `/can read this machine/u`.
- `admit('kimi', 'web')`: `admitted === false`, its `reason` matches `/not been measured/u` and matches `/RESULTS_kimi_confinement\.md/u`.
Use short assertion messages as in the examples.
