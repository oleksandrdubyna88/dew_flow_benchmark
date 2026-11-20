// The stub `claude` of the smoke tests: a script that speaks the CLI's stream-json init and result events, calls the broker once with the credential it was given and writes the token file on purpose.
// `version` is what its init event says (`claude_code_version`): the one thing a test of a pinned CLI changes.
export const smokeStub = (version = '2.1.310') => `#!/usr/bin/env node
const fs = require('node:fs'); const http = require('node:http');
const say = o => process.stdout.write(JSON.stringify(o) + '\\n');
const post = (url, body, headers) => new Promise((resolve, reject) => { const u = new URL(url); const req = http.request({ host: u.hostname, port: u.port, path: u.pathname, method: 'POST', headers: { 'content-type': 'application/json', ...headers } }, res => { res.resume(); res.on('end', resolve); }); req.on('error', reject); req.end(body); });
(async () => {
  const tools = process.argv[process.argv.indexOf('--tools') + 1].split(',').filter(t => t !== 'TodoWrite').map(t => (t === 'Agent' ? 'Task' : t));
  say({ type: 'system', subtype: 'init', cwd: process.cwd(), session_id: 'stub', tools, mcp_servers: [], model: 'claude-sonnet-5-5', permissionMode: 'default', apiKeySource: 'none', claude_code_version: '${version}', memory_paths: { auto: process.env.CLAUDE_CONFIG_DIR + '/projects/-p/memory/' } });
  await post(process.env.ANTHROPIC_BASE_URL + '/v1/messages', '{}', { authorization: 'Bearer ' + process.env.CLAUDE_CODE_OAUTH_TOKEN });
  fs.writeFileSync('token.txt', 'CLAUDE_CODE_OAUTH_TOKEN=' + process.env.CLAUDE_CODE_OAUTH_TOKEN + '\\n');
  await new Promise(r => setTimeout(r, 1500)); // long enough for the sampler to take its five samples
  fs.mkdirSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p', { recursive: true });
  fs.writeFileSync(process.env.CLAUDE_CONFIG_DIR + '/projects/-p/s.jsonl', '{}\\n');
  say({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 0.009232, result: 'DONE' });
})();
`;
