// The scan behind no-shell.test.mjs: every way a spike module could hand a string to a shell interpreter.
// Comments are blanked first (line numbers kept), so a word in prose is never a route.
//
//   child-process-name         an import of anything but the argv launchers (spawn, spawnSync, execFile, execFileSync)
//   child-process-whole-module a namespace, default or non-destructured import: the names it reaches cannot be checked
//   shell-package              execa, shelljs, zx ...: a package that runs command strings
//   shell-key                  a `shell` option whose value is not literally `false`
//   cmd-exe-literal            the string 'cmd.exe' anywhere
//   shell-program-flag         a spawn/exec call of a shell program (sh, bash, ksh, pwsh, powershell, cmd ..., by literal, path or a
//                              constant) with a `-c`, `-lc`, `/c`, `-Command` style flag
import { stripComments } from './scan.mjs';

export const ALLOWED_CHILD_PROCESS = Object.freeze(['spawn', 'spawnSync', 'execFile', 'execFileSync']);
const SHELL_PROGRAMS = new Set(['sh', 'bash', 'dash', 'zsh', 'ksh', 'csh', 'tcsh', 'fish', 'ash', 'cmd', 'pwsh', 'powershell']);
const SHELL_PACKAGES = new Set(['execa', 'shelljs', 'zx', 'exec-sh', 'child-process-promise', 'child-process-promises']);
const CP = String.raw`['"](?:node:)?child_process['"]`;
const SHELL_FLAG = /^(?:-[A-Za-z]*c[A-Za-z]*|\/[cdk]|-Command|-EncodedCommand)$/i;
const LAUNCH_CALL = /\b(?:spawn|spawnSync|execFile|execFileSync)\s*\(\s*([^,()]+?)\s*,\s*\[([^\]]*)\]/g;

const namesIn = list => list.split(',').map(n => n.trim().replace(/^type\s+/, '').split(/\s+as\s+/)[0].trim()).filter(Boolean);
const row = (rule, index) => ({ rule, index });

function importRoutes(text) {
  const rows = [];
  const named = (list, index) => namesIn(list).filter(n => !ALLOWED_CHILD_PROCESS.includes(n)).forEach(() => rows.push(row('child-process-name', index)));
  for (const m of text.matchAll(new RegExp(String.raw`\b(?:import|export)\s*(?:(\w+)\s*,\s*)?\{([^}]*)\}\s*from\s*${CP}`, 'g'))) {
    if (m[1]) rows.push(row('child-process-whole-module', m.index));
    named(m[2], m.index);
  }
  for (const m of text.matchAll(new RegExp(String.raw`\bimport\s+\w+\s+from\s*${CP}`, 'g'))) rows.push(row('child-process-whole-module', m.index));
  for (const m of text.matchAll(new RegExp(String.raw`\b(?:import|export)\s*\*\s*(?:as\s+\w+\s+)?from\s*${CP}`, 'g'))) rows.push(row('child-process-whole-module', m.index));
  for (const m of text.matchAll(new RegExp(String.raw`\{([^}]*)\}\s*=\s*(?:await\s+)?(?:require|import)\(\s*${CP}\s*\)`, 'g'))) named(m[1], m.index);
  for (const m of text.matchAll(new RegExp(String.raw`(?:require|import)\(\s*${CP}\s*\)`, 'g'))) {
    if (!/\{[^}]*\}\s*=\s*(?:await\s+)?$/.test(text.slice(0, m.index))) rows.push(row('child-process-whole-module', m.index));
  }
  const pkg = String.raw`(?:from\s*|require\(\s*|import\(\s*)['"]([^'"]+)['"]`;
  for (const m of text.matchAll(new RegExp(pkg, 'g'))) if (SHELL_PACKAGES.has(m[1])) rows.push(row('shell-package', m.index));
  return rows;
}

const keyRoutes = text => [
  ...[...text.matchAll(/['"]?\bshell['"]?\s*:\s*(?!false\b|[[{])\S/g)].map(m => row('shell-key', m.index)),
  ...[...text.matchAll(/[{,]\s*shell\s*[,}]/g)].map(m => row('shell-key', m.index)),
  ...[...text.matchAll(/['"`]cmd\.exe['"`]/gi)].map(m => row('cmd-exe-literal', m.index)),
];

const constants = text => new Map([...text.matchAll(/\b(?:const|let|var)\s+(\w+)\s*=\s*(['"])([^'"]+)\2/g)].map(m => [m[1], m[3]]));
const baseName = exe => exe.split(/[\\/]/).pop().replace(/\.exe$/i, '').toLowerCase();

function programFlagRoutes(text) {
  const named = constants(text);
  return [...text.matchAll(LAUNCH_CALL)].flatMap(m => {
    const literal = /^['"`]([^'"`]*)['"`]$/.exec(m[1].trim());
    const exe = literal ? literal[1] : named.get(m[1].trim());
    const flags = [...m[2].matchAll(/['"`]([^'"`]*)['"`]/g)].map(f => f[1]);
    return exe && SHELL_PROGRAMS.has(baseName(exe)) && flags.some(f => SHELL_FLAG.test(f)) ? [row('shell-program-flag', m.index)] : [];
  });
}

const lineOf = (text, index) => text.slice(0, index).split('\n').length;

/** Every shell route in `source`, as `{ rule, line }`, in source order. */
export function shellRoutes(source) {
  const text = stripComments(source);
  return [...importRoutes(text), ...keyRoutes(text), ...programFlagRoutes(text)]
    .map(r => ({ rule: r.rule, line: lineOf(text, r.index) })).sort((a, b) => a.line - b.line);
}
