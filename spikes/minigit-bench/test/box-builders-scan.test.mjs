// The structural guard of the E2 amendment of 2026-11-19 (D-9; reuse-first: "enumerate the sites mechanically and leave the enumeration behind as a test"). A box that runs a toolchain shows ONE
// dotnet version because its builder mounts the list of `boxDotnetHide` over the others; that holds only while every bwrap argv of the two spikes is built by one of the builders named here. So the
// scan reads every production `.mjs` of `spikes/minigit-bench` and `spikes/lang-explore` (comments stripped, lines kept) and fails, naming file and line, when
//   - anything that composes a bwrap argv (the word `bwrap` opening an argv or handed to a spawn, a bwrap flag written as a literal, a primitive of `lib/bwrap.mjs` called) sits in a file that is not
//     in the table below, or a second argv is opened anywhere, the table's files included;
//   - one of the two toolchain builders (`agentBoxPrefix`, `scoreBoxArgv`) no longer spreads `boxDotnetHide(...)` into its tmpfs list, or the list is computed anywhere else.
// Each prohibition has its companion: the scan still finds every sanctioned site, and it names a bypass planted into a copy of a real file. No folder is made: the copies are strings.
// Outside this scan: `spikes/lang-bench` builds boxes of its own (the execution layer; a pinned SDK directory, the whole distribution hidden) and is another harness's to guard.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { spikeDir } from './helpers.mjs';
import { sourceFiles, stripComments } from './scan.mjs';

const SPIKES = path.join(spikeDir, '..');
const rel = file => path.relative(SPIKES, file).split(path.sep).join('/');
/** Every production source of the two spikes as `{ rel, text }` (the tests are not production: they take argvs apart on purpose). */
const production = () => ['minigit-bench', 'lang-explore'].flatMap(spike => sourceFiles(path.join(SPIKES, spike), ['test'])).map(file => ({ rel: rel(file), text: readFileSync(file, 'utf8') }));

/** What composes a bwrap argv, each as `[kind, pattern]`. */
const SHAPES = Object.freeze([
  ['an argv that opens with bwrap', /\[\s*['"`]bwrap['"`]\s*,/g],
  ['bwrap handed to a call as the program', /\(\s*['"`]bwrap['"`]\s*,/g],
  ['a bwrap flag as a literal', /['"`]--(?:ro-bind(?:-try)?|bind(?:-try)?|dev-bind(?:-try)?|tmpfs|unshare-[a-z]+|die-with-parent|new-session|clearenv|setenv)['"`]/g],
  ['a primitive of lib/bwrap.mjs called', /(?<![\w$])(?:roRoot|devProc|isolation|setenvAll|hiddenPaths|roBind|tmpfs)\(/g], // `...roRoot()` is a call too: only a longer name is not
]);

const lineAt = (code, index) => code.slice(0, index).split('\n').length;
const DECLARATION = /export (?:async )?function (\w+)\s*\(|export const (\w+)\s*=/g;
/** The exported name declared last before `index`: the function a site belongs to. */
const ownerAt = (code, index) => [...code.slice(0, index).matchAll(DECLARATION)].map(m => m[1] ?? m[2]).at(-1) ?? '(top of the file)';

/** Every site of every shape in the sources: `{ rel, line, kind, owner, text }`, in file and line order. */
function sitesIn(sources) {
  return sources.flatMap(({ rel: file, text }) => {
    const code = stripComments(text);
    return SHAPES.flatMap(([kind, pattern]) => [...code.matchAll(pattern)].map(m => ({ rel: file, line: lineAt(code, m.index), kind, owner: ownerAt(code, m.index), text: code.slice(m.index, m.index + 60).split('\n')[0] })));
  }).sort((a, b) => a.rel.localeCompare(b.rel) || a.line - b.line);
}
const named = sites => sites.map(s => `${s.rel}:${s.line} ${s.kind} (in ${s.owner})`);

/** The files that may compose a bwrap argv, each with what it does and why that is no second road to a toolchain box. */
const SANCTIONED = Object.freeze({
  'minigit-bench/lib/bwrap.mjs': 'the primitives, and the scoring box: scoreBoxArgv',
  'minigit-bench/lib/sandbox.mjs': 'the agent\'s box: agentBoxPrefix (agentBoxArgv appends the command to it); boxView READS the flags of a recorded argv',
  'minigit-bench/lib/preflight.mjs': '`bwrap --version`, asked before any box is built: it builds none',
  'lang-explore/lib/oracle.mjs': 'bruteBoxArgv: the box of `python3 -I brute.py` over private material; PATH is /usr/bin:/bin, no toolchain is bound and nothing is built or scored in it, so it takes no pin',
  'lang-explore/lib/score.mjs': 'withEnv: replaces or adds `--setenv` triples in an argv scoreBoxArgv built; it adds no bind, no mount and no program',
});
/** The three places an argv is opened: file -> the builder. Two run a toolchain and must take the dotnet list; the third is named with its reason above. */
const BUILDERS = Object.freeze({ 'lang-explore/lib/oracle.mjs': 'bruteBoxArgv', 'minigit-bench/lib/bwrap.mjs': 'scoreBoxArgv', 'minigit-bench/lib/sandbox.mjs': 'agentBoxPrefix' });
const TOOLCHAIN_BUILDERS = Object.freeze([['minigit-bench/lib/sandbox.mjs', 'agentBoxPrefix'], ['minigit-bench/lib/bwrap.mjs', 'scoreBoxArgv']]);

/** The sites that are outside the table, or open a second argv. */
function bypasses(sources) {
  const sites = sitesIn(sources);
  const opened = sites.filter(s => s.kind === SHAPES[0][0]);
  return [...new Set([...sites.filter(s => !Object.hasOwn(SANCTIONED, s.rel)), ...opened.filter(s => BUILDERS[s.rel] !== s.owner)])]; // a site is named once, whichever rule it breaks
}

/** The text of an exported function, from its declaration to the closing brace at the start of a line. */
function bodyOf(code, name) {
  const start = code.search(new RegExp(`export function ${name}\\s*\\(`));
  assert.ok(start >= 0, `${name} is an exported function`);
  return code.slice(start, code.indexOf('\n}', start));
}
const TAKES_THE_LIST = /\.\.\.boxDotnetHide\(\{[^}]*\bdotnetPin\b[^}]*\}\)\.flatMap\(tmpfs\)/;
/** The toolchain builders, of these sources, that do not spread the list into their argv. */
const withoutTheList = sources => TOOLCHAIN_BUILDERS.filter(([file, name]) => !TAKES_THE_LIST.test(bodyOf(stripComments(sources.find(s => s.rel === file).text), name))).map(([file, name]) => `${file}: ${name}`);

const callers = (sources, call) => sources.filter(s => new RegExp(`(?<![\\w$])${call}\\(`).test(stripComments(s.text).replace(new RegExp(`(?:function|const) ${call}\\b[^\\n]*`, 'g'), ''))).map(s => s.rel).sort();

test('No_production_file_of_the_two_spikes_composes_a_bwrap_argv_outside_the_named_builders', () => {
  const sources = production();
  assert.ok(sources.length > 200, `${sources.length} production sources read: the walk covers both spikes`);
  assert.deepEqual(named(bypasses(sources)), [], 'a bwrap argv is built through agentBoxPrefix / agentBoxArgv or scoreBoxArgv; a new builder joins the table with its reason, and the dotnet list if it runs a toolchain');
});

test('The_scan_still_finds_every_sanctioned_site_and_the_three_builders_by_name', () => {
  const sites = sitesIn(production());
  assert.deepEqual([...new Set(sites.map(s => s.rel))].sort(), Object.keys(SANCTIONED).sort(), 'every file of the table still holds a site: a row that names nothing is removed, not kept');
  const opened = sites.filter(s => s.kind === SHAPES[0][0]);
  assert.deepEqual(Object.fromEntries(opened.map(s => [s.rel, s.owner])), { ...BUILDERS });
  assert.equal(opened.length, 3, named(opened).join('; '));
  const programs = sites.filter(s => s.kind === SHAPES[1][0]);
  assert.deepEqual(programs.map(s => [s.rel, /^\(\s*'bwrap'\s*,\s*\['--version'\]\)/.test(s.text)]), [['minigit-bench/lib/preflight.mjs', true]], 'bwrap is handed to a call as the program once: the version probe');
  for (const kind of SHAPES.map(([k]) => k)) assert.ok(sites.some(s => s.kind === kind), `the pattern "${kind}" still matches something`);
  assert.deepEqual([...new Set(sites.filter(s => s.rel === 'lang-explore/lib/score.mjs').map(s => s.text.slice(0, 10)))], ["'--setenv'"], 'what lang-explore\'s score.mjs touches of a box is its --setenv triples, and nothing else');
});

test('Both_toolchain_builders_take_the_dotnet_list_from_boxDotnetHide_and_nothing_else_computes_one', () => {
  const sources = production();
  assert.deepEqual(withoutTheList(sources), []);
  assert.deepEqual(callers(sources, 'boxDotnetHide'), ['minigit-bench/lib/bwrap.mjs', 'minigit-bench/lib/sandbox.mjs'], 'the decision is taken by the two builders only');
  assert.deepEqual(callers(sources, 'otherDotnetVersions'), ['minigit-bench/lib/dotnet-hide.mjs'], 'and the list is computed in one place: no caller keeps a list of its own');
  const oracle = stripComments(sources.find(s => s.rel === 'lang-explore/lib/oracle.mjs').text);
  assert.match(oracle, /setenvAll\(\{ PATH: '\/usr\/bin:\/bin'/, 'the third builder binds no toolchain');
  assert.match(oracle, /\[\.\.\.box\.slice\(1\), python, '-I', heldout\.bruteFile/, 'and its one user runs python on brute.py: the reason it is in the table without the list');
});

/** A copy of a real production file with `more` appended: `{ rel, text }` and the line the addition starts on. */
function planted(sources, file, more) {
  const original = sources.find(s => s.rel === file);
  assert.ok(original, `${file} is a production source`);
  return { sources: sources.map(s => (s.rel === file ? { rel: file, text: `${original.text}\n${more}\n` } : s)), line: original.text.split('\n').length + 1 };
}

test('A_bypass_planted_into_a_copy_of_a_real_file_is_named_with_its_file_and_line_whatever_its_shape', () => {
  const sources = production();
  const cases = [
    ['lang-explore/lib/plan.mjs', "export const sneakyBox = dir => ['bwrap', '--ro-bind', '/', '/', '--bind', dir, dir, '--'];"],
    ['lang-explore/lib/run-ctx.mjs', "export const sneakySpawn = (spawn, dir) => spawn('bwrap', [...args(dir), '--', 'dotnet', 'build']);"],
    ['minigit-bench/lib/session-launch.mjs', "const sneakyFlags = dir => [BWRAP, \"--unshare-net\", `--tmpfs`, dir];"],
    ['minigit-bench/lib/score-steps.mjs', "const sneakyPrimitives = dir => [exe, ...roRoot(), ...roBind(dir), '--'];"],
    ['minigit-bench/lib/sandbox.mjs', "export const secondAgentBox = dir => Object.freeze([\n  'bwrap', ...roRoot(), ...roBind(dir), '--']);"],
  ];
  for (const [file, more] of cases) {
    const copy = planted(sources, file, more);
    const found = named(bypasses(copy.sources));
    assert.ok(found.length > 0 && found.every(f => f.startsWith(`${file}:${copy.line} `) || f.startsWith(`${file}:${copy.line + 1} `)), `${file}:${copy.line} is named, and only it: ${found.join('; ')}`);
  }
  // a builder that loses the list, in a copy: the line is the one of sandbox.mjs and bwrap.mjs that carries it
  for (const [file, name] of TOOLCHAIN_BUILDERS) {
    const without = sources.map(s => (s.rel === file ? { rel: file, text: s.text.replace(/^.*\.\.\.boxDotnetHide\(.*\n/m, '') } : s));
    assert.notEqual(without.find(s => s.rel === file).text, sources.find(s => s.rel === file).text, `${file} holds the line`);
    assert.deepEqual(withoutTheList(without), [`${file}: ${name}`]);
  }
});
