// The shape of the tree a push carries. A submodule is a pointer to code that no scan here can read, so the only one allowed is the
// conventions mount, from the conventions repository; anything else is refused by path (or, in `.gitmodules`, by line).
import { SUBMODULE_MODE } from './push-range.mjs';

export const CONVENTIONS_PATH = '.agents/conventions';
export const CONVENTIONS_URL = 'https://github.com/oleksandrdubyna88/dew_flow_conventions.git';
const GITMODULES = '.gitmodules';
const KEY_VALUE = /^\s*(path|url)\s*=\s*(.*?)\s*$/;

function gitmodulesProblems(text) {
  const expected = { path: CONVENTIONS_PATH, url: CONVENTIONS_URL };
  const label = { path: 'submodule path other than the conventions mount', url: 'submodule URL other than the conventions repository' };
  return text.split('\n').flatMap((line, i) => {
    const [, key, value] = KEY_VALUE.exec(line) ?? [];
    return key !== undefined && value !== expected[key] ? [{ path: GITMODULES, line: i + 1, what: label[key] }] : [];
  });
}

/** Findings `{ path, line, what }` for every submodule pointer but the conventions mount and every `.gitmodules` entry that is not that mount. */
export function findSubmoduleProblems({ tip, gitmodulesText }) {
  const links = [...tip].filter(([path, entry]) => entry.mode === SUBMODULE_MODE && path !== CONVENTIONS_PATH);
  return [
    ...links.map(([path]) => ({ path, line: null, what: 'submodule outside the conventions mount' })),
    ...(gitmodulesText === null ? [] : gitmodulesProblems(gitmodulesText)),
  ];
}
