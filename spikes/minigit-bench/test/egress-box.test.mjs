// The egress proxy from INSIDE the agent box, through the real box-init: effect tests that need the network (they skip, with the reason,
// when the host itself cannot reach api.nuget.org).
import assert from 'node:assert/strict';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { egressProxy, egressSummary } from '../lib/egress.mjs';
import { EGRESS_HOSTS } from '../lib/egress-hosts.mjs';
import { brokerServer } from '../lib/broker.mjs';
import { prices, fakeToken } from './broker-stub.mjs';
import { envFor, hostHas, newSession, onHost, probeInit, TS_DIR } from './box-helpers.mjs';
import { hostLacks, sandboxGate } from './helpers.mjs';

/** Whether the host reaches api.nuget.org:443 (the positive control of an allowed host). */
async function hostHasNetwork() {
  if (!hostHas('curl')) return false;
  const r = await onHost('curl', ['-s', '-o', '/dev/null', '-m', '15', '-w', '%{http_code}', 'https://api.nuget.org/v3/index.json'], 30_000);
  return r.ok && r.value.stdout === '200';
}

async function withProxy(allow, fn) {
  const layout = newSession();
  const lines = [];
  const proxy = await egressProxy({ socketPath: layout.sockets.proxy, allow, log: l => lines.push(l) });
  try {
    return await fn({ layout, lines });
  } finally {
    await proxy.close();
  }
}

test('The_proxy_allows_only_the_listed_hosts_on_443', async t => {
  if (!sandboxGate(t)) return;
  if (!await hostHasNetwork()) return hostLacks(t, 'the host cannot reach https://api.nuget.org: the positive control of an allowed host is unavailable');
  await withProxy(['api.nuget.org'], async ({ layout, lines }) => {
    const code = url => `curl -s -o /dev/null -m 20 -w '%{http_code}' ${url}; echo " rc=$?"`;
    const r = await probeInit(layout, [code('https://api.nuget.org/v3/index.json'), code('https://example.com/'), code('https://api.anthropic.com/')].join('\n'));
    const [allowed, example, anthropic] = r.out.trim().split('\n');
    assert.match(allowed, /^200 rc=0$/, r.out);
    for (const refused of [example, anthropic]) assert.match(refused, /^000 rc=(7|56)$/, `a refused host fails the CONNECT: ${refused}`);
    assert.deepEqual(egressSummary(lines), { allowed: { 'api.nuget.org': 1 }, refused: { 'example.com': 1, 'api.anthropic.com': 1 }, nonConnect: 0 });
    assert.ok(lines.filter(l => l.verdict === 'refused').every(l => l.status === 403));
  });
});

test('Plain_HTTP_is_refused_and_the_restore_does_not_need_it', async t => {
  if (!sandboxGate(t)) return;
  if (!hostHas('dotnet') || !await hostHasNetwork()) return hostLacks(t, 'needs dotnet on PATH and the network (api.nuget.org) on this host');
  await withProxy(EGRESS_HOSTS.bare, async ({ layout, lines }) => {
    writeProject(layout);
    const gets = "curl -s -o /dev/null -m 20 -w '%{http_code}' -x \"$HTTP_PROXY\" http://example.com/; echo ' via-proxy'; curl -s -o /dev/null -m 20 -w '%{http_code}' --noproxy '' http://127.0.0.1:3128/; echo ' direct'";
    const g = await probeInit(layout, gets);
    assert.match(g.out, /405 via-proxy/);
    assert.match(g.out, /405 direct/, 'a GET sent straight at the box\'s proxy port is answered 405 too');
    const before = egressSummary(lines);
    assert.equal(before.nonConnect, 2);
    const restore = await probeInit(layout, `cd ${layout.work} && dotnet restore -m:4 --disable-build-servers >/tmp/restore.txt 2>&1; echo "restore=$?"`);
    assert.match(restore.out, /restore=0/, 'the restore succeeds with every plain-HTTP request refused');
    const after = egressSummary(lines);
    assert.deepEqual(Object.keys(after.allowed), ['api.nuget.org'], 'no allowed host but api.nuget.org');
    assert.ok(after.nonConnect >= 2, `dotnet's CRL/OCSP fetches (if any) are counted under nonConnect (${after.nonConnect})`);
  });
});

test('Npm_and_dotnet_restore_through_the_proxy_from_inside_the_box', async t => {
  if (!sandboxGate(t)) return;
  if (!hostHas('dotnet') || !hostHas('npm') || !await hostHasNetwork()) return hostLacks(t, 'needs dotnet, npm and the network on this host');
  await withProxy(EGRESS_HOSTS.bare, async ({ layout, lines }) => {
    writeProject(layout);
    const script = [
      'npm install --prefix /tmp/ts typescript@7.0.2 >/tmp/npm.txt 2>&1; echo "npm=$?"',
      '/tmp/ts/node_modules/.bin/tsc --version',
      `cd ${layout.work} && dotnet restore -m:4 --disable-build-servers >/tmp/restore.txt 2>&1; echo "restore=$?"`,
    ].join('\n');
    const r = await probeInit(layout, script, { env: envFor(layout, { tsDir: TS_DIR }) });
    assert.match(r.out, /npm=0/, r.out);
    assert.match(r.out, /7\.0\.2/);
    assert.match(r.out, /restore=0/);
    assert.deepEqual(Object.keys(egressSummary(lines).allowed).sort(), ['api.nuget.org', 'registry.npmjs.org']);
    assert.deepEqual(egressSummary(lines).refused, {});
  });
}, { timeout: 180_000 });

test('A_direct_post_from_inside_the_box_at_zero_budget_is_refused_and_the_upstream_count_is_unchanged', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const seen = [];
  const lines = [];
  const broker = await brokerServer({
    socketPath: layout.sockets.api, token: fakeToken(), upstream: { host: '127.0.0.1', port: 9, tls: false }, budget: { capUsd: 0, reserveUsd: 0.05 },
    prices: prices(), cliVersion: '2.1.310', log: l => { lines.push(l); seen.push(l.event); },
  });
  try {
    const post = "curl -s -o /tmp/resp.json -w 'http=%{http_code}' -X POST http://127.0.0.1:8787/v1/messages -H 'content-type: application/json' --data '{\"model\":\"m\",\"max_tokens\":5,\"messages\":[]}'; echo; cat /tmp/resp.json";
    const r = await probeInit(layout, post);
    assert.match(r.out, /http=429/);
    assert.match(r.out, /rate_limit_error/);
    assert.equal(broker.summary().upstreamRequests, 0, 'the broker forwarded nothing');
    assert.deepEqual(seen.filter(e => e === 'forwarding'), []);
    assert.equal(lines.filter(l => l.event === 'refused').length, 1);
  } finally {
    await broker.close();
  }
});

/** The N-8 project: a net10.0 console with FluentAssertions 7.2.2, written into the box's cwd. */
function writeProject(layout) {
  mkdirSync(layout.work, { recursive: true });
  writeFileSync(path.join(layout.work, 'probe.csproj'), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="FluentAssertions" Version="7.2.2" /></ItemGroup>
</Project>
`);
  writeFileSync(path.join(layout.work, 'Program.cs'), 'System.Console.WriteLine("hi");\n');
}
