#!/usr/bin/env bash
# The objective quality gate of one arm, on its final commit. Runs lowered; run the arms one after another.
# Writes results/<arm>/objective.json.   Usage: objective-checks.sh V|C|C2
set -u
ARM=$1; E=/home/jinx/git/coai-exp; D=$E/arm-$ARM; O=$E/results/$ARM/objective; mkdir -p "$O"
LP="node $D/.agents/conventions/tools/low-priority.mjs"
BASELINE_MCP='KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn|KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed'
BASELINE_TS='the baseline names no release that was never tagged'
cd "$D"
uncommitted=$(git status --porcelain | wc -l)
if [ "${PARSE_ONLY:-}" = 1 ]; then build=0 mcp=0 format=0 generator=0 ts=0  # test the parser on logs already in $O
else
$LP dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers > "$O/build.log" 2>&1; build=$?
$LP ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --results-directory "$O" --report-xunit-ctrf \
  --report-xunit-ctrf-filename mcp.ctrf.json > "$O/mcp-tests.log" 2>&1; mcp=$?
changed_cs=$(git diff --name-only --diff-filter=AM b4e2ea96..HEAD -- '*.cs')
if [ -z "$changed_cs" ]; then format=0; else
  dotnet format dew_flow_connect_other_ais.slnx whitespace --verify-no-changes --no-restore --include $changed_cs > "$O/format.log" 2>&1 \
    && dotnet format dew_flow_connect_other_ais.slnx style --verify-no-changes --no-restore --include $changed_cs >> "$O/format.log" 2>&1; format=$?
fi  # the whole solution fails on IL2026/IL3050 analyzer errors in src_bugs, which no arm touches
(cd src_vs_code && node scripts/generate-runtime-capabilities.mjs --check) > "$O/generator.log" 2>&1; generator=$?
(cd src_vs_code && $LP npm test) > "$O/ts-tests.log" 2>&1; ts=$?
fi
node - "$O" "$BASELINE_MCP" "$BASELINE_TS" "$ARM" "$uncommitted" "$build" "$mcp" "$format" "$generator" "$ts" <<'EOF'
const fs = require('fs'); const { execSync } = require('child_process');
const [O, baseMcp, baseTs, arm, uncommitted, build, mcp, format, generator, ts] = process.argv.slice(2);
const read = (f) => fs.existsSync(`${O}/${f}`) ? fs.readFileSync(`${O}/${f}`, 'utf8') : '';
const mcpLog = read('mcp-tests.log'); const tsLog = read('ts-tests.log');
const num = (re, s) => Number((s.match(re) ?? [])[1] ?? NaN);
// flaky on this host: it also fails at the start commit when re-run (checked 2026-10-25), so it is never an arm's failure
const FLAKY = ['TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow'];
const mcpFailed = [...mcpLog.matchAll(/^failed (\S+)/gm)].map((m) => m[1].replace(/^CoaiMcp\.Tests\./, ''));
const tsFailed = [...new Set([...tsLog.matchAll(/^✖ (.+?) \(\d/gm)].map((m) => m[1]))];
const scenarios = ['AKimiCheck_RunsInACopy_OnAOneShotHome', 'AfterTheCheck_TheHomeIsGone_JunctionFirst',
  'ACheckCancelledMidTurn_StillDisposesItsHome', 'ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst',
  'KimiHasThreeRows_EachCitingItsCells', 'TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable', 'ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb',
  'AdmissionFlagsBothRows', 'AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome',
  'APlanWithFlags_IsRefusedForKimi', 'AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind'];
const tree = (p) => { try { return require('child_process').execFileSync('git', ['grep', '-l', '-F', p, '--', 'src_mcp/tests', 'src_vs_code/src/test'], { encoding: 'utf8' }).trim(); } catch { return ''; } };
const present = Object.fromEntries(scenarios.map((s) => [s, !!tree(s)]));
present['S5.6.8 (TS: kimi rows admit flagged)'] = !!tree("admit('kimi'") || !!tree('admit("kimi"');
// per scenario from the CTRF report: 'passed' when every case of that test passed, else its statuses
const ctrf = fs.existsSync(`${O}/mcp.ctrf.json`) ? JSON.parse(fs.readFileSync(`${O}/mcp.ctrf.json`, 'utf8')).results?.tests ?? [] : [];
const scenarioPasses = Object.fromEntries(scenarios.map((s) => {
  const cases = ctrf.filter((t) => new RegExp(`\\b${s}\\b`).test(t.name));
  if (cases.length === 0) return [s, 'absent'];
  const st = [...new Set(cases.map((t) => t.status))];
  return [s, st.length === 1 && st[0] === 'passed' ? 'passed' : st.join('+')];
}));
const stat = execSync('git diff --shortstat b4e2ea96..HEAD', { encoding: 'utf8' }).trim();
const files = execSync('git diff --numstat b4e2ea96..HEAD', { encoding: 'utf8' }).trim().split('\n').filter(Boolean)
  .map((l) => { const [a, d, f] = l.split('\t'); return { f, a: +a || 0, d: +d || 0 }; });
const sum = (pred) => files.filter((x) => pred(x.f)).reduce((n, x) => n + x.a + x.d, 0);
// a Q arm's skeleton must leave nothing behind: no marker, no placeholder body
const leftovers = (p) => { try { return require('child_process').execFileSync('git', ['grep', '-n', '-E', p, '--', 'src_mcp', 'src_vs_code/src', 'shared'], { encoding: 'utf8' }).trim().split('\n').filter(Boolean); } catch { return []; } };
const markersLeft = leftovers('QWEN-FILL-(BEGIN|END)');
// only lines this change ADDED: the start commit has its own throw new Error('boom') lines
const placeholdersLeft = execSync('git diff -U0 b4e2ea96..HEAD -- src_mcp src_vs_code/src shared', { encoding: 'utf8', maxBuffer: 1 << 28 }).split('\n')
  .filter((l) => l.startsWith('+') && /NotImplementedException\("[a-z0-9-]+"\)|throw new Error\('[a-z0-9-]+'\)/.test(l));
const out = {
  markersLeft, placeholdersLeft,
  arm, head: execSync('git rev-parse --short HEAD', { encoding: 'utf8' }).trim(),
  commits: +execSync('git rev-list --count b4e2ea96..HEAD', { encoding: 'utf8' }).trim(), uncommittedFiles: +uncommitted,
  build: +build === 0, format: +format === 0, generatorCheck: +generator === 0,
  mcp: { exit: +mcp, total: num(/total: (\d+)/, mcpLog), passed: num(/succeeded: (\d+)/, mcpLog), skipped: num(/skipped: (\d+)/, mcpLog),
    failedNew: mcpFailed.filter((f) => !new RegExp(baseMcp).test(f) && !FLAKY.includes(f)), failedBaseline: mcpFailed.filter((f) => new RegExp(baseMcp).test(f)),
    failedEnvironment: mcpFailed.filter((f) => FLAKY.includes(f)) },
  ts: { exit: +ts, total: num(/^ℹ tests (\d+)/m, tsLog), failedNew: tsFailed.filter((f) => f !== baseTs) },
  scenariosPresent: present, scenariosPresentCount: Object.values(present).filter(Boolean).length,
  scenariosPassing: scenarioPasses,
  diff: { shortstat: stat, changedLines: { tests: sum((f) => /tests?\//.test(f)), src: sum((f) => /^src_/.test(f) && !/tests?\//.test(f)),
    shared: sum((f) => f.startsWith('shared/')), docs: sum((f) => f.endsWith('.md')) }, files: files.map((x) => x.f) },
};
fs.writeFileSync(`${O}/../objective.json`, JSON.stringify(out, null, 2));
console.log(JSON.stringify({ arm, build: out.build, format: out.format, generator: out.generatorCheck,
  mcpNewFailures: out.mcp.failedNew.length, tsNewFailures: out.ts.failedNew.length, scenarios: `${out.scenariosPresentCount}/12`, stat }));
EOF
