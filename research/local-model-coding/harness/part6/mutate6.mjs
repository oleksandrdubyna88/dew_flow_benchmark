#!/usr/bin/env node
// Part 6 — FROZEN behavioural checks (mutation checks) on each arm's FINAL code.
//
// FROZEN 2026-10-29 (before any mutant was built or run against any arm). The mutant list, the anchors per arm, the intended
// scenarios and the test scope below are not changed after any arm's result has been seen; a mutant that proves unusable in
// an arm is reported NOT APPLICABLE for that arm, with the reason.
//
// The question per arm: when ONE decision of todo/PLAN_a_spent_quota_cools_the_model_down.md (frozen plan 6786582e) is broken
// in the arm's final code, do the arm's OWN tests notice?
//
//   node mutate6.mjs check        → reads every arm's HEAD (git show, nothing written) and reports whether each anchor applies
//   node mutate6.mjs <ARM>        → mut6/<ARM>/mutation.json
//   node mutate6.mjs all          → every arm, at most two at a time, skipping an arm whose mutation.json is complete
//
// Method (the earlier parts' harness, dew_flow_benchmark/research/local-model-coding/harness/mutate.mjs, adapted):
// - a `git clone --shared` of the arm's HEAD under mut6/<ARM>/testsToDelete-mut6-<ARM> (the arm's clone, branch and r/<token>/
//   are only READ); the conventions submodule is cloned from the arm's own checkout of it;
// - the MCP test project is built (dotnet build -c Debug -m:4 --disable-build-servers) and its MTP executable run (never
//   `dotnet test`), every process under .agents/conventions/tools/low-priority.mjs, with --max-threads 4 so two arms at once stay
//   within 8 test threads (common.local-cpu-budget);
// - scope: the whole MCP suite takes ~8 min here, so each run is scoped to: every test class in a src_mcp/tests file the arm
//   added or changed since BASE, every class holding a C# scenario test (scenarios.json, by method name), and every test class
//   whose file name matches AREA below — one rule, applied to each arm's own tree;
// - the baseline run once, unmutated; then per mutant: the tree restored, the edits applied (each `find` must occur EXACTLY
//   once, else NOT APPLICABLE), the test project built (a build failure is NOT APPLICABLE: does not compile), the assembly
//   holding the edited file checked to be newer than the edit (a stale binary is NOT APPLICABLE, never a survivor), the same
//   classes run, and every test that passed in the baseline and failed now re-run alone on the same mutated binary: one that
//   passes on the re-run is reported as `flaky` and does not count;
// - KILLED = at least one baseline-passing test fails, confirmed on the re-run. The intended tests (the scenarios the mutant
//   breaks) are reported apart: present in the run, and caught or not;
// - the never-true guard is `DateTime.UtcNow.Year < 0`, the always-true one `DateTime.UtcNow.Year > 0` (the compiler cannot prove
//   either constant, so no unreachable-code warning under TreatWarningsAsErrors); every original symbol stays referenced.
//
// PREDICTION (recorded before the run, common.measurement rule 4): M1, M2, M3 and M7 are killed in all nine arms — each breaks
// a pure decision a named scenario asserts directly (S1/S2, S6, S14/S34, S12). M8 is killed in all nine (S26 asserts the
// exclusion sentence). M4, M5 and M6 are where survivors are most likely: M4 only if an arm's S19 reads the torn file some way
// other than Standing; M5 because S25's "then an Ok result → null" half is easy to assert through Lift() directly instead of
// the round; M6 if an arm's S41 asserts only one order. Expected: 7–8 of 8 killed per arm, no scheme difference beyond one
// mutant; at most two NOT APPLICABLE in total.
//
// NOT chosen, with the reason: D7 (Claude's spend envelope) — one runtime, one scenario, low behavioural reach; D8/the card
// (node, S30–S33) — the extension suite is a different runner and the question here is the server's decisions; "a reset_time
// without an offset is read as UTC" — the plan does not decide the no-offset case (every fixture carries Z; D6 says only "its
// offset kept, stored as UTC"), so a mutant there would break no decision of the plan; "a key belongs to one sign-in"
// (QuotaKeyOf keyed per row) — covered in effect by M3 (the key of a model) and the scenario set has no distinct test for it.
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdirSync, rmSync, statSync, readdirSync, appendFileSync } from 'node:fs';
import { dirname, join, basename } from 'node:path';

const E = '/home/jinx/git/coai-exp2';
const FROZEN = '2026-10-29';
const ARMS = ['C1', 'C2', 'C3', 'CR1', 'CR2', 'CR3', 'SRS1', 'SRS2', 'SRS3'];
const env = Object.fromEntries(readFileSync(`${E}/exp.env`, 'utf8').split('\n').map((l) => l.match(/^([A-Z_]+)=(\S*)$/)).filter(Boolean).map((m) => [m[1], m[2]]));
const BASE = env.BASE;
const MAP = JSON.parse(readFileSync(env.MAP, 'utf8'));
const SCENARIOS = Object.fromEntries(JSON.parse(readFileSync(`${E}/scenarios.json`, 'utf8')).map((s) => [s.id, s]));
const AREA = /Quota|Cooldown|Agy|Antigravity|RateLimit|Hopeless|Exclud|Ladder|BoundedScheduler|StatedReset|ClaudeReason|SpendLimit|GateReporting|ReviewerFailures|RefusalRoads|Providers|Roster|SkipReason/;
const TEST_EXE = './src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests';
const TEST_BIN = 'src_mcp/tests/bin/Debug/net10.0';
const NEVER = 'DateTime.UtcNow.Year < 0';
const ALWAYS = 'DateTime.UtcNow.Year > 0';

// ── the mutants (FROZEN 2026-10-29) ──────────────────────────────────────────────────────────────────────────────────────
const R = 'src_mcp/runners/Reviewers';
const PANEL = 'src_mcp/src/Server/PanelService.cs';
const e = (file, find, replace) => ({ file, find, replace });
const same = (edits) => Object.fromEntries(ARMS.map((a) => [a, edits]));
const orTrue = (file, expr) => e(file, expr, `(${expr} || ${ALWAYS})`);
const GEMINI_FAMILIES = '["gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.1-pro"]';
const argonRowToGemini = (file) => [
  e(file, GEMINI_FAMILIES, '["gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.1-pro", "gemini-4-argon"]'),
  e(file, '["gemini-4-argon"]', '["gemini-4-argon-moved-by-mutant"]'),
];
const argonDictToGemini = (file) => [e(file, '["gemini-4-argon"] = Argon,', '["gemini-4-argon"] = Gemini,')];
// M4: Standing first parses the file strictly, so an unparseable file throws out of Standing instead of reading as no cooldown.
// Injected at Standing — the plan's one read road (D4: "Every read — Standing from CanRun, from ProbeAsync … answers no
// cooldown") — so the write paths of every arm are left exactly as they are.
const strictStanding = (pathMember) => ({
  file: `${R}/QuotaCooldowns.cs`,
  regex: /( {4}public QuotaCooldownEntry\? Standing\(string key, DateTimeOffset \w+\) =>)([^;]*;)/,
  replaceWith: (m, decl, body) => `${decl} MutantStrictRead() ? null :${body}\n\n    private bool MutantStrictRead()\n    {\n`
    + `        if (System.IO.File.Exists(${pathMember}))\n        {\n`
    + `            System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(${pathMember})).Dispose();\n        }\n\n`
    + '        return false;\n    }',
});
const ROUND_FILE = {
  C1: `${R}/QuotaCooldowns.Round.cs`, C2: `${R}/QuotaRound.cs`, C3: `${R}/QuotaCooldowns.Round.cs`, CR1: `${R}/QuotaCooldowns.cs`,
  CR2: `${R}/QuotaCooldowns.cs`, CR3: `${R}/QuotaCooldowns.cs`, SRS1: `${R}/QuotaCooldowns.cs`, SRS2: `${R}/QuotaCooldownsRound.cs`, SRS3: `${R}/QuotaRound.cs`,
};

const MUTANTS = [
  {
    id: 'M1-ladder-ignores-spent', decision: 'D1',
    breaks: "the ladder's Hopeless reads only the words again: the structured Spent field no longer outranks the word scan",
    intended: ['S1', 'S2'],
    edits: same([e(`${R}/BoundedScheduler.cs`, '(limited.Spent ?? RateLimit.Hopeless(limited.Reason))', '(RateLimit.Hopeless(limited.Reason))')]),
  },
  {
    id: 'M2-exhausted-alone-is-spent', decision: 'D2',
    breaks: "agy: a RESOURCE_EXHAUSTED is classified Spent == true without the sentence 'Individual quota reached' (keyed on the status alone)",
    intended: ['S6'],
    edits: {
      C1: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuota, StringComparison.Ordinal)')],
      C2: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuota, StringComparison.OrdinalIgnoreCase)')],
      C3: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(QuotaSentence, StringComparison.Ordinal)')],
      CR1: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuota, StringComparison.Ordinal)')],
      CR2: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuota, StringComparison.Ordinal)')],
      CR3: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuota, StringComparison.OrdinalIgnoreCase)')],
      SRS1: [orTrue(`${R}/AntigravityRuntime.cs`, 'error.Message.Contains(IndividualQuota, StringComparison.OrdinalIgnoreCase)')],
      SRS2: [orTrue(`${R}/AgyError.cs`, 'Message.Contains(IndividualQuotaSentence, StringComparison.Ordinal)')],
      SRS3: [orTrue(`${R}/AntigravityRuntime.cs`, 'exhausted.Message.Contains(AgyError.IndividualQuota, StringComparison.Ordinal)')],
    },
  },
  {
    id: 'M3-argon-keyed-as-gemini', decision: 'D3',
    breaks: "the agy table maps the gemini-4-argon family to antigravity/gemini (the prefix-rule defect); the bucket ids per key are unchanged",
    intended: ['S14', 'S34'],
    edits: {
      C1: argonRowToGemini(`${R}/AgyQuotaReport.cs`), C2: argonDictToGemini(`${R}/AgyQuotaReport.cs`), C3: argonRowToGemini(`${R}/AgyQuotaReport.cs`),
      CR1: argonRowToGemini(`${R}/AgyQuotaReport.cs`), CR2: argonRowToGemini(`${R}/AgyQuotaReport.cs`), CR3: argonDictToGemini(`${R}/AgyQuotaReport.cs`),
      SRS1: argonDictToGemini(`${R}/AgyQuotaReport.cs`), SRS2: argonDictToGemini(`${R}/AgyQuotaReport.cs`), SRS3: argonRowToGemini(`${R}/AgyQuotaReport.cs`),
    },
  },
  {
    id: 'M4-torn-file-throws', decision: 'D4',
    breaks: 'a torn / unparseable quota-cooldowns.json no longer reads as "no cooldown": Standing throws (fail closed instead of fail open)',
    intended: ['S19'],
    edits: Object.fromEntries(ARMS.map((a) => [a, [strictStanding(a === 'CR3' ? 'JsonPath' : 'FilePath')]])),
  },
  {
    id: 'M5-ok-does-not-lift', decision: 'D4/D5',
    breaks: 'after a round, a key whose launch ended Ok is no longer lifted (every lift-on-Ok of the round decision is disabled; Lift() itself is untouched)',
    intended: ['S25'],
    edits: {
      C1: [e(ROUND_FILE.C1, 'pairs.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && Standing(key, now)', `pairs.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && ${NEVER} && Standing(key, now)`)],
      C2: [e(ROUND_FILE.C2, 'var answered = results.Any(r => r.Outcome is ReviewerOutcome.Ok);', `var answered = results.Any(r => r.Outcome is ReviewerOutcome.Ok) && ${NEVER};`)],
      C3: [e(ROUND_FILE.C3, 'return ended.Any(item => item.Outcome is ReviewerOutcome.Ok) ? new Lifted(key) : null;', `return ended.Any(item => item.Outcome is ReviewerOutcome.Ok) && ${NEVER} ? new Lifted(key) : null;`)],
      CR1: [e(ROUND_FILE.CR1, 'var answered = pairs.Any(p => p.Outcome is ReviewerOutcome.Ok);', `var answered = pairs.Any(p => p.Outcome is ReviewerOutcome.Ok) && ${NEVER};`)],
      CR2: [e(ROUND_FILE.CR2, 'group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && Standing(key, now)', `group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && ${NEVER} && Standing(key, now)`)],
      CR3: [e(ROUND_FILE.CR3, '(group.Any(result => result.Outcome is ReviewerOutcome.Ok) ? new LiftOp(key) : null)', `(group.Any(result => result.Outcome is ReviewerOutcome.Ok) && ${NEVER} ? new LiftOp(key) : null)`)],
      SRS1: [e(ROUND_FILE.SRS1, 'rows.Any(Answered) ? new KeyPlan(key, true, null) : null;', `rows.Any(Answered) && ${NEVER} ? new KeyPlan(key, true, null) : null;`)],
      SRS2: [e(ROUND_FILE.SRS2, 'if (rows.Any(row => row.Outcome is ReviewerOutcome.Ok))', `if (rows.Any(row => row.Outcome is ReviewerOutcome.Ok) && ${NEVER})`)],
      SRS3: [e(ROUND_FILE.SRS3, 'group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && store.Standing(key, now)', `group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && ${NEVER} && store.Standing(key, now)`)],
    },
  },
  {
    id: 'M6-ok-beats-spent', decision: 'D5',
    breaks: 'per key, a sibling Ok wins over a qualifying spent refusal: a key with any Ok outcome is never parked (it is lifted instead)',
    intended: ['S41'],
    edits: {
      C1: [e(ROUND_FILE.C1, 'if (limited.Count > 0 && await ParkAsync(', 'if (limited.Count > 0 && !pairs.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && await ParkAsync(')],
      C2: [e(ROUND_FILE.C2, 'if (refusal is null)', 'if (refusal is null || answered)')],
      C3: [e(ROUND_FILE.C3, 'if (refusal is not null && await ResetOfAsync(', 'if (refusal is not null && !ended.Any(item => item.Outcome is ReviewerOutcome.Ok) && await ResetOfAsync(')],
      CR1: [e(ROUND_FILE.CR1, 'return refusals.Count == 0 ? Quiet(key, answered)', 'return refusals.Count == 0 || answered ? Quiet(key, answered)')],
      CR2: [e(ROUND_FILE.CR2, 'var parked = spent.Count > 0 ? await ParkedAsync(', 'var parked = spent.Count > 0 && !group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) ? await ParkedAsync(')],
      CR3: [e(ROUND_FILE.CR3, 'var parked = Refusal(group) is { } refused ? await', 'var parked = !group.Any(result => result.Outcome is ReviewerOutcome.Ok) && Refusal(group) is { } refused ? await')],
      SRS1: [e(ROUND_FILE.SRS1, 'if (Refusal(rows) is not { } row)', 'if (rows.Any(Answered) || Refusal(rows) is not { } row)')],
      SRS2: [e(ROUND_FILE.SRS2, 'if (asking.Count == 0)', 'if (asking.Count == 0 || rows.Any(row => row.Outcome is ReviewerOutcome.Ok))')],
      SRS3: [e(ROUND_FILE.SRS3, 'if (RefusalOf(group) is { } refusal)', 'if (!group.Any(pair => pair.Outcome is ReviewerOutcome.Ok) && RefusalOf(group) is { } refusal)')],
    },
  },
  {
    id: 'M7-earlier-reset-wins', decision: 'D6',
    breaks: '/quota: when both active buckets of a group are spent, the EARLIER reset is taken instead of the later (a single bucket is unaffected)',
    intended: ['S12'],
    edits: {
      C1: [e(`${R}/AgyQuotaReport.cs`, 'known.Max(bucket => bucket.ResetTime!.Value)', 'known.Min(bucket => bucket.ResetTime!.Value)')],
      C2: [e(`${R}/AgyQuotaReport.cs`, '.OrderByDescending(b => b.ResetTime).ToArray();', '.OrderBy(b => b.ResetTime).ToArray();')],
      C3: [e(`${R}/AgyQuotaReport.cs`, 'known.Max(bucket => bucket.ResetTime!.Value)', 'known.Min(bucket => bucket.ResetTime!.Value)')],
      CR1: [e(`${R}/AgyQuotaReport.cs`, 'known.Max(b => b.ResetTime!.Value)', 'known.Min(b => b.ResetTime!.Value)')],
      CR2: [e(`${R}/AgyQuotaReport.cs`, 'known.Max(bucket => bucket.ResetTime!.Value)', 'known.Min(bucket => bucket.ResetTime!.Value)')],
      CR3: [e(`${R}/AgyQuotaReport.cs`, 'known.Max(bucket => bucket.ResetTime!.Value)', 'known.Min(bucket => bucket.ResetTime!.Value)')],
      SRS1: [e(`${R}/AgyQuotaReport.cs`, 'known.Select(bucket => bucket.ResetTime!.Value).Max();', 'known.Select(bucket => bucket.ResetTime!.Value).Min();')],
      SRS2: [e(`${R}/AgyQuotaReport.cs`, 'usable.Max(bucket => bucket.ResetTime!.Value)', 'usable.Min(bucket => bucket.ResetTime!.Value)')],
      SRS3: [e(`${R}/AgyQuotaReport.cs`, 'w > f ? w : f', 'w < f ? w : f')],
    },
  },
  {
    id: 'M8-cooling-row-still-runs', decision: 'D9',
    breaks: "PanelService.CanRun's cooldown term is always true: a parked row is dealt work and is not excluded",
    intended: ['S26'],
    edits: {
      C1: [e(PANEL, 'RowRefusal(provider).Length == 0 && CooldownEntryOf(provider) is null;', `RowRefusal(provider).Length == 0 && (CooldownEntryOf(provider) is null || ${ALWAYS});`)],
      C2: [e(PANEL, 'RowRefusal(provider).Length == 0 && Cooling(provider) is null;', `RowRefusal(provider).Length == 0 && (Cooling(provider) is null || ${ALWAYS});`)],
      C3: [e(PANEL, 'RowRefusal(provider).Length == 0 && Standing(provider) is null;', `RowRefusal(provider).Length == 0 && (Standing(provider) is null || ${ALWAYS});`)],
      CR1: [e(PANEL, 'RowRefusal(provider).Length == 0 && !IsCooling(provider);', `RowRefusal(provider).Length == 0 && (!IsCooling(provider) || ${ALWAYS});`)],
      CR2: [e(PANEL, 'RowRefusal(provider).Length == 0 && Cooling(provider) is null;', `RowRefusal(provider).Length == 0 && (Cooling(provider) is null || ${ALWAYS});`)],
      CR3: [e(PANEL, 'RowRefusal(provider).Length == 0 && CoolingOf(provider) is null;', `RowRefusal(provider).Length == 0 && (CoolingOf(provider) is null || ${ALWAYS});`)],
      SRS1: [e(PANEL, 'RowRefusal(provider).Length == 0 && !IsCooling(provider);', `RowRefusal(provider).Length == 0 && (!IsCooling(provider) || ${ALWAYS});`)],
      SRS2: [e(PANEL, 'RowRefusal(provider).Length == 0 && !IsCooling(provider);', `RowRefusal(provider).Length == 0 && (!IsCooling(provider) || ${ALWAYS});`)],
      SRS3: [e(PANEL, 'RowRefusal(provider).Length == 0 && !IsCooling(provider);', `RowRefusal(provider).Length == 0 && (!IsCooling(provider) || ${ALWAYS});`)],
    },
  },
];

// ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
const git = (dir, ...a) => execFileSync('git', ['-C', dir, ...a], { encoding: 'utf8', maxBuffer: 1 << 28 });
const armDir = (arm) => `${E}/r/${MAP[arm]}/arm`;
const count = (s, sub) => s.split(sub).length - 1;
const now = () => new Date().toISOString();

/** Applies one mutant's edits to a reader/writer pair; returns { why } when not applicable, else { applied: [...] }. */
function applyEdits(edits, read, write) {
  const contents = new Map();
  const applied = [];
  for (const ed of edits) {
    const text = contents.get(ed.file) ?? read(ed.file);
    if (text === null) return { why: `file missing: ${ed.file}` };
    let next;
    if (ed.regex) {
      const matches = [...text.matchAll(new RegExp(ed.regex.source, 'g'))];
      if (matches.length !== 1) return { why: `anchor ${matches.length === 0 ? 'missing' : `ambiguous (${matches.length})`} in ${ed.file}: ${ed.regex}` };
      const m = matches[0];
      const replacement = ed.replaceWith(...m);
      next = text.slice(0, m.index) + replacement + text.slice(m.index + m[0].length);
      applied.push({ file: ed.file, find: m[0], replace: replacement });
    } else {
      const n = count(text, ed.find);
      if (n !== 1) return { why: `anchor ${n === 0 ? 'missing' : `ambiguous (${n})`} in ${ed.file}: ${ed.find}` };
      next = text.replace(ed.find, () => ed.replace);
      applied.push({ file: ed.file, find: ed.find, replace: ed.replace });
    }
    contents.set(ed.file, next);
  }
  for (const [f, t] of contents) write(f, t);
  return { applied };
}

/** The assembly an edited source file compiles into: the nearest .csproj up the tree, its AssemblyName or its file name. */
function assemblyOf(dir, file) {
  let d = dirname(join(dir, file));
  while (d.startsWith(dir) && d !== dir) {
    const proj = readdirSync(d).find((f) => f.endsWith('.csproj'));
    if (proj) {
      const name = readFileSync(join(d, proj), 'utf8').match(/<AssemblyName>([^<]+)<\/AssemblyName>/)?.[1] ?? proj.replace(/\.csproj$/, '');
      return `${name}.dll`;
    }
    d = dirname(d);
  }
  return null;
}

// ── check: every anchor against every arm's HEAD, read-only ─────────────────────────────────────────────────────────────
function check() {
  const rows = [];
  for (const arm of ARMS) {
    const A = armDir(arm);
    const head = git(A, 'rev-parse', '--short', 'HEAD').trim();
    for (const m of MUTANTS) {
      const r = applyEdits(m.edits[arm] ?? [], (f) => { try { return git(A, 'show', `HEAD:${f}`); } catch { return null; } }, () => {});
      rows.push(`${arm} ${head} ${m.id.padEnd(30)} ${r.why ? 'NOT APPLICABLE — ' + r.why : 'applies (' + r.applied.length + ' edit' + (r.applied.length > 1 ? 's' : '') + ')'}`);
    }
  }
  console.log(rows.join('\n'));
}

// ── one arm ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
function runArm(arm) {
  const A = armDir(arm);
  const OUT = `${E}/mut6/${arm}`;
  const WT = `${OUT}/testsToDelete-mut6-${arm}`;
  const RES = `${OUT}/results`;
  const LOGS = `${OUT}/logs`;
  mkdirSync(RES, { recursive: true });
  mkdirSync(LOGS, { recursive: true });
  const say = (s) => console.log(`[${now()}] ${arm}: ${s}`);
  const head = git(A, 'rev-parse', 'HEAD').trim();
  const report = { arm, token: MAP[arm], head, base: BASE, frozen: FROZEN, harness: `${E}/mutate6.mjs`, started_utc: now(), complete: false, mutants: [] };
  const save = () => writeFileSync(`${OUT}/mutation.json`, JSON.stringify(report, null, 2));

  // the clone (the arm's clone is only read)
  rmSync(WT, { recursive: true, force: true });
  execFileSync('git', ['clone', '-q', '--shared', '--no-checkout', A, WT]);
  git(WT, 'checkout', '-q', '--detach', head);
  const subName = git(WT, 'config', '-f', '.gitmodules', '--get-regexp', '^submodule\\..*\\.path$').split('\n')
    .map((l) => l.split(' ')).find(([, p]) => p === '.agents/conventions')?.[0].replace(/^submodule\./, '').replace(/\.path$/, '');
  git(WT, 'submodule', 'init', '-q', '.agents/conventions');
  git(WT, 'config', `submodule.${subName}.url`, `${A}/.agents/conventions`);
  execFileSync('git', ['-c', 'protocol.file.allow=always', 'submodule', 'update', '-q', '.agents/conventions'], { cwd: WT });
  const LP = `${WT}/.agents/conventions/tools/low-priority.mjs`;
  if (!existsSync(LP)) throw new Error(`no low-priority.mjs in the clone's conventions (${LP})`);

  // the scope: one rule, each arm's own tree
  const testFiles = git(WT, 'ls-files', 'src_mcp/tests').split('\n').filter((f) => f.endsWith('.cs'));
  const changed = new Set(git(WT, 'diff', '--name-only', '--diff-filter=AM', BASE, 'HEAD', '--', 'src_mcp/tests').split('\n').filter((f) => f.endsWith('.cs')));
  const csScenarios = Object.values(SCENARIOS).filter((s) => s.kind === 'cs').map((s) => s.name);
  const classes = new Set();
  for (const f of testFiles) {
    const text = readFileSync(join(WT, f), 'utf8');
    if (!/\[(Fact|Theory)\b/.test(text)) continue;
    const holdsScenario = csScenarios.some((n) => new RegExp(`\\b${n}\\w*\\s*\\(`).test(text));
    if (!(changed.has(f) || AREA.test(basename(f)) || holdsScenario)) continue;
    for (const m of text.matchAll(/^ {0,4}(?:public |internal )?(?:sealed |static |abstract |partial )*class (\w+)/gm)) classes.add(m[1]);
  }
  report.classes = [...classes].sort();
  report.testThreads = 4;

  const lp = (args, opts = {}) => spawnSync('node', [LP, ...args], { cwd: WT, encoding: 'utf8', maxBuffer: 1 << 29, ...opts });
  const build = (tag) => {
    const t0 = Date.now();
    const r = lp(['dotnet', 'build', 'src_mcp/tests/CoaiMcp.Tests.csproj', '-c', 'Debug', '-m:4', '--disable-build-servers'], { timeout: 40 * 60_000 });
    writeFileSync(`${LOGS}/${tag}.build.log`, (r.stdout ?? '') + (r.stderr ?? ''));
    const errors = [...new Set(((r.stdout ?? '') + (r.stderr ?? '')).split('\n').filter((l) => /\berror\b/.test(l)).map((l) => l.trim()))].slice(0, 8);
    return { ok: r.status === 0, seconds: Math.round((Date.now() - t0) / 1000), errors };
  };
  const run = (tag, filterArgs) => {
    const t0 = Date.now();
    const r = lp([TEST_EXE, ...filterArgs, '--max-threads', '4', '--timeout', '25m', '--results-directory', RES, '--report-xunit-ctrf', '--report-xunit-ctrf-filename', `${tag}.json`], { timeout: 30 * 60_000 });
    writeFileSync(`${LOGS}/${tag}.test.log`, (r.stdout ?? '') + (r.stderr ?? ''));
    const f = `${RES}/${tag}.json`;
    const tests = existsSync(f) ? JSON.parse(readFileSync(f, 'utf8')).results.tests.map((t) => ({ name: t.name.replace(/^CoaiMcp\.Tests\./, ''), status: t.status })) : null;
    return { tests, exit: r.status, seconds: Math.round((Date.now() - t0) / 1000) };
  };
  const classArgs = report.classes.flatMap((c) => ['--filter-class', `CoaiMcp.Tests.${c}`]);
  const methodOf = (name) => name.replace(/\(.*$/s, '');

  // baseline
  say(`clone at ${head.slice(0, 8)}, ${report.classes.length} classes in scope; building the baseline`);
  const b0 = build('baseline');
  report.baseline = { build: b0 };
  if (!b0.ok) { report.error = 'the unmutated arm does not build'; save(); say(report.error); return report; }
  const base = run('baseline', classArgs);
  if (!base.tests) { report.error = 'the baseline run produced no results'; save(); say(report.error); return report; }
  const passing = new Set(base.tests.filter((t) => t.status === 'passed').map((t) => t.name));
  report.baseline = { build: b0, tests: base.tests.length, passed: passing.size, skipped: base.tests.filter((t) => t.status === 'skipped').length,
    failed: base.tests.filter((t) => t.status === 'failed').map((t) => t.name), seconds: base.seconds };
  save();
  say(`baseline ${passing.size}/${base.tests.length} passed in ${base.seconds}s (build ${b0.seconds}s)`);

  for (const m of MUTANTS) {
    const t0 = Date.now();
    git(WT, 'checkout', '-q', '--', '.');
    const entry = { id: m.id, decision: m.decision, breaks: m.breaks, intendedScenarios: m.intended };
    const appliedAt = Date.now();
    const r = applyEdits(m.edits[arm] ?? [], (f) => (existsSync(join(WT, f)) ? readFileSync(join(WT, f), 'utf8') : null), (f, t) => writeFileSync(join(WT, f), t));
    if (r.why) { Object.assign(entry, { applicable: false, verdict: 'NOT APPLICABLE', why: r.why }); report.mutants.push(entry); save(); say(`${m.id} NOT APPLICABLE — ${r.why}`); continue; }
    entry.edits = r.applied;
    const bm = build(m.id);
    entry.build = bm;
    if (!bm.ok) { Object.assign(entry, { applicable: false, verdict: 'NOT APPLICABLE', why: 'the mutant does not compile' }); report.mutants.push(entry); save(); say(`${m.id} NOT APPLICABLE — does not compile`); continue; }
    const stale = [...new Set(r.applied.map((x) => assemblyOf(WT, x.file)))].filter((dll) => !dll || !existsSync(join(WT, TEST_BIN, dll)) || statSync(join(WT, TEST_BIN, dll)).mtimeMs < appliedAt);
    entry.assemblies = [...new Set(r.applied.map((x) => assemblyOf(WT, x.file)))];
    if (stale.length) { Object.assign(entry, { applicable: false, verdict: 'NOT APPLICABLE', why: `stale binary after the build: ${stale.join(', ')}` }); report.mutants.push(entry); save(); say(`${m.id} NOT APPLICABLE — stale ${stale}`); continue; }
    const res = run(m.id, classArgs);
    if (!res.tests) { Object.assign(entry, { applicable: true, verdict: 'RUN FAILED', why: `the test run produced no results (exit ${res.exit})` }); report.mutants.push(entry); save(); say(`${m.id} RUN FAILED`); continue; }
    const failedNow = res.tests.filter((t) => t.status === 'failed' && passing.has(t.name)).map((t) => t.name);
    let confirmed = failedNow;
    let flaky = [];
    if (failedNow.length) {
      const again = run(`${m.id}-confirm`, [...new Set(failedNow.map(methodOf))].flatMap((n) => ['--filter-method', `CoaiMcp.Tests.${n}`]));
      if (again.tests) {
        const failedAgain = new Set(again.tests.filter((t) => t.status === 'failed').map((t) => t.name));
        confirmed = failedNow.filter((n) => failedAgain.has(n));
        flaky = failedNow.filter((n) => !failedAgain.has(n));
      } else entry.confirmNote = 'the confirming re-run produced no results; the first run\'s failures are kept';
    }
    const intended = m.intended.map((id) => {
      const name = SCENARIOS[id].name;
      const cases = res.tests.filter((t) => new RegExp(`\\.${name}(\\w*)(\\(|$)`).test(t.name));
      return { scenario: id, test: name, present: cases.length > 0, matched: [...new Set(cases.map((t) => methodOf(t.name)))],
        caught: cases.some((t) => confirmed.includes(t.name)) };
    });
    Object.assign(entry, { applicable: true, verdict: confirmed.length ? 'KILLED' : 'SURVIVED', killedBy: confirmed, flaky, intended,
      run: { tests: res.tests.length, seconds: res.seconds }, seconds: Math.round((Date.now() - t0) / 1000) });
    report.mutants.push(entry);
    save();
    say(`${m.id} ${entry.verdict} (${confirmed.length} failing${flaky.length ? `, ${flaky.length} flaky` : ''}; intended ${intended.map((x) => `${x.scenario}${x.present ? (x.caught ? '+' : '-') : '?'}`).join(' ')})`);
  }
  git(WT, 'checkout', '-q', '--', '.');
  report.ended_utc = now();
  report.complete = true;
  report.summary = { killed: report.mutants.filter((x) => x.verdict === 'KILLED').length, applicable: report.mutants.filter((x) => x.applicable).length,
    notApplicable: report.mutants.filter((x) => !x.applicable).map((x) => x.id) };
  save();
  rmSync(WT, { recursive: true, force: true });
  say(`done: ${report.summary.killed}/${report.summary.applicable} applicable mutants killed`);
  return report;
}

// ── all arms, at most two at a time ─────────────────────────────────────────────────────────────────────────────────────
async function runAll() {
  const complete = (arm) => { try { return JSON.parse(readFileSync(`${E}/mut6/${arm}/mutation.json`, 'utf8')).complete === true; } catch { return false; } };
  const queue = ARMS.filter((a) => { if (complete(a)) { console.log(`[${now()}] ${a}: complete mutation.json, skipped`); return false; } return true; });
  const t0 = Date.now();
  const one = (arm) => new Promise((resolve) => {
    mkdirSync(`${E}/mut6/${arm}`, { recursive: true });
    const log = `${E}/mut6/${arm}/run.log`;
    appendFileSync(log, `\n=== ${now()} start\n`);
    const child = spawn('node', [process.argv[1], arm], { stdio: ['ignore', 'pipe', 'pipe'] });
    const pass = (d) => { appendFileSync(log, d); process.stdout.write(d); };
    child.stdout.on('data', pass);
    child.stderr.on('data', pass);
    child.on('close', (code) => { console.log(`[${now()}] ${arm}: exit ${code}`); resolve(); });
  });
  const workers = [0, 1].map(async () => { while (queue.length) await one(queue.shift()); });
  await Promise.all(workers);
  console.log(`[${now()}] all arms done in ${Math.round((Date.now() - t0) / 60000)} min`);
}

const what = process.argv[2];
if (what === 'check') check();
else if (what === 'all') await runAll();
else if (ARMS.includes(what)) runArm(what);
else { console.error('usage: node mutate6.mjs check | all | <ARM>'); process.exit(2); }
