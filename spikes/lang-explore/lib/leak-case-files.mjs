// The case files of a held-out folder, as the leak tiers read them: `.in` and `.out`, sorted, and each file's lines. One reader, so the material (built once) and the verify step (which reads again) number lines the same way.
import { readdirSync } from 'node:fs';
import path from 'node:path';
import { readLines } from './leak-text.mjs';

/** The case files of a folder (`.in` and `.out`, sorted), as names. */
export const caseFilesOf = casesDir => readdirSync(casesDir).filter(f => f.endsWith('.in') || f.endsWith('.out')).sort();

/** The lines of one case file, as latin1 text split at `\n` (the 1-based physical line is the index plus one). */
export const caseLinesOf = (casesDir, name) => readLines(path.join(casesDir, name), 'latin1');
