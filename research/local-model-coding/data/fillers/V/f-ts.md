Write one test for a TypeScript project using node:test and node:assert/strict. Output only the test in one ```ts block.

The file `capabilityAdmission.test.ts` already imports `test` from 'node:test', `assert` from 'node:assert/strict', and has a function `admit(runtime: string, capability: string)` returning `{ admitted: boolean, reason: string, caveat: string, ... }` (for a refused pair `admitted` is false and `reason` is the sentence; for an admitted flagged pair `caveat` is non-empty and says "can read this machine"). Existing tests for style:
```ts
test('a flagged admission carries the caveat a person must acknowledge (D13)', () => {
  assert.match(admit('codex', 'web').caveat, /can read this machine/u);
  assert.equal(admit('claude', 'web').caveat, '', 'a confined pair has nothing to acknowledge');
});
test('a refusal says what cures it', () => {
  assert.match(admit('vibe', 'web').reason, /not been measured/u, 'no Vibe cell exercises the web');
});
```
Write the test named exactly `kimi rows admit flagged, web refuses naming its record`: `admit('kimi', 'none')` and `admit('kimi', 'disk')` are admitted (`.admitted === true`) with a caveat matching /can read this machine/u; `admit('kimi', 'web')` is not admitted and its reason matches /not been measured/u and /RESULTS_kimi_confinement\.md/u.
