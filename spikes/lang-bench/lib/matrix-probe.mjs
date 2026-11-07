// F1.S3 (a): one probe of the package matrix. A copy of the template of (stack, kind) with a one-line program has the package added the way an agent adds it (a `PackageReference` in `App.csproj`; `npm install NAME@VERSION`),
// and `bash build.sh` runs in the build box over the registered snapshot with no registry (`inBuildBox`). Exit 0 is `works`; any other exit is `unsupported` with the first code the output names. A probe that cannot run (the box
// did not start, the build timed out) is a refusal, never a row. An `unsupported` result is probed a second time and must give the same code: a row is a measurement, not a flake.
import { cpSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { inBuildBox } from './workspace-box.mjs';

const TEMPLATES = path.join(fileURLToPath(new URL('..', import.meta.url)), 'templates');
const PROGRAM = Object.freeze({ csharp: ['Program.cs', 'System.Console.WriteLine("ok");\n'], node: ['src/main.ts', "console.log('ok');\n"] });
const CODE_PATTERNS = Object.freeze([/error (NU\d{4})/, /npm error code (E[A-Z0-9_]+)/, /error (TS\d+)/, /error (CS\d+)/, /error (MSB\d+)/, /\b(NU\d{4})\b/]);

/** The first code the output of a failed build names (an `error NU1603`, an npm `code ENOTCACHED`, a TypeScript, C# or MSBuild error), else `exit <n>`. */
export function codeOf(output, exit) {
  for (const pattern of CODE_PATTERNS) {
    const found = pattern.exec(output);
    if (found !== null) return found[1];
  }
  return `exit ${exit}`;
}

/** An `App.csproj` text with a `PackageReference` added in an `ItemGroup` of its own; the text as it is when the project already references the package. */
export function withPackage(text, name, version) {
  if (text.toLowerCase().includes(`include="${name.toLowerCase()}"`)) return text;
  return text.replace(/\s*<\/Project>\s*$/, `\n  <ItemGroup>\n    <PackageReference Include="${name}" Version="${version}" />\n  </ItemGroup>\n</Project>\n`);
}

/** The template of (stack, kind) copied into `view` with its one-line program; for C# the package is added to `App.csproj`. */
export function prepareView({ view, stack, kind, name, version }) {
  cpSync(path.join(TEMPLATES, stack, kind), view, { recursive: true });
  const [program, text] = PROGRAM[stack];
  mkdirSync(path.dirname(path.join(view, program)), { recursive: true });
  writeFileSync(path.join(view, program), text);
  if (stack !== 'csharp') return;
  const project = path.join(view, 'App.csproj');
  writeFileSync(project, withPackage(readFileSync(project, 'utf8'), name, version));
}

const references = (view, name) => readFileSync(path.join(view, 'package.json'), 'utf8').includes(`"${name}"`);

/** The commands to run in the box for one probe: the install an agent runs (Node, when the template does not list the package), then the build. */
export function probeCommands({ view, stack, name, version }) {
  const build = ['bash', 'build.sh'];
  if (stack === 'csharp' || references(view, name)) return [build];
  return [['npm', 'install', `${name}@${version}`, '--no-audit', '--no-fund'], build];
}

const outputOf = ran => `${ran.stdout}\n${ran.stderr}`;

/** The commands one after another in one view: the first that exits non-zero ends it. -> `ok({ code, output })` | `fail`. */
async function runAll({ commands, box }) {
  let last = { code: 0, output: '' };
  for (const command of commands) {
    const ran = await box(command);
    if (!ran.ok) return ran;
    if (ran.value.timedOut) return fail(`${command.join(' ')} timed out`);
    last = { code: ran.value.code, output: outputOf(ran.value) };
    if (last.code !== 0) return ok(last);
  }
  return ok(last);
}

/** One probe in a view of its own, removed when the probe ends: `ok({ state: 'works' })` | `ok({ state: 'unsupported', code })` | `fail`. `work` is the folder the view is made in. */
export async function probeOnce({ owned, work, dirs, stack, kind, name, version, record, caches, timeoutMs = 600_000, inBox = inBuildBox }) {
  const scope = cleanupScope();
  try {
    const view = owned.ownedDir(scope, 'mtx', { parent: work });
    prepareView({ view, stack, kind, name, version });
    const box = command => inBox({ owned, view, dirs, stack, record, caches, command, timeoutMs });
    const ran = await runAll({ commands: probeCommands({ view, stack, name, version }), box });
    if (!ran.ok) return ran;
    return ok(ran.value.code === 0 ? { state: 'works' } : { state: 'unsupported', code: codeOf(ran.value.output, ran.value.code) });
  } finally {
    await scope.close();
  }
}

/** A probe whose `unsupported` result is confirmed by a second, fresh probe giving the same code. */
export async function probeConfirmed(args) {
  const first = await probeOnce(args);
  if (!first.ok || first.value.state === 'works') return first;
  const again = await probeOnce(args);
  if (!again.ok) return again;
  return again.value.state === first.value.state && again.value.code === first.value.code ? first : fail(`${args.stack}/${args.kind}/${args.name}: two probes disagree (${JSON.stringify(first.value)} and ${JSON.stringify(again.value)})`);
}
