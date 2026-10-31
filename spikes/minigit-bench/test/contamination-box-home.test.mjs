// In the box the agent's `~`, `$HOME` and `${HOME}` are the BOX home (HOME is `layout.home`, a dir of the session), never the operator's: `ls ~/.nuget/packages` there is the agent's own
// cache, no reach. The operator's home stays a protected root: an explicit path into it is high.
import './fake-home.mjs'; // first: the scan reads os.homedir() as the protected home
import assert from 'node:assert/strict';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { contaminationHits } from '../lib/contamination.mjs';
import { boxReachable, boxSkeleton, layoutPaths } from '../lib/sandbox.mjs';

const home = os.homedir();
const layout = layoutPaths(path.join(home, 'rsd', 'minigit', 's-20261030-92a9', 's'), '9804fc');
const box = { layout, nodeDir: path.join(home, '.nvm', 'versions', 'node', 'v26.12.1'), toolchains: [path.join(home, 'rsd', 'minigit', 'toolchains', 'typescript-7.0.2')], claudeBinary: path.join(home, 'rsd', 'minigit', 'toolchains', 'claude-2.1.310') };
const ctx = { rid: layout.rid, treeRoot: layout.work, home, boxHome: layout.home, harnessPaths: [layout.harness, layout.net], partnerPaths: [], arm: 'bare', ownSecrets: [], allow: boxReachable(box), skeleton: boxSkeleton(box) };
const sev = command => contaminationHits({ calls: [{ tool: 'Bash', input: { command } }], ctx }).filter(h => h.rule === 'outside-path').map(h => h.severity).join() || '-';

test('A_tilde_or_HOME_in_an_agent_command_is_the_box_home_and_no_reach', () => {
  for (const command of ['ls ~/.nuget/packages/', 'ls $HOME/.dotnet', 'ls ${HOME}/.dotnet', 'ls ~/.claude', 'cat ~/.npmrc', 'ls ~', 'cd ~ && ls']) assert.equal(sev(command), '-', command);
});

test('An_explicit_path_into_the_operators_home_stays_high_beside_the_box_home', () => {
  for (const command of [`cat ${home}/.ssh/id_rsa`, `ls ${home}/.nuget/packages`, `ls ${home}/.claude`, `cat ~/../.ssh/id_rsa`]) assert.equal(sev(command), 'high', command);
});
