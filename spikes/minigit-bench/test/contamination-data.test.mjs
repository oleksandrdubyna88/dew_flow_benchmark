// A heredoc body is DATA the command writes, not a command it runs; a walk is one simple command's, never the whole call's; `rm -rf` deletes and reads nothing; a `//` in a quoted string is text.
// The A/A twin d3f571 (series s-20261030-92a9) was voided by a Makefile in a heredoc: its `rm -rf $(BUILD_DIR)` made the whole call a walk, and the `//` of a C# comment was read as the path `/`.
// The security probes stay high. Every other rule still sees the whole call (a heredoc may name the harness or upstream's repository).
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { stripData } from '../lib/contamination-heredoc.mjs';
import { boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';
import { pilotCommands } from './pilot-ts-calls.mjs';
import { spikeDir } from './helpers.mjs';

const home = os.homedir();
const ts = path.join(home, 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2');
const layout = layoutPaths(path.join(home, 'rsd', 'minigit', 's-20261030-92a9', 's'), 'd3f571');
const box = { layout, nodeDir: path.join(home, '.nvm', 'versions', 'node', 'v26.12.1'), toolchains: [ts], claudeBinary: path.join(home, 'rsd', 'minigit', 'toolchains', 'claude-2.1.310') };
const ctx = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box), isLink: () => false };
const hitsOf = (command, tool = 'Bash') => contaminationHits({ calls: [{ agent: 'main', tool, input: { command } }], ctx });
const high = command => hitsOf(command).filter(h => h.severity === 'high').map(h => h.rule);
const underRoot = p => path.relative('/', p);
const D3F571 = readFileSync(path.join(spikeDir, 'test', 'fixtures', 'pilot-d3f571-call-1.txt'), 'utf8').replaceAll('@SESSION@', layout.dir);

test('The_pilots_calls_that_were_voided_or_flagged_are_no_high_hit', () => {
  assert.deepEqual(high(D3F571), [], 'd3f571 call 1: a cd to its own tree, then a Makefile with rm -rf and a C# comment');
  assert.ok(hitsOf(D3F571).every(h => h.severity === 'low'), 'at most low hits');
  for (const command of pilotCommands({ session: layout.dir, ts })) assert.deepEqual(high(command), [], command.slice(0, 60));
});

test('A_heredoc_body_is_data_and_no_reach_walk_or_cd', () => {
  const body = lines => `cat > f <<'EOF'\n${lines.join('\n')}\nEOF`;
  for (const lines of [['// a comment', '/* block */', '</OutputType>'], ['rm -rf $(BUILD_DIR)', 'cd /', 'grep -r token /'], [`cat ${home}/.ssh/id_rsa`, 'find / -name x']]) assert.deepEqual(hitsOf(body(lines)), [], lines.join(' | '));
  assert.deepEqual(hitsOf(`cat <<EOF > out.txt\n${home}/x\nEOF\ncat out.txt`), [], 'an unquoted marker, the redirect after it');
  assert.deepEqual(hitsOf(`cat <<-EOF\n\t${home}/x\n\tEOF\nls`), [], '<<- closes on an indented terminator');
  assert.deepEqual(hitsOf(`cat <<A <<B\n${home}/x\nA\n${home}/y\nB\nls`), [], 'two heredocs on one line');
  assert.deepEqual(hitsOf(`cat <<EOF\n${home}/never-closed`), [], 'no terminator: the body runs to the end, as in a shell');
  assert.deepEqual(hitsOf(`grep x <<< "${home}/x"`), [], 'a here-string is data');
  assert.deepEqual(high(`cat <<EOF\ncd /\nEOF\ngrep -r token .`), [], 'a cd in a body does not move the next command');
});

test('The_command_after_a_heredoc_and_the_line_that_opens_it_are_still_commands', () => {
  assert.deepEqual(high(`cat > f <<EOF\nx\nEOF\ncat ${home}/.ssh/id_rsa`), ['outside-path'], 'after the terminator');
  assert.deepEqual(high(`cat ${home}/.ssh/id_rsa > f <<EOF\nx\nEOF`), ['outside-path'], 'on the opening line');
  assert.deepEqual(high(`cat <<EOF && cat ${home}/x\nbody\nEOF`), ['outside-path'], 'after the marker on its line');
  assert.deepEqual(high(`echo $((1 << 2)); cat ${home}/x\nls ${home}/y`).length, 1, 'a shift is no heredoc: the next line is read');
});

test('A_body_that_a_shell_or_an_interpreter_runs_is_code_and_is_kept', () => {
  assert.deepEqual(high(`bash <<EOF\ncat ${home}/.ssh/id_rsa\nEOF`), ['outside-path']);
  assert.deepEqual(high(`cat <<EOF | sh\ngrep -r token ${home}\nEOF`), ['outside-path']);
  assert.deepEqual(high(`bash <<< "cat ${home}/x"`), ['outside-path']);
  assert.deepEqual(high(`python3 - <<EOF\nopen('${home}/x')\nEOF`), ['outside-path']);
});

test('The_text_rules_and_the_script_link_rule_still_read_the_whole_call_including_the_heredoc_body', () => {
  const rules = command => hitsOf(command).map(h => h.rule);
  assert.deepEqual(rules('cat > f <<EOF\nsee mame/ai-coding-lang-bench\nEOF'), ['upstream-solutions']);
  assert.deepEqual(rules('cat > f <<EOF\n/home/x/dew_flow_benchmark/y\nEOF'), ['harness-name']);
  const py = `cat > mk.py <<EOF\nimport os\nos.symlink('${'../'.repeat(12)}${underRoot(home)}', 'up')\nEOF\npython3 mk.py`;
  assert.deepEqual(rules(py), ['link-out'], 'the link a script file would make is still seen');
});

test('stripData_replaces_each_body_by_one_placeholder_and_hands_back_its_text', () => {
  const { text, data } = stripData(`cat > a <<'X'\none\ntwo\nX\nls\ncat > b <<Y\nthree\nY`);
  assert.deepEqual(text.split('\n'), ["cat > a <<'X'", '\u0001DATA0\u0001', 'ls', 'cat > b <<Y', '\u0001DATA1\u0001']);
  assert.deepEqual(data, ['one\ntwo', 'three']);
  assert.equal(stripData('ls /x').text, 'ls /x');
});

test('A_walk_is_one_simple_commands_never_the_whole_calls', () => {
  assert.deepEqual(high(`ls ${path.dirname(home)}; grep -r token src`), [], 'a -r elsewhere does not make the listing of a dir above the home a walk');
  assert.deepEqual(high(`rm -rf build; ls /`), []);
  assert.deepEqual(high(`ls /; grep -r token /`), ['outside-path'], 'the walk itself');
  assert.deepEqual(high(`ls /\ngrep -r token src | wc -l`), []);
});

test('rm_is_not_a_reading_walk_and_its_path_words_are_judged_as_any_reach', () => {
  assert.deepEqual(high(`rm -rf ${home}/x`), ['outside-path']);
  assert.deepEqual(high('rm -rf build'), []);
  assert.deepEqual(hitsOf('rm -rf build'), []);
  assert.deepEqual(hitsOf('rm -rf /tmp/mg').map(h => h.severity), ['low']);
  assert.deepEqual(hitsOf('cd "$d" && rm -rf out'), [], 'an unknown directory and rm -rf of a relative word');
  assert.deepEqual(high(`cd ${home} && rm -rf cache`), ['outside-path'], 'a relative word resolved from the cd target is still a reach');
  assert.deepEqual(high('find . -name x -exec rm -rf {} +'), [], 'find walks, here inside the tree');
  assert.deepEqual(high(`find ${home} -name x -exec rm -rf {} +`), ['outside-path']);
  assert.deepEqual(high(`FOO=1 rm -rf /`), [], 'the command word is rm after an assignment');
});

test('A_comment_mark_in_a_quoted_string_is_text_and_not_the_path_slash', () => {
  assert.deepEqual(high('grep -rn "a // b" src'), []);
  assert.deepEqual(high("grep -rn 'see /* x */ y' ."), []);
  assert.deepEqual(high('echo "x // y" | grep -r z .'), []);
  assert.deepEqual(high('find // -name settings.json'), ['outside-path'], 'a word position: the path //');
  assert.deepEqual(high('grep -r token //'), ['outside-path']);
});

test('The_security_probes_stay_high', () => {
  const harness = underRoot(path.join(layout.harness, 'settings.json'));
  const probes = [`grep -r token ${home}`, `cd / && cat ${harness}`, 'ln -s ../.. up && grep -r token up', 'find / -name settings.json', 'cd $OLDPWD && grep -r . .', 'grep -rn token /', 'cd - && find . -name x',
    `cd ${home} && grep -r token .`, `tar cf - ${home} | wc -c`, `du -a ${path.dirname(home)}`, `ls -R ${home}`, `find ${layout.dir} -name x`, `cd ${layout.dir} && cat harness/settings.json`];
  for (const probe of probes) assert.ok(hitsOf(probe).some(h => h.severity === 'high'), probe);
  assert.ok(hitsOf('x', 'Grep').length === 0);
  const tool = (name, input) => contaminationHits({ calls: [{ agent: 'main', tool: name, input }], ctx }).filter(h => h.severity === 'high').length;
  assert.equal(tool('Grep', { pattern: 't', path: '/' }), 1, 'the Grep tool over /');
  assert.equal(tool('Glob', { pattern: '**', path: home }), 1, 'the Glob tool over the home');
});

test('A_walk_of_a_glob_is_a_walk_of_the_globs_fixed_prefix_and_a_plain_listing_of_it_is_not', () => {
  const above = path.dirname(home);
  for (const command of ['grep -r token /*', 'grep -rn token /*/x', `find ${above}/*`, 'find /tm* -name x', `grep -r token ${above}/*`, `find ${home}/rs*`, 'du -a ~/../../*', 'tar cf - /*', 'cd / && grep -r token tm*', `cd ${above} && find ${path.basename(home)}/*`, 'rg token /[a-h]*']) {
    assert.deepEqual(high(command), ['outside-path'], command);
  }
  for (const command of ['ls /*', 'ls -la /home/*', `ls ${home}/*`.replace(home, above), 'cat /*', 'cd / && ls ho*', 'cd / && cat ho*/x', 'grep -r token /usr/*', 'find /usr/* -name x', 'grep -r token src/*', 'cd src && grep -r token *.ts', 'cd /tmp/mg && grep -r token f*']) {
    assert.deepEqual(high(command), [], command);
  }
  assert.deepEqual(high(`ls ${home}/*`), ['outside-path'], 'a listing of a glob UNDER a protected root is a reach as ever');
  assert.deepEqual(high('grep -rn "a /* b" src'), [], 'a comment mark in a quoted string is still text');
  assert.deepEqual(hitsOf('ls /*').map(h => h.severity), ['low'], 'a plain listing of the glob stays low');
});

test('A_20000_line_heredoc_is_stripped_in_linear_time', () => {
  const body = Array.from({ length: 20000 }, (_, i) => `const line${i} = ${i}; // a comment`).join('\n');
  const started = performance.now();
  const { text, data } = stripData(`cat > big.ts <<'EOF'\n${body}\nEOF\nls`);
  const ms = performance.now() - started;
  assert.deepEqual(text.split('\n'), ["cat > big.ts <<'EOF'", '\u0001DATA0\u0001', 'ls']);
  assert.equal(data[0].split('\n').length, 20000);
  assert.ok(ms < 500, `${ms.toFixed(0)} ms: copying the lines again at every line is quadratic`);
});
