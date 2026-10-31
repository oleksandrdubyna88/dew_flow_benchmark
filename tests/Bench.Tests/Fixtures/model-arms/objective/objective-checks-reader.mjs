const fs = require('fs'); const { execSync, execFileSync } = require('child_process');
const [O, E, arm, BASE, uncommitted, build, mcp, format, ts] = process.argv.slice(2);
const read = (f) => fs.existsSync(`${O}/${f}`) ? fs.readFileSync(`${O}/${f}`, 'utf8') : '';
const base = JSON.parse(fs.readFileSync(`${E}/baseline.json`, 'utf8'));   // { mcpFailed: [...], tsFailed: [...] } at BASE on this host
const scenarios = JSON.parse(fs.readFileSync(`${E}/scenarios.json`, 'utf8')); // [{ id, kind: 'cs' | 'node', cls?, name }]
const mcpLog = read('mcp-tests.log'); const tsLog = read('ts-tests.log');
const num = (re, s) => Number((s.match(re) ?? [])[1] ?? NaN);
const mcpFailed = [...mcpLog.matchAll(/^failed (\S+)/gm)].map((m) => m[1].replace(/^CoaiMcp\.Tests\./, ''));
const tsFailed = [...new Set([...tsLog.matchAll(/^✖ (.+?) \(\d/gm)].map((m) => m[1]))];
const tsPassed = new Set([...tsLog.matchAll(/^\s*✔ (.+?) \(\d/gm)].map((m) => m[1]));
const tree = (p) => { try { return execFileSync('git', ['grep', '-l', '-F', p, '--', 'src_mcp/tests', 'src_vs_code/src/test'], { encoding: 'utf8' }).trim(); } catch { return ''; } };
const ctrf = fs.existsSync(`${O}/mcp.ctrf.json`) ? JSON.parse(fs.readFileSync(`${O}/mcp.ctrf.json`, 'utf8')).results?.tests ?? [] : [];
const per = Object.fromEntries(scenarios.map((s) => {
  const present = !!tree(s.name);
  let passing;
  if (s.kind === 'node') passing = tsPassed.has(s.name) ? 'passed' : tsFailed.includes(s.name) ? 'failed' : 'absent';
  else {
    const cases = ctrf.filter((t) => new RegExp(`\\b${s.name}\\b`).test(t.name));
    const st = [...new Set(cases.map((t) => t.status))];
    passing = cases.length === 0 ? 'absent' : st.length === 1 && st[0] === 'passed' ? 'passed' : st.join('+');
  }
  return [s.id, { name: s.name, present, passing }];
}));
const git = (args) => execSync(`git ${args}`, { encoding: 'utf8', maxBuffer: 1 << 28 }).trim();
const files = git(`diff --numstat ${BASE}..HEAD`).split('\n').filter(Boolean).map((l) => { const [a, d, f] = l.split('\t'); return { f, a: +a || 0, d: +d || 0 }; });
const sum = (pred) => files.filter((x) => pred(x.f)).reduce((n, x) => n + x.a + x.d, 0);
const out = {
  arm, head: git('rev-parse --short HEAD'), commits: +git(`rev-list --count ${BASE}..HEAD`), uncommittedFiles: +uncommitted,
  build: +build === 0, format: +format === 0,
  mcp: { exit: +mcp, total: num(/total: (\d+)/, mcpLog), passed: num(/succeeded: (\d+)/, mcpLog), skipped: num(/skipped: (\d+)/, mcpLog),
    failedNew: mcpFailed.filter((f) => !base.mcpFailed.includes(f)), failedBaseline: mcpFailed.filter((f) => base.mcpFailed.includes(f)) },
  ts: { exit: +ts, total: num(/^ℹ tests (\d+)/m, tsLog), failedNew: tsFailed.filter((f) => !base.tsFailed.includes(f)) },
  scenarios: per,
  scenariosPresentCount: Object.values(per).filter((x) => x.present).length,
  scenariosPassingCount: Object.values(per).filter((x) => x.passing === 'passed').length,
  diff: { shortstat: git(`diff --shortstat ${BASE}..HEAD`), changedLines: { tests: sum((f) => /tests?\//.test(f)),
    src: sum((f) => /^src_/.test(f) && !/tests?\//.test(f)), docs: sum((f) => f.endsWith('.md')) }, files: files.map((x) => x.f) },
};
fs.writeFileSync(`${O}/../objective.json`, JSON.stringify(out, null, 2));
console.log(JSON.stringify({ arm, build: out.build, format: out.format, mcpNewFailures: out.mcp.failedNew.length,
  tsNewFailures: out.ts.failedNew.length, scenarios: `${out.scenariosPresentCount}/${scenarios.length} present, ${out.scenariosPassingCount} passing`, stat: out.diff.shortstat }));
