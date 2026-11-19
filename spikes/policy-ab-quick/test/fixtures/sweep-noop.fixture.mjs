// A test file that does nothing: `start-sweep.test.mjs` runs the minigit runner (`spikes/minigit-bench/run-tests.mjs`) on it to see the runner's start sweep retire a killed test's folder (it is not named `*.test.mjs`, so no suite runs it itself).
import { test } from 'node:test';

test('nothing', () => {});
