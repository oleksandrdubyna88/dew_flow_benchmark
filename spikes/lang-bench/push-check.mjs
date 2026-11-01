#!/usr/bin/env node
// The pre-push check of the language comparison's task repository (parent §4 step 3), and step 1's remote.
//
//   node spikes/lang-bench/push-check.mjs --repo <task repo> --branch <b>     the branch's new commits (git rev-list <b> --not --remotes=origin)
//   node spikes/lang-bench/push-check.mjs --text <file> [--self]              a PR or review body, with the same scans (--self: a body of a bench PR, leak and secret scans only)
//   node spikes/lang-bench/push-check.mjs --self --branch <b> [--repo <dir>]  the bench's own branch (leak and secret scans only)
//   node spikes/lang-bench/push-check.mjs --setup-remote --repo <task repo> [--url <u>]
//
//   node spikes/lang-bench/push-check.mjs --install-hook --repo <task repo> [--force-hook]    the enforced push path (TASK repository only)
//   node spikes/lang-bench/push-check.mjs --uninstall-hook --repo <task repo>                 removes only our own hook (marker line)
//     --install-hook writes <repo>/.git/hooks/pre-push (where git looks: core.hooksPath and worktrees honoured), a small POSIX sh script:
//     for every ref of a push it runs this command with --branch <ref> --tip <the sha git pushes> and refuses the push on any failure, when
//     the bench checkout cannot be found, or for a ref outside refs/heads (a tag: check by hand, then --no-verify); a deleted ref passes.
//     A second install changes nothing; a foreign hook is refused (exit 4) unless --force-hook (it is kept as pre-push.foreign).
//     NEVER installed into the bench repository (it is shared by other sessions; its rule is the manual --self run before a push): a
//     repository holding spikes/lang-bench/push-check.mjs, or whose origin is not the task repository, is refused.
//   --tip <sha> (with --repo --branch): check exactly that commit, as git is about to publish it; the branch moving off it is exit 4.
//
//   a branch check refuses (exit 4) when a refs/remotes/origin/* ref is not what the remote has now (ls-remote): run --setup-remote first.
//
//   options: --aid <file> (repeatable) | --aids-dir <dir>   the private aids, read at run time (default ~/rsd/lang-v2/test-design/)
//            --public <file> (repeatable)  texts that are public already; their six-word runs are not aid hits. Default: the two plans plus the
//                            files listed in inputs/public-texts.json, as published at origin/main of the bench repository (--bench-repo <dir>),
//                            never the working tree; unreadable = exit 3
//            --secret-file <file> (repeatable)  real secret files whose values must appear nowhere (default: ~/.config/lang-v2/github-pat and
//                            deploy_key_lang_v2 when they exist; read at run time, never printed)
//            --tokens <file>  literal leak tokens, one per line, outside git (default: leak-tokens.txt in the aids folder when it is present;
//                            the report prints the token count, 0 when there is none)     --credits <file>  --needles <file>  --template-files <file>
//
// Exit 0 clean · 1 refused · 3 environment (git, the network, an aid that cannot be read) · 4 configuration. Nothing is pushed, ever.
// A line names the commit, the path, the line and the reason - never the text that matched. The report names the tip it checked; publish
// exactly that commit: `git push <url> <tip sha>:refs/heads/<branch>`, so what was checked is what is published.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { checkBranch, checkSelf, checkText, EXIT, formatReport } from './lib/push-check.mjs';
import { runGit } from './lib/push-git.mjs';
import { installHook, uninstallHook } from './lib/push-hook.mjs';
import { loadInputs } from './lib/push-inputs.mjs';
import { setupRemote } from './lib/push-remote.mjs';

const OPTIONS = {
  repo: { type: 'string' }, branch: { type: 'string' }, text: { type: 'string' }, url: { type: 'string' },
  self: { type: 'boolean' }, 'setup-remote': { type: 'boolean' }, tip: { type: 'string' },
  'install-hook': { type: 'boolean' }, 'uninstall-hook': { type: 'boolean' }, 'force-hook': { type: 'boolean' },
  aid: { type: 'string', multiple: true }, 'aids-dir': { type: 'string' }, tokens: { type: 'string' },
  public: { type: 'string', multiple: true }, 'bench-repo': { type: 'string' }, 'origin-url': { type: 'string' }, dest: { type: 'string' }, 'secret-file': { type: 'string', multiple: true }, credits: { type: 'string' }, needles: { type: 'string' }, 'template-files': { type: 'string' },
};

const usage = reason => ({ exit: EXIT.CONFIG, text: `push-check: ${reason}\nusage: see the header of ${import.meta.url.replace('file://', '')}\n` });

function parse(argv) {
  try {
    return { values: parseArgs({ args: argv, options: OPTIONS, strict: true }).values };
  } catch (error) {
    return { error: error.message };
  }
}

// --self alone is the bench's branch check; with --text it only narrows the body check to the bench's rules
const modeOf = v => [v['setup-remote'] ? 'setup-remote' : null, v['install-hook'] ? 'install-hook' : null, v['uninstall-hook'] ? 'uninstall-hook' : null, v.text === undefined ? null : 'text', v.self && v.text === undefined ? 'self' : null, v.branch !== undefined && !v.self ? 'branch' : null].filter(Boolean);

const readOrNull = file => { try { return readFileSync(file, 'utf8'); } catch { return null; } };
/** Progress goes to stderr, one line per phase, so a long check is not silent and stdout stays exactly the report. */
const progress = line => process.stderr.write(`push-check: ${line}\n`);
const report = result => ({ exit: result.exit, text: formatReport(result) });
const NO_MODE = 'give one of --branch, --text, --self or --setup-remote';
const modeProblem = modes => (modes.length === 0 ? NO_MODE : `one mode at a time, not ${modes.join(' and ')}`);

const inputOptions = v => ({
  aid: v.aid ?? [], publicTexts: v.public ?? [], secretFiles: v['secret-file'] ?? [], benchRepo: v['bench-repo'], aidsDir: v['aids-dir'],
  tokens: v.tokens, credits: v.credits, needles: v.needles, templateFiles: v['template-files'],
});

/** Load the inputs, then `use(inputs)`; an input that cannot be loaded ends the run with its kind's exit code. */
async function withInputs(v, use) {
  const inputs = await loadInputs(inputOptions(v));
  if (!inputs.ok) return { exit: inputs.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, text: `push-check: ${inputs.reason}\n` };
  return use(inputs.value);
}

async function selfRepo(v) {
  const top = v.repo === undefined ? await runGit(process.cwd(), ['rev-parse', '--show-toplevel']) : null;
  return v.repo ?? (top.ok ? top.value.stdout.trim() : undefined);
}

async function runSetupRemote(v) {
  if (v.repo === undefined) return usage('--setup-remote needs --repo');
  const r = await setupRemote({ repo: v.repo, url: v.url });
  return { exit: r.exit, text: `${r.lines.join('\n')}\n` };
}

const runText = v => withInputs(v, inputs => {
  const body = readOrNull(v.text);
  return body === null ? usage(`${v.text} cannot be read`) : report(checkText({ text: body, label: v.text, inputs, self: v.self === true }));
});

const runSelf = v => withInputs(v, async inputs => {
  const repo = await selfRepo(v);
  return repo === undefined || v.branch === undefined ? usage('--self needs --branch (and a git repository)') : report(await checkSelf({ repo, branch: v.branch, inputs, progress }));
});

const runBranch = v => withInputs(v, async inputs => (v.repo === undefined ? usage('--branch needs --repo') : report(await checkBranch({ repo: v.repo, branch: v.branch, tip: v.tip, dest: v.dest, inputs, expectedOrigin: v['origin-url'], progress }))));

const SELF_PATH = fileURLToPath(import.meta.url);
/** The options a hook bakes in at install (fixtures, a named aid, ...): each flag given to --install-hook that a check reads. */
const BAKED = ['aid', 'public', 'secret-file', 'aids-dir', 'tokens', 'bench-repo', 'credits', 'needles', 'template-files'];
const bakedArgs = v => BAKED.flatMap(name => [].concat(v[name] ?? []).flatMap(value => [`--${name}`, value]));

async function runInstallHook(v) {
  if (v.repo === undefined) return usage('--install-hook needs --repo');
  const r = await installHook({ repo: v.repo, checker: SELF_PATH, originUrl: v['origin-url'], extraArgs: bakedArgs(v), force: v['force-hook'] === true });
  return { exit: r.exit, text: `${r.lines.join('\n')}\n` };
}

async function runUninstallHook(v) {
  if (v.repo === undefined) return usage('--uninstall-hook needs --repo');
  const r = await uninstallHook({ repo: v.repo });
  return { exit: r.exit, text: `${r.lines.join('\n')}\n` };
}

const MODES = { 'install-hook': runInstallHook, 'uninstall-hook': runUninstallHook, 'setup-remote': runSetupRemote, text: runText, self: runSelf, branch: runBranch };

async function run(v) {
  const modes = modeOf(v);
  return modes.length === 1 ? MODES[modes[0]](v) : usage(modeProblem(modes));
}

const parsed = parse(process.argv.slice(2));
const outcome = parsed.error ? usage(parsed.error) : await run(parsed.values);
process.stdout.write(outcome.text);
process.exitCode = outcome.exit;
