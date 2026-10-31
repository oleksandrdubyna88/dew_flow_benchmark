// Session-level tables of the results record, built only from research/minigit/data/<series>.json.
import { DASH, checks, flagList, h12, int, pct, sec, short, stackName, table, usd } from './format.mjs';

const paid = s => s.kind !== 'rehearsal';
const ord = s => `${s.kind}${s.pair ? ` p${s.pair}` : ''}`;
const idCols = (d, s) => [short(d.series.seriesId), s.rid, ord(s), stackName(s.stack), s.phase, `a${s.attempt}`];
const ID_HEAD = ['series', 'rid', 'kind', 'stack', 'phase', 'att.'];
const rowsOf = (all, f) => all.flatMap(d => d.sessions.filter(paid).map(s => f(d, s)));

/** How a series without a recorded stop stands, read from its sessions and pairs (the run-map has no "abandoned" state). */
function runState(d) {
  if (!d.sessions.some(s => s.kind === 'data')) return 'no stop; no data session ran';
  const done = d.pairs.filter(p => !p.cells.csharp.missing && !p.cells.node.missing && p.cells.csharp.incomplete.length === 0 && p.cells.node.incomplete.length === 0);
  return done.length === d.pairs.length ? `no stop; pairs ${done.map(p => p.pair).join(', ')} complete` : `no stop; complete pairs: ${done.map(p => p.pair).join(', ') || 'none'} of ${d.pairs.length}`;
}

export function conditions(all) {
  return table(
    ['series', 'harness at init', 'harness accepted for data', 'prereg tag commit', 'claude', 'node', '.NET SDK', 'TS', 'cap / wall / stall', 'bare budget / ceiling', 'spin median (ms)', 'stopped'],
    all.map(d => {
      const s = d.series;
      const acc = s.harnessAccepted.length ? s.harnessAccepted.map(h12).join(', ') : DASH;
      const stop = s.stopped ? `${s.stopped.rule} (${s.stopped.at})` : runState(d);
      return [s.seriesId, h12(s.harnessIdentityAtInit), acc, h12(s.preregCommit), s.pins.claude, s.pins.node, s.pins.dotnetSdk, s.pins.typescript,
        `${usd(s.caps.capUsd, 0)} / ${s.caps.wallMs / 60000} min / ${s.caps.stallMs / 60000} min`, `${usd(s.bareBudgetUsd, 0)} / ${usd(s.ceilingUsd, 0)}`, s.spin.medianMs.toFixed(2), stop];
    }),
  );
}

export function sessionsCost(all) {
  return table(
    [...ID_HEAD, 'label', 'class', 'flags', 'headline (primary)', 'E', 'B', 'O', 'T1–T5', 'charged'],
    rowsOf(all, (d, s) => [...idCols(d, s), s.label, s.class, flagList(s), usd(s.cost.headlineUsd), usd(s.cost.eUsd), usd(s.cost.bUsd), usd(s.cost.oUsd), s.recorded ? checks(s.cost.checks) : 'no record', usd(s.runMapUsd)]),
  );
}

const tokenAgree = s => {
  if (!s.broker || !s.recorded) return DASH;
  const [m, t] = Object.entries(s.tokens)[0] ?? [];
  const b = s.broker.tokens;
  if (!m) return DASH;
  return t.input === b.input && t.output === b.output && t.cacheRead === b.cacheRead && t.cacheWrite === b.cacheWrite && t.cacheWrite1h === b.cacheWrite1h ? 'yes' : 'NO';
};

export function sessionsTokens(all) {
  return table(
    [...ID_HEAD, 'model', 'input', 'output', 'cache read', 'cache write 5m', 'cache write 1h', 'first-turn cache read', 'B tokens equal'],
    all.flatMap(d => d.sessions.filter(paid).flatMap(s => Object.entries(s.tokens).map(([m, t]) => [...idCols(d, s), m + (t.source ? ` (${t.source})` : ''),
      int(t.input), int(t.output), int(t.cacheRead), int(t.cacheWrite5m), int(t.cacheWrite1h), int(s.cost.firstTurnCacheRead), tokenAgree(s)]))),
  );
}

const share = (part, whole) => (part !== null && part !== undefined && whole ? pct((100 * part) / whole) : DASH);

export function sessionsTime(all) {
  return table(
    [...ID_HEAD, 'harness', 'wall s', 'model_api s', 'active − model_api s', 'build_test s', 'other_tool s', 'hooks s', 'unattributed s', 'unattributed / wall', 'wall − Σ partition s', 'turns', 'subagents'],
    rowsOf(all, (d, s) => [...idCols(d, s), h12(s.host?.harnessIdentity), sec(s.time.wallMs), sec(s.time.modelApiMs), sec(s.time.activeMinusModelApiMs),
      sec(s.time.buildTestMs), sec(s.time.otherToolMs), sec(s.time.hooksMs), sec(s.time.unattributedMs), share(s.time.unattributedMs, s.time.wallMs), s.time.partitionSumMs === null || s.time.partitionSumMs === undefined ? '—' : sec(s.time.wallMs - s.time.partitionSumMs), int(s.time.turns), int(s.time.subagentsLaunched)]),
  );
}

const visible = q => (q.visible?.length ? q.visible.map(v => `${v.script.replace('.sh', '')} ${v.passed}/${v.expectedTotal}`).join(', ') : DASH);
const held = q => (q.heldout ? `${pct(q.heldout.percent)} of ${q.heldout.total}` : DASH);
const egress = h => {
  if (!h?.egress) return DASH;
  const a = Object.entries(h.egress.allowed).map(([k, v]) => `${k} ${v}`).join(', ') || 'none';
  const r = Object.values(h.egress.refused).reduce((x, y) => x + y, 0);
  const mb = Object.values(h.egress.bytes ?? {}).reduce((x, b) => x + (b.down ?? 0), 0) / 1e6;
  return `allowed: ${a}; refused: ${r}; ${mb.toFixed(1)} MB down`;
};
const pk = h => (h?.packages ? `${h.packages.total} seen, ${h.packages.added.length} added${h.packages.added.length ? ` (${h.packages.added.map(p => `${p.name} ${p.version}`).join(', ')})` : ''}, ${h.packages.flagged.length} flagged` : DASH);

export function sessionsQuality(all) {
  return table(
    [...ID_HEAD, 'LOC product / test / files', 'build', 'visible', 'held-out percent', 'defects', 'blocked', 'latency median / box baseline ms', 'egress', 'packages', 'real token seen'],
    rowsOf(all, (d, s) => {
      const q = s.quality;
      const loc = s.host?.loc ? `${s.host.loc.product} / ${s.host.loc.test} / ${s.host.loc.files}` : DASH;
      const lat = q.latency ? `${q.latency.medianMs} / ${q.latency.boxBaselineMedianMs}` : DASH;
      const build = q.scored ? (q.buildOk ? 'ok' : 'FAIL') : `not scored`;
      return [...idCols(d, s), loc, build, visible(q), held(q), int(q.heldout?.defects), int(q.heldout?.blocked), lat, egress(s.host), pk(s.host), int(s.meta?.realTokenSeen)];
    }),
  );
}

export function rehearsals(all) {
  return table(
    ['series', 'rid', 'stack', 'class', 'stub model', 'ledger charge', 'G-14 checks'],
    all.flatMap(d => d.sessions.filter(s => s.kind === 'rehearsal').map(s => {
      const r = d.rehearsalV2?.stacks?.[s.stack];
      const ok = r ? r.checks.map(c => `${c.id} ${c.ok ? 'ok' : 'FAIL'}`).join(', ') : DASH;
      const ledger = d.ledger.lines.find(l => l.rid === s.rid && !l.fromSeries);
      return [short(d.series.seriesId), s.rid, stackName(s.stack), s.class, Object.keys(s.tokens).join(', ') || DASH, usd(ledger?.usd), ok];
    })),
  );
}

export function voids(all) {
  return table(
    ['series', 'rid', 'kind', 'stack', 'phase', 'harness', 'high hits', 'rule', 'where', 'path the scan saw', 'charged'],
    all.flatMap(d => d.sessions.filter(s => (s.class ?? '').startsWith('void')).map(s => {
      const v = s.host?.voidDetail ?? {};
      const h = v.hits?.[0];
      return [short(d.series.seriesId), s.rid, ord(s), stackName(s.stack), s.phase, h12(s.host?.harnessIdentity), int(v.high),
        h?.rule ?? 'not persisted', h?.where ?? DASH, h?.excerpt ? `\`${h.excerpt}\`` : 'not persisted (gap fixed in 5249048)', usd(s.runMapUsd)];
    })),
  );
}

export function cpu(all) {
  return table(
    ['series', 'samples file', 'rid', 'samples', 'foreign CPU mean', 'foreign CPU max', 'samples with a foreign agent', 'flags on the session'],
    all.flatMap(d => d.cpu.flatMap(f => Object.entries(f.perSession).map(([rid, st]) => {
      const s = d.sessions.find(x => x.rid === rid);
      return [short(d.series.seriesId), f.file, rid, int(st?.samples), st ? st.meanForeignCpus.toFixed(2) : DASH, st ? st.maxForeignCpus.toFixed(2) : DASH, int(st?.foreignAgentSamples), s ? flagList(s) : DASH];
    }))),
  );
}
