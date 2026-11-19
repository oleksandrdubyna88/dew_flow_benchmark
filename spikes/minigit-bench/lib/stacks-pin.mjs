// v1's pinned SDK version, read from `inputs/stacks.json` (the pin the preflight checks inside the box). The ONE reader for the paths that have no run context to take the pin from: a snapshot made
// with no box options (`cache-snapshot.mjs`), and the scorer run by hand or by the held-out pipeline (`score-deps.mjs`). A run hands its own pin on (`ctx.dotnetPin`), read from the same file.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const STACKS = fileURLToPath(new URL('../inputs/stacks.json', import.meta.url));

/** -> the pinned SDK version (`pins.dotnetSdk`); a file that cannot be read or parsed throws, naming it. */
export const v1SdkPin = (file = STACKS) => JSON.parse(readFileSync(file, 'utf8')).pins.dotnetSdk;
