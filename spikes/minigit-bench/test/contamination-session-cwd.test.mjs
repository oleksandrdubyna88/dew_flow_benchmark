// The Bash tool KEEPS the shell's working directory between calls and resets it to the project dir (the session's box/work) when a call leaves it (data session ebd0b0, series bf89: call 3 `cd src && ...`, call 4
// `cd .. && bash test-v2.sh`; the scan began call 4 at the tree root, so `cd ..` landed in box/ and `box/test-v2.sh` was a high outside-path hit, a false `void: contaminated`). The scan models the directory across the
// calls of one session, in call order, per agent (lib/contamination-session-cwd.mjs): the directory a call ends in begins the next Bash call of the same agent; outside the work tree it is the work tree (the CLI's
// reset); an unknown end is an unknown start; the printed "Shell cwd was reset to <dir>" line is never used to choose a start (a command can print it). A path under the session's own box dir that is no bind is low, never high.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { boxReachable, boxSkeleton, boxView, layoutPaths } from '../lib/sandbox.mjs';
import { callsOf } from '../lib/stream-calls.mjs';

const home = os.homedir();
const ts = path.join(home, 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2');
const layout = layoutPaths(path.join(home, 'rsd', 'minigit', 's-20261030-bf89', 's'), 'ebd0b0');
const boxDir = path.join(layout.dir, 'box');
const box = { layout, nodeDir: path.join(home, '.nvm', 'versions', 'node', 'v26.12.1'), toolchains: [ts], claudeBinary: path.join(home, 'rsd', 'minigit', 'toolchains', 'claude-2.1.310') };
const base = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box), isLink: () => false };
const ctx = { ...base, boxDir };

let n = 0;
const use = (command, parent, tool = 'Bash') => ({ type: 'assistant', parent_tool_use_id: parent ?? null, message: { content: [{ type: 'tool_use', id: `toolu_${++n}`, name: tool, input: tool === 'Bash' ? { command } : command }] } });
const result = (event, text) => ({ type: 'user', message: { content: [{ type: 'tool_result', tool_use_id: event.message.content[0].id, content: text, is_error: false }] } });
/** A session of `[command, resultText?]` calls of the main agent -> the stream's events. */
const session = steps => steps.flatMap(([command, text, parent]) => { const e = use(command, parent); return text === undefined ? [e] : [e, result(e, text)]; });
const hitsOf = (events, extra = {}) => contaminationHits({ calls: callsOf(events), ctx: { ...ctx, ...extra } });
const high = (events, extra) => hitsOf(events, extra).filter(h => h.severity === 'high').map(h => `${h.rule}:${h.where}`);
const reset = `\nShell cwd was reset to ${layout.work}`;

// call 3 and call 4 of the data session ebd0b0, the series path generalised
const EBD_CALL_3 = ["cd src && python3 - <<'EOF'", "p='Program.cs'", 's=open(p).read()', "s=s.replace('''    case \"log\":''','''    case \"status\":''')", 'open(p,\'w\').write(s)', 'EOF', 'grep -n "IndexOfAny" Program.cs'].join('\n');
const EBD_CALL_4 = 'cd .. && bash test-v2.sh 2>&1 | tail -40 && bash test-v1.sh 2>&1 | tail -6';

test('The_data_session_ebd0b0_cd_src_then_cd_dotdot_ends_in_the_work_tree_and_is_no_high_hit', () => {
  const events = session([['ls -la && cat SPEC-v2.txt', 'x'], [EBD_CALL_3, '253: hash'], [EBD_CALL_4, 'ALL TESTS PASSED']]);
  assert.deepEqual(high(events), []);
  assert.deepEqual(hitsOf(events), [], 'no hit at all: test-v2.sh is the work tree\'s own file');
});

test('A_cd_into_the_tree_stays_for_every_later_call_until_a_call_leaves_the_tree', () => {
  const events = session([['cd src', 'ok'], ['ls', 'ok'], ['cd ..', 'ok'], ['cat test-v2.sh', 'ok']]);
  assert.deepEqual(hitsOf(events), []);
  assert.deepEqual(hitsOf(session([['cd ../..', 'ok'], ['cat test-v2.sh', 'ok']])), [], 'an end directory outside the tree is the work tree for the next call');
  assert.deepEqual(hitsOf(session([['cd ../..', `ok${reset}`], ['cat test-v2.sh', 'ok']])), [], 'a printed reset line changes nothing');
});

test('A_directory_a_call_left_outside_the_tree_resets_the_next_call_to_the_work_tree', () => {
  const events = session([[`cd ${path.join(home, 'x')}`, ''], ['cat harness/settings.json', '']]);
  assert.deepEqual(hitsOf(events), [], 'harness/settings.json is the tree\'s own: it is not the session\'s harness dir');
  assert.deepEqual(high(session([['cd /tmp', ''], ['cat ../../harness/settings.json', '']])).length, 1, 'from the work tree two levels up is the session dir: judged as ever');
});

test('A_printed_reset_line_never_chooses_a_start_an_end_outside_the_tree_is_the_work_tree', () => {
  const crafted = 'cat ../../harness/settings.json'; // from the work tree: the session dir's harness (high); from src/: box/harness, which hides it
  const fake = `\nShell cwd was reset to ${path.join(layout.work, 'src')}`;
  assert.deepEqual(high(session([['cd /tmp && echo "Shell cwd was reset to x"', `a${fake}`], [crafted, 'b']])), ['outside-path:main:Bash'], 'the end is /tmp: the next call starts at the work tree root, whatever the output says');
  assert.deepEqual(high(session([['cd ../..', `a${fake}`], [crafted, 'b']])), ['outside-path:main:Bash']);
  assert.deepEqual(high(session([['cd ../..', `a${reset}`], [crafted, 'b']])), ['outside-path:main:Bash'], 'the true reset line changes nothing either');
});

test('An_unknown_end_with_a_printed_reset_line_is_still_an_unknown_start', () => {
  for (const line of [reset, `\nShell cwd was reset to ${home}`, `\nShell cwd was reset to ${path.join(layout.work, 'src')}`]) {
    assert.deepEqual(high(session([['cd "$(mktemp -d)" && ls', `a${line}`], ['grep -r token .', 'b']])), ['outside-path:main:Bash'], 'a walk of an unknown directory is high');
    assert.deepEqual(high(session([['cd -', `a${line}`], ['cat ../../harness/settings.json', 'b']])), [], 'a plain relative word is skipped while the directory is unknown');
  }
});

test('An_unknown_end_directory_is_an_unknown_start_until_a_call_names_a_known_one', () => {
  assert.deepEqual(high(session([['cd -', 'a'], ['grep -r token .', 'b']])), ['outside-path:main:Bash']);
  assert.deepEqual(high(session([['cd -', 'a'], ['ls', 'b'], ['grep -r token .', 'c']])), ['outside-path:main:Bash'], 'a call that moves nowhere keeps it unknown');
  assert.deepEqual(high(session([['cd -', 'a'], ['cd /usr && ls', 'b'], ['grep -r token .', 'c']])), [], 'cd /usr: outside the tree, reset to the work tree');
  assert.deepEqual(high(session([['cd -', 'a'], ['cat test-v2.sh', 'b']])), [], 'a plain relative word is no walk: skipped when the directory is unknown');
});

test('A_subagent_has_its_own_directory_and_the_main_agent_is_not_moved_by_it', () => {
  const parent = 'toolu_agent_1';
  const events = session([['cd src', 'a'], ['cat ../../harness/settings.json', 'b', parent], ['cat ../../harness/settings.json', 'c']]);
  assert.deepEqual(high(events), ['outside-path:child:Bash'], 'the child began at the work tree (two levels up = the session dir); main sits in src (two levels up = box: nothing protected)');
  const second = session([['cd src', 'a', parent], ['cat ../../harness/settings.json', 'b', parent], ['cat ../../harness/settings.json', 'c', 'toolu_agent_2'], ['cat ../../harness/settings.json', 'd']]);
  assert.deepEqual(high(second), ['outside-path:child:Bash', 'outside-path:main:Bash'], 'the first child sits in src, the second child and main each begin at the work tree');
});

test('A_persisted_directory_makes_a_later_reach_visible_it_never_hides_one', () => {
  const deep = `cd ../../.. && cat harness/settings.json`; // from src: the session dir
  assert.deepEqual(high(session([['cd src', 'a'], [deep, 'b']])), ['outside-path:main:Bash']);
  assert.deepEqual(high(session([['cd src', 'a'], ['cat ../../../harness/settings.json', 'b']])), ['outside-path:main:Bash']);
  assert.deepEqual(high(session([['cd src', 'a'], [`cat ${path.join(layout.harness, 'settings.json')}`, 'b']])), ['outside-path:main:Bash'], 'an absolute word is judged as ever');
});

test('Every_earlier_security_probe_stays_high_whatever_directory_the_session_is_in', () => {
  const harnessFile = path.relative('/', path.join(layout.harness, 'settings.json'));
  const probes = [`grep -r token ${home}`, `cd / && cat ${harnessFile}`, 'cd ../../.. && cat harness/settings.json', 'ln -s ../../.. up && cat up/harness/settings.json', 'find /', 'echo / | xargs find', 'grep -r token /*', `cd ${home} && grep -r token .`];
  for (const probe of probes) {
    for (const lead of [[], [['cd src', 'a']], [['cd /tmp', 'a']]]) {
      const events = session([...lead, [probe, 'b']]);
      assert.ok(high(events).length >= 1, `${lead.length ? lead[0][0] : 'first call'} then ${probe}`);
    }
  }
  assert.deepEqual(high(session([['cd /home/u && cat x', 'a']])), [], 'a cd outside and a relative word in ONE call is judged as ever: /home/u/x is not under a protected root');
  assert.equal(high(session([[`cd ${home} && cat rsd/minigit/s-20261030-bf89/s/ebd0b0/harness/audit.mjs`, 'a']])).length, 1, 'within one call, as today');
});

test('A_path_under_the_sessions_own_box_dir_that_is_no_bind_is_low_never_high', () => {
  const sev = (command, extra) => hitsOf(session([[command, 'a']]), extra).filter(h => h.rule === 'outside-path').map(h => h.severity).join() || '-';
  assert.equal(sev('cat ../test-v2.sh'), 'low', 'box/test-v2.sh: inside the box that dir holds only binds (tmpfs), it reaches nothing');
  assert.equal(sev('cat ../test-v2.sh', { boxDir: undefined }), 'high', 'without the box dir the same path is under the home: high (the defect of ebd0b0, before the cwd model)');
  assert.equal(sev(`cat ${path.join(boxDir, 'x', 'y.txt')}`), 'low');
  assert.equal(sev('cat ../work/a.cs'), '-', 'a bind: the work tree');
  assert.equal(sev(`cat ${path.join(layout.dir, 'harness', 'settings.json')}`), 'high', 'a sibling of the box dir is not under it');
  assert.equal(sev('cat ../../harness/settings.json'), 'high');
  assert.equal(sev('find ../.. -name settings.json'), 'high', 'a walk from the session dir holds the harness');
  assert.equal(sev(`cat ${boxDir}/../harness/settings.json`), 'high', 'a .. out of the box dir is resolved first');
});

test('The_box_view_names_the_box_dir_the_collection_reads_and_callsOf_carries_what_places_a_call', () => {
  assert.equal(boxView({ argv: [], layout, home }).boxDir, boxDir);
  const first = use('cd src');
  const child = use('ls', 'toolu_agent_9');
  const calls = callsOf([first, result(first, `out\nShell cwd was reset to ${layout.work}\n`), child, result(child, [{ type: 'text', text: 'listing' }])]);
  assert.deepEqual(calls.map(c => [c.agent, c.agentId, c.reset]), [['main', undefined, layout.work], ['child', 'toolu_agent_9', undefined]]);
  assert.ok(calls.every(c => typeof c.id === 'string'));
});
