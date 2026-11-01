#!/usr/bin/env node
// The pre-push check of the language comparison's task repository (parent §4 step 3), and step 1's remote.
//
//   node spikes/lang-bench/push-check.mjs --repo <task repo> --branch <b>     the branch's new commits (git rev-list <b> --not --remotes=origin)
//   node spikes/lang-bench/push-check.mjs --text <file> [--self]              a PR or review body, with the same scans (--self: a body of a bench PR, leak and secret scans only)
//   node spikes/lang-bench/push-check.mjs --self --branch <b> [--repo <dir>]  the bench's own branch (leak and secret scans only)
//   node spikes/lang-bench/push-check.mjs --setup-remote --repo <task repo> [--url <u>]
//
//   options: --aid <file> (repeatable) | --aids-dir <dir>   the private aids, read at run time (default ~/rsd/lang-v2/test-design/)
//            --public <file> (repeatable)  texts that are public already; their six-word runs are not aid hits. Default: the two plans as
//                            published at origin/main of the bench repository (--bench-repo <dir>), never the working tree; unreadable = exit 3
//            --secret-file <file> (repeatable)  real secret files whose values must appear nowhere (default: ~/.config/lang-v2/github-pat and
//                            deploy_key_lang_v2 when they exist; read at run time, never printed)
//            --tokens <file>  literal leak tokens, one per line, outside git (default: leak-tokens.txt in the aids folder when it is present;
//                            the report prints the token count, 0 when there is none)     --credits <file>  --needles <file>  --template-files <file>
//
// Exit 0 clean · 1 refused · 3 environment (git, the network, an aid that cannot be read) · 4 configuration. Nothing is pushed, ever.
// A line names the commit, the path, the line and the reason - never the text that matched. The report names the tip it checked; publish
// exactly that commit: `git push <url> <tip sha>:refs/heads/<branch>`, so what was checked is what is published.
import { readFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { checkBranch, checkSelf, checkText, EXIT, formatReport } from './lib/push-check.mjs';
import { runGit } from './lib/push-git.mjs';
import { loadInputs } from './lib/push-inputs.mjs';
import { setupRemote } from './lib/push-remote.mjs';

const OPTIONS = {
  repo: { type: 'string' }, branch: { type: 'string' }, text: { type: 'string' }, url: { type: 'string' },
  self: { type: 'boolean' }, 'setup-remote': { type: 'boolean' },
  aid: { type: 'string', multiple: true }, 'aids-dir': { type: 'string' }, tokens: { type: 'string' },
  public: { type: 'string', multiple: true }, 'bench-repo': { type: 'string' }, 'origin-url': { type: 'string' }, 'secret-file': { type: 'string', multiple: true }, credits: { type: 'string' }, needles: { type: 'string' }, 'template-files': { type: 'string' },
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
const modeOf = v => [v['setup-remote'] ? 'setup-remote' : null, v.text === undefined ? null : 'text', v.self && v.text === undefined ? 'self' : null, v.branch !== undefined && !v.self ? 'branch' : null].filter(Boolean);

async function selfRepo(v) {
  if (v.repo !== undefined) return v.repo;
  const top = await runGit(process.cwd(), ['rev-parse', '--show-toplevel']);
  return top.ok ? top.value.stdout.trim() : undefined;
}

async function run(v) {
  const modes = modeOf(v);
  if (modes.length !== 1) return usage(modes.length === 0 ? 'give one of --branch, --text, --self or --setup-remote' : `one mode at a time, not ${modes.join(' and ')}`);
  if (modes[0] === 'setup-remote') {
    if (v.repo === undefined) return usage('--setup-remote needs --repo');
    const r = await setupRemote({ repo: v.repo, ...(v.url === undefined ? {} : { url: v.url }) });
    return { exit: r.exit, text: `${r.lines.join('\n')}\n` };
  }
  const inputs = await loadInputs({ aid: v.aid ?? [], publicTexts: v.public ?? [], benchRepo: v['bench-repo'], secretFiles: v['secret-file'] ?? [], aidsDir: v['aids-dir'], tokens: v.tokens, credits: v.credits, needles: v.needles, templateFiles: v['template-files'] });
  if (!inputs.ok) return { exit: inputs.kind === 'config' ? EXIT.CONFIG : EXIT.ENVIRONMENT, text: `push-check: ${inputs.reason}\n` };
  return runChecks(modes[0], v, inputs.value);
}

async function runChecks(mode, v, inputs) {
  if (mode === 'text') {
    const body = readOrNull(v.text);
    return body === null ? usage(`${v.text} cannot be read`) : report(checkText({ text: body, label: v.text, inputs, self: v.self === true }));
  }
  const repo = mode === 'self' ? await selfRepo(v) : v.repo;
  if (repo === undefined || v.branch === undefined) return usage(mode === 'self' ? '--self needs --branch (and a git repository)' : '--branch needs --repo');
  return report(await (mode === 'self' ? checkSelf : checkBranch)({ repo, branch: v.branch, inputs, ...(v['origin-url'] === undefined ? {} : { expectedOrigin: v['origin-url'] }) }));
}

const readOrNull = file => { try { return readFileSync(file, 'utf8'); } catch { return null; } };
const report = result => ({ exit: result.exit, text: formatReport(result) });

const parsed = parse(process.argv.slice(2));
const outcome = parsed.error ? usage(parsed.error) : await run(parsed.values);
process.stdout.write(outcome.text);
process.exitCode = outcome.exit;
