# S4-t2 — code notes (E5.5/E5.6, S5.6.8)

Commit: 9346bcac on tmp/S4-t2 (two files only: runtimeCapabilities.generated.ts, capabilityAdmission.test.ts). Not pushed.

## Done
- Baseline `npm test` (before edits) red as the document says: `the generated table is the seed…`,
  `kimi × none / disk / web`. Two reds the document did not list, same cause (stale generated file):
  `the vectors and the seed actually loaded` (21 !== 18) and `generate-runtime-capabilities.mjs still produces the file that is committed`.
- Three test edits: runtimes list (line 143), gemini refusal regex (line 166), new test `kimi rows admit flagged, web refuses naming its record` after the caveat test.
- Red before the generator: the runtimes list (`Expected values to be strictly deep-equal`), the gemini regex, and the new test (`'kimi' is not a runtime the question consultant can launch for 'none'`). Logs: notes/S4-t2-red-admission.log.
- Generator run (not hand-edited): `21 rows over 7 runtimes × 3 capabilities`. Three `runtime: 'kimi'` rows at the end.
- `generate-runtime-capabilities.mjs --check`: exit 0.
- `npm run typecheck`: exit 0. `npm run lint`: exit 0.
- `npm test`: only `the baseline names no release that was never tagged` fails (known, no git tags). Logs: notes/S4-t2-green-test.log.
- No other extension test failed on `kimi` being admitted.

## Not done / left
- Nothing in scope. The `npm test` run showed only the no-tags failure. The two MCP tests the document names did not appear in its output, so I did not check them.
