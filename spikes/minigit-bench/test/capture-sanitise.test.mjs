import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { buildMap, collectAttrSecrets, leakScan, localContext, sanitisePath, sanitiseText } from '../capture/sanitise.mjs';
import { hostLacks, spikeDir, tmpDir } from './helpers.mjs';
import { SKIP_CAPTURE, hasCapture } from './synth.mjs';

const REAL = path.join(spikeDir, 'test', 'fixtures', 'real');
const ctx = { out: '/cap-real', home: '/home/alice', user: 'alice', host: 'laptop-7', email: 'alice@corp.example', token: null };
const map = buildMap({ ...ctx, idValues: ['11111111-aaaa'] });

const walk = dir => (existsSync(dir) ? readdirSync(dir).flatMap(n => {
  const f = path.join(dir, n);
  return statSync(f).isDirectory() ? walk(f) : [f];
}) : []);
const filesOf = dir => walk(dir).map(f => ({ relPath: path.relative(dir, f), text: readFileSync(f, 'utf8') }));

test('Every_spelling_of_a_path_is_replaced', () => {
  const line = JSON.stringify({ a: '/cap-real/x' }) + ' \\/cap-real\\/x %2Fcap-real%2Fx %2fcap-real%2fx -cap-real-x';
  const out = sanitiseText(line, buildMap({ out: '/cap-real', home: '/home/alice', user: 'alice', host: 'laptop-7', email: null }));
  assert.equal(out, JSON.stringify({ a: '/cap/x' }) + ' \\/cap\\/x %2Fcap%2Fx %2fcap%2fx -cap-x');
});

test('Home_user_host_and_email_are_replaced_in_every_spelling', () => {
  const text = ['/home/alice/work', '\\/home\\/alice\\/work', '%2Fhome%2Falice', 'by alice on laptop-7', 'alice@corp.example', 'alice%40corp.example', '-home-alice-work'].join('\n');
  const out = sanitiseText(text, map);
  assert.equal(out, ['/home/user/work', '\\/home\\/user\\/work', '%2Fhome%2Fuser', 'by user on host', 'user@example.invalid', 'user%40example.invalid', '-home-user-work'].join('\n'));
});

test('A_user_name_inside_another_word_is_left_alone', () => {
  assert.equal(sanitiseText('malice palace alice.', map), 'malice palace user.');
});

test('A_path_component_is_sanitised_like_text', () => {
  const flattened = '-cap-real-tree/abc.jsonl';
  assert.equal(sanitisePath(flattened, map), '-cap-tree/abc.jsonl');
  assert.equal(sanitisePath('projects/-home-alice-x/s.jsonl', map), 'projects/-home-user-x/s.jsonl');
});

test('The_account_ids_and_emails_an_otlp_payload_carries_are_found_and_redacted_but_the_session_id_is_kept', () => {
  const attr = (key, v) => ({ key, value: { stringValue: v } });
  const body = JSON.stringify({ resourceLogs: [{ scopeLogs: [{ logRecords: [{ attributes: [attr('user.id', 'U-123'), attr('user.account_uuid', 'ACC-9'), attr('organization.id', 'ORG-5'), attr('user.email', 'bob@x.example'), attr('session.id', 'sess-keep')] }] }] }] });
  const sink = JSON.stringify({ receivedAt: 1, path: '/v1/logs', contentType: 'application/json', body });
  const found = collectAttrSecrets(sink);
  assert.deepEqual([...found.ids].sort(), ['ACC-9', 'ORG-5', 'U-123']);
  assert.deepEqual(found.emails, ['bob@x.example']);
  const m = buildMap({ ...ctx, email: null, idValues: found.ids, extraEmails: found.emails });
  const out = sanitiseText(sink, m);
  for (const secret of ['U-123', 'ACC-9', 'ORG-5', 'bob@x.example']) assert.equal(out.includes(secret), false, secret);
  assert.ok(out.includes('sess-keep'));
  assert.ok(out.includes('redacted') && out.includes('user@example.invalid'));
  assert.deepEqual(collectAttrSecrets('not json at all'), { ids: [], emails: [] });
});

test('Account_ids_and_emails_in_a_metrics_payload_are_found_in_every_data_point_type', () => {
  const attr = (key, v) => ({ key, value: { stringValue: v } });
  const point = tag => ({ attributes: [attr('user.account_uuid', `ACCT-${tag}`), attr('user.email', `${tag}@real.example`), attr('session.id', 'sess-keep')] });
  const kinds = ['sum', 'gauge', 'histogram', 'exponentialHistogram', 'summary'];
  const body = JSON.stringify({
    resourceMetrics: [{
      resource: { attributes: [attr('organization.id', 'ORG-RES'), attr('user.id', 'USER-RES')] },
      scopeMetrics: [{ metrics: kinds.map(k => ({ name: `m.${k}`, [k]: { dataPoints: [point(k)] } })) }],
    }],
  });
  const sink = JSON.stringify({ receivedAt: 1, path: '/v1/metrics', contentType: 'application/json', body });
  const found = collectAttrSecrets(sink);
  assert.deepEqual([...found.ids].sort(), ['ACCT-exponentialHistogram', 'ACCT-gauge', 'ACCT-histogram', 'ACCT-sum', 'ACCT-summary', 'ORG-RES', 'USER-RES']);
  assert.deepEqual([...found.emails].sort(), ['exponentialHistogram@real.example', 'gauge@real.example', 'histogram@real.example', 'sum@real.example', 'summary@real.example']);
  const out = sanitiseText(sink, buildMap({ ...ctx, email: null, idValues: found.ids, extraEmails: found.emails }));
  for (const secret of [...found.ids, ...found.emails]) assert.equal(out.includes(secret), false, secret);
  assert.ok(out.includes('sess-keep'));
});

test('A_resource_level_secret_in_a_logs_payload_is_collected_too', () => {
  const attr = (key, v) => ({ key, value: { stringValue: v } });
  const body = JSON.stringify({ resourceLogs: [{ resource: { attributes: [attr('user.email', 'res@real.example'), attr('organization.id', 'ORG-LOGRES')] }, scopeLogs: [] }] });
  const found = collectAttrSecrets(JSON.stringify({ path: '/v1/logs', body }));
  assert.deepEqual([found.ids, found.emails], [['ORG-LOGRES'], ['res@real.example']]);
});

test('The_scanner_flags_any_email_shape_except_the_two_placeholders', () => {
  const scan = text => leakScan([{ relPath: 'f.txt', text }], { out: null, home: '/home/user', user: 'user', host: 'host', email: null, token: null });
  assert.ok(scan('contact stranger@somewhere.example now').some(f => f.pattern === 'e-mail address shape'));
  assert.ok(scan('a.b+c@sub.domain.org').length > 0);
  assert.deepEqual(scan('mail noreply@anthropic.com and user@example.invalid and Noreply@Anthropic.com'), []);
  assert.deepEqual(scan('an @types/node import and a decorator @Injectable() are not emails'), []);
  assert.equal(JSON.stringify(scan('stranger@somewhere.example')).includes('stranger'), false, 'a finding never repeats the address');
});

test('The_scanner_names_the_file_and_the_pattern_for_every_kind_of_leak_and_never_the_matched_text', () => {
  const planted = [
    { relPath: 'a.txt', text: 'token sk-ant-oat01-AAAAAAAAAAAAAAAAAAAAAAAAAAAA end' },
    { relPath: 'b.txt', text: 'cwd /home/alice/x' },
    { relPath: 'c.txt', text: 'mail alice@corp.example' },
    { relPath: 'd.txt', text: 'host laptop-7' },
    { relPath: 'e.txt', text: 'out /cap-real/tree' },
    { relPath: 'f.txt', text: 'the secret is tok-123456789 here' },
    { relPath: 'g.txt', text: 'user alice ran' },
    { relPath: 'h.txt', text: 'some other machine /home/bob/project' },
    { relPath: 'i/-home-alice-tree/x.jsonl', text: 'clean text' },
  ];
  const found = leakScan(planted, { ...ctx, token: 'tok-123456789' });
  const expected = {
    'a.txt': 'credential shape sk-ant-', 'b.txt': 'home directory', 'c.txt': 'e-mail address', 'd.txt': 'host name', 'e.txt': 'raw out path',
    'f.txt': 'the token value', 'g.txt': 'user name', 'h.txt': 'foreign home directory', 'i/-home-alice-tree/x.jsonl': 'home directory',
  };
  for (const [file, pattern] of Object.entries(expected)) assert.ok(found.some(f => f.file === file && f.pattern === pattern), `${file} should be named for '${pattern}'; got ${JSON.stringify(found.filter(f => f.file === file))}`);
  assert.deepEqual(found.filter(f => f.file === 'a.txt').map(f => f.pattern), ['credential shape sk-ant-']);
  assert.equal(JSON.stringify(found).includes('tok-123456789'), false, 'a finding never repeats the secret');
  assert.deepEqual(leakScan([{ relPath: 'ok.txt', text: 'all /home/user clean user@example.invalid /cap/tree' }], ctx), []);
});

test('The_sanitised_output_of_a_leaky_input_scans_clean', () => {
  const dirty = [{ relPath: '-cap-real-tree/s.jsonl', text: 'cwd=/cap-real/tree home=/home/alice by alice@corp.example on laptop-7 as alice' }];
  const cleaned = dirty.map(f => ({ relPath: sanitisePath(f.relPath, map), text: sanitiseText(f.text, map) }));
  assert.deepEqual(leakScan(cleaned, ctx), []);
  assert.ok(leakScan(dirty, ctx).length > 0);
});

test('The_scanner_still_finds_a_planted_token_and_home_path_in_a_copy_of_the_fixtures', () => {
  const dir = tmpDir('plant');
  mkdirSync(path.join(dir, 'real'), { recursive: true });
  const here = localContext();
  writeFileSync(path.join(dir, 'real', 'planted.ndjson'), `{"t":"sk-ant-api03-ABCDEFGHIJKLMNOPQRSTUVWX","cwd":"${here.home}/x"}\n`);
  const found = leakScan(filesOf(dir), { ...here, out: null });
  assert.ok(found.some(f => f.file.endsWith('planted.ndjson') && f.pattern === 'credential shape sk-ant-'), JSON.stringify(found));
  assert.ok(found.some(f => f.pattern === 'home directory' || f.pattern === 'foreign home directory'));
});

test('The_committed_fixtures_are_clean_of_this_machines_identity_and_of_credentials', () => {
  const found = leakScan(filesOf(path.join(spikeDir, 'test', 'fixtures')), { ...localContext(), out: null });
  assert.deepEqual(found, []);
});

test('The_committed_capture_is_clean_and_the_scanner_still_bites', t => {
  if (!hasCapture()) return hostLacks(t, SKIP_CAPTURE);
  const found = leakScan(filesOf(REAL), { ...localContext(), out: null });
  assert.deepEqual(found, []);
  const claudeRoot = path.join(REAL, 'claude-2.1.310-capture');
  const argv = JSON.parse(readFileSync(path.join(claudeRoot, 'argv.json'), 'utf8'));
  assert.ok(!JSON.stringify(argv).includes(os.homedir()));
  return undefined;
});

test('The_machine_context_has_no_email_when_git_has_none', () => {
  const c = localContext({ gitEmail: () => null });
  assert.equal(c.email, null);
  assert.equal(c.home, os.homedir());
  assert.equal(c.user, os.userInfo().username);
});
