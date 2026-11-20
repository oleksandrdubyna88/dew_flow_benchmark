// The leak check over the pre-registration's registered files (F3.S1 (b), PR 5; D41): no case id, no trap name and no case line in any registered file. The files are the ones `STAGE_PREREG_FILES` names, found on disk, and they go through
// the leak check's own tiers (`scanFiles` over the material of the held-out side), so there is no second scanner. The material is built by the caller from the sealed side (`leakcheck` does it); a test builds it from a fixture.
import { existsSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { publicFinding, scanFiles, uniqueFindings } from './leak-scan.mjs';
import { STAGE_PREREG_FILES } from './stage-lists.mjs';

/** The registered files that exist under `root`, as paths from it: a folder entry stands for every file of its folder that matches. */
export function registeredFilesOn(root) {
  return STAGE_PREREG_FILES.flatMap(entry => {
    if (typeof entry === 'string') return existsSync(path.join(root, entry)) ? [entry] : [];
    const dir = path.join(root, entry.dir);
    return existsSync(dir) ? readdirSync(dir).filter(name => entry.pattern.test(name)).sort().map(name => `${entry.dir}/${name}`) : [];
  });
}

/** The findings over the registered files of `root` through every tier of the material: `{ findings, files, incomplete }` (public form: a place and a tier, never the private text). `incomplete` lists registered files the scan could not read: an incomplete scan is never clean. */
export function preregLeakFindings({ root, material }) {
  const files = registeredFilesOn(root);
  const scanned = scanFiles({ root, files, material });
  return { findings: uniqueFindings(scanned.findings).map(publicFinding), files, incomplete: scanned.unreadable };
}
