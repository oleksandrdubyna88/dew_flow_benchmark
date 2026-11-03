// A test file that does nothing: `sweep.test.mjs` runs `run-tests.mjs` on it to see the suite-start sweep retire a killed probe test's leftovers (it is not named `*.test.mjs`, so the suite does not run it itself).
import { test } from 'node:test';

test('nothing', () => {});
