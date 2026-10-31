// A heredoc an INTERPRETER runs (`python3 - <<'EOF'`) is code in that language, not shell. Series b82e, TS v2 session 97168d, call 3: a Python body that edited src/minigit.ts held the TypeScript literal `hash.includes("/")`,
// the shell path rules tokenised the Python as shell, the literal "/" became the path `/` in a walk, and a clean session was voided (`outside-path` high on "/"). Only a body a SHELL runs is parsed as shell; an interpreter's body is judged
// by its quoted absolute-path literals (plain reaches, never walks), its script symlink calls (link-out) and the text rules. The controls stay high.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { pathLiterals, stripData } from '../lib/contamination-heredoc.mjs';
import { spikeDir } from './helpers.mjs';

const HOME = '/home/u';
const TREE = `${HOME}/rsd/minigit/s-x/s/ab12/box/work`;
const HARNESS = `${HOME}/rsd/minigit/s-x/s/ab12/harness`;
const ctx = Object.freeze({
  rid: 'ab12', treeRoot: TREE, home: HOME, boxHome: `${HOME}/rsd/minigit/s-x/s/ab12/box/home`, harnessPaths: [HARNESS], partnerPaths: [], arm: 'bare', ownSecrets: [],
  allow: [`${HOME}/rsd/minigit/s-x/s/ab12/box/tmp`, `${HOME}/rsd/minigit/s-x/s/ab12/box/home`, '/usr/'],
});
const hitsOf = command => contaminationHits({ calls: [{ agent: 'main', tool: 'Bash', input: { command } }], ctx });
const high = command => hitsOf(command).filter(h => h.severity === 'high').map(h => h.rule);
const heredoc = (opener, ...lines) => `${opener} <<'EOF'\n${lines.join('\n')}\nEOF`;
// the shape of that call (a Python body editing a TypeScript file), its identifiers and messages changed: the real text matches the held-out suite's strings, so it is never committed
const CALL = readFileSync(path.join(spikeDir, 'test', 'fixtures', 'interpreter-heredoc-ts-edit-call.txt'), 'utf8');

test('The_python_heredoc_that_edits_a_TypeScript_file_is_no_high_hit', () => {
  assert.deepEqual(high(CALL), [], 'the shape of TS v2 97168d call 3: the literal "/" in the TypeScript text is no path');
  assert.deepEqual(hitsOf(CALL), [], 'and no hit at all');
});

test('An_interpreters_body_is_no_shell_whatever_the_interpreter', () => {
  const body = ['text = "a/b".split("/")', 'if (x.includes("/")) find / -name x', 'grep -r token /', 'cd /', '// a comment /* block */'];
  for (const opener of ['python3 -', 'python -', 'python3.12 -', 'node -', 'nodejs -', 'ruby', 'perl', 'php', 'deno run -', 'bun run -', 'tsx -', 'ts-node -', 'env python3 -']) {
    assert.deepEqual(high(heredoc(opener, ...body)), [], opener);
  }
  assert.deepEqual(high(`cat <<'EOF' | node\n${body.join('\n')}\nEOF`), [], 'a heredoc piped into an interpreter');
});

test('A_quoted_absolute_path_literal_in_an_interpreters_body_is_a_plain_reach', () => {
  assert.deepEqual(high(heredoc('python3 -', 'import os', `print(open('${HOME}/.ssh/id_rsa').read())`)), ['outside-path']);
  assert.deepEqual(high(heredoc('node -', `require('fs').readFileSync("${HOME}/x")`)), ['outside-path']);
  assert.deepEqual(high(heredoc('node -', `require('fs').readFileSync('${HARNESS}/settings.json')`)), ['outside-path'], 'a harness root');
  assert.deepEqual(high(heredoc('ruby', `File.read("${HOME}/.aws/credentials")`)), ['outside-path']);
  assert.deepEqual(hitsOf(heredoc('python3 -', "open('/etc/hosts')")).map(h => `${h.rule}:${h.severity}`), ['outside-path:low'], 'a system path is low, as a plain reach for it is');
});

test('A_literal_is_never_a_walk_and_a_lone_slash_or_a_comment_mark_is_no_path', () => {
  assert.deepEqual(high(heredoc('python3 -', 'os.walk("/")', "os.walk('/home')")), [], 'the body is not read as a walk: / and /home above the home are plain, and /home is no protected root');
  assert.deepEqual(high(heredoc('python3 -', 'a = "//"', "b = '/'", 'c = "/*"', 'd = "/ x"')), []);
});

test('A_symlink_call_in_an_interpreters_body_is_still_link_out', () => {
  const rules = command => hitsOf(command).filter(h => h.severity === 'high').map(h => h.rule);
  assert.deepEqual(rules(heredoc('python3 -', 'import os', "os.symlink('/home', 'up')")), ['link-out']);
  assert.deepEqual(rules(heredoc('python3 -', 'import os', `os.symlink('${'../'.repeat(12)}home/u', 'up')`)), ['link-out'], 'a relative target is resolved from the link');
  assert.deepEqual(rules(heredoc('node -', "require('fs').symlinkSync('/home/u', 'x')")), ['outside-path', 'link-out'], 'the literal /home/u is also a plain reach of the home');
  assert.deepEqual(rules(heredoc('python3 -', "os.symlink('src', 'up')")), [], 'a link inside the tree');
});

test('A_body_a_shell_runs_is_still_shell_and_is_parsed_as_shell', () => {
  assert.deepEqual(high(heredoc('bash', `grep -r token ${HOME}`)), ['outside-path']);
  assert.deepEqual(high(heredoc('sh', 'find / -name x')), ['outside-path']);
  assert.deepEqual(high(heredoc('zsh', 'grep -r token /')), ['outside-path']);
  assert.deepEqual(high(heredoc('dash', `cat ${HOME}/.ssh/id_rsa`)), ['outside-path']);
  assert.deepEqual(high(`cat <<'EOF' | sh\ngrep -r token ${HOME}\nEOF`), ['outside-path']);
  assert.deepEqual(high(`source /dev/stdin <<'EOF'\ncat ${HOME}/x\nEOF`), ['outside-path']);
  assert.deepEqual(high(`eval "$(cat <<'EOF'\ncat ${HOME}/x\nEOF\n)"`), ['outside-path']);
  assert.deepEqual(high(`bash <<< "grep -r token ${HOME}"`), ['outside-path']);
  assert.deepEqual(high(`python3 -c x; bash <<'EOF'\ngrep -r token /\nEOF`), ['outside-path'], 'a shell on the line wins');
});

test('The_text_rules_still_read_the_interpreters_body', () => {
  const rules = command => hitsOf(command).map(h => h.rule);
  assert.deepEqual(rules(heredoc('python3 -', '# see mame/ai-coding-lang-bench')), ['upstream-solutions']);
  assert.deepEqual(rules(heredoc('node -', "const p = '/x/dew_flow_benchmark/y'")), ['harness-name', 'outside-path']);
});

test('The_command_after_an_interpreters_heredoc_is_still_a_command_and_so_is_its_opening_line', () => {
  assert.deepEqual(high(`${heredoc('python3 -', 'print(1)')}\ncat ${HOME}/.ssh/id_rsa`), ['outside-path']);
  assert.deepEqual(high(`cat ${HOME}/.ssh/id_rsa | python3 - <<'EOF'\nprint(1)\nEOF`), ['outside-path']);
});

test('stripData_marks_an_interpreters_body_as_code_and_pathLiterals_reads_its_absolute_literals', () => {
  const { text, data, code } = stripData(`python3 - <<'A'\nx = "/a/b"\nA\ncat <<B\ny\nB\nbash <<C\nls\nC`);
  assert.deepEqual(text.split('\n'), ["python3 - <<'A'", '\u0001DATA0\u0001', 'cat <<B', '\u0001DATA1\u0001', 'bash <<C', 'ls', 'C'], 'the shell body stays in the text');
  assert.deepEqual(data, ['x = "/a/b"', 'y']);
  assert.deepEqual(code, [true, false]);
  assert.deepEqual(pathLiterals(`a = "/home/u/x y"; b = '/'; c = "//d"; d = '/etc/hosts'; e = "/"; f = \`/tpl\``), ['/home/u/x y', '/etc/hosts']);
  assert.deepEqual(pathLiterals('s = "it\'s /x"'), [], 'a quote of the other kind inside a string does not start a literal');
});

// An interpreter or a shell launched through a wrapper (`env`, `timeout`, `nice`, `command`, `exec`, `nohup`, `sudo`) or by its path is the same interpreter or shell: the word is found anywhere on the line, as a command word at the
// end of a path, and the body is classified exactly as for the bare word. A word that is the target of a redirection (`> node`) runs nothing.
const WRAPPED = ['env python3 -', 'env -i node', 'env FOO=1 BAR=2 python3 -', 'env -u X -i python3 -', 'timeout 5 node', 'timeout -k 2 5s node', 'nice -n 5 python3 -', 'nice python3 -', 'command python3 -', 'exec node', 'nohup python3 -',
  'sudo python3 -', 'sudo -u root python3 -', 'stdbuf -oL python3 -', 'time python3 -', '/usr/bin/env python3 -', 'env /usr/bin/python3 -', '/usr/bin/python3 -', '/usr/local/bin/node -', './node_modules/.bin/tsx -'];
const WRAPPED_SHELLS = ['env bash', 'env -i sh', 'timeout 5 bash', 'nice -n 5 zsh', 'command dash', 'exec bash', 'nohup sh', 'sudo bash', '/bin/bash', '/usr/bin/env bash', 'env /bin/sh'];

test('A_wrapped_or_path_named_interpreter_reads_its_body_like_the_bare_one', () => {
  for (const opener of WRAPPED) {
    assert.deepEqual(high(heredoc(opener, 'import os', `print(open('${HOME}/.ssh/id_rsa').read())`)), ['outside-path'], `${opener}: a protected literal`);
    assert.deepEqual(high(heredoc(opener, `require('fs').readFileSync("${HARNESS}/settings.json")`)), ['outside-path'], `${opener}: a harness literal`);
    assert.deepEqual(high(heredoc(opener, "s = x.includes(\"/\")", 'grep -r token /', 'find / -name x', '// a comment /* block */')), [], `${opener}: the TypeScript literal "/" is no path`);
  }
  assert.deepEqual(high(heredoc('env python3 -', "s = x.includes(\"/\")")), []);
});

test('A_wrapped_or_path_named_shell_parses_its_body_as_shell', () => {
  for (const opener of WRAPPED_SHELLS) {
    assert.deepEqual(high(heredoc(opener, `grep -r token ${HOME}`)), ['outside-path'], opener);
    assert.deepEqual(high(heredoc(opener, 'find / -name x')), ['outside-path'], opener);
  }
});

test('A_word_named_like_an_interpreter_that_runs_nothing_leaves_the_body_data', () => {
  for (const command of [`cat > node <<'EOF'\ncat ${HOME}/x\nEOF`, `cat >> src/python <<'EOF'\ncat ${HOME}/x\nEOF`, `cat > /tmp/bash <<'EOF'\ngrep -r token /\nEOF`, `cat > out.txt <<'EOF'\ngrep -r token /\nEOF`]) assert.deepEqual(high(command), [], command);
  assert.deepEqual(high(`cat < /usr/bin/python3 > f <<'EOF'\nx\nEOF`), [], 'a redirection source runs nothing either way');
});
