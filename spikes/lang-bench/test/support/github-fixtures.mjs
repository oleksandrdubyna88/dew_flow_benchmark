// A fake GitHub for F2.S2 (b)'s tests: the `runBounded` seam of lib/github-lang.mjs answered from memory. It speaks only the four calls the library makes (list, read by id, create, read the
// repository) and records every call, so a test can say what was asked, in what order, and that nothing but a fake ever answered. No test of this story touches the network.
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';

/** Every call any fake of this process received, for the one test that proves the whole file reached nothing real. */
export const ALL_CALLS = [];

const ADDED = Object.freeze({ source_type: 'Repository', source: 'o/r', current_user_can_bypass: 'always', created_at: '2026-11-05T10:00:00.000+01:00', updated_at: '2026-11-05T10:00:00.000+01:00' });

/** A ruleset as GitHub returns it: the stated data plus the fields GitHub adds. */
export const asGitHub = (ruleset, id) => ({ id, ...structuredClone(ruleset), ...ADDED, node_id: `RRS_${id}`, _links: { self: { href: `https://api.github.com/repos/o/r/rulesets/${id}` } } });

const result = (stdout, extra = {}) => ok({ code: 0, signal: null, stdout: JSON.stringify(stdout), stderr: '', timedOut: false, truncated: false, ms: 1, ...extra });
const methodOf = args => (args.includes('--method') ? args[args.indexOf('--method') + 1] : 'GET');
const pathOf = args => args.find(arg => arg.startsWith('repos/'));

/**
 * `{ run, calls, state }`. `state.rulesets` holds the rulesets as GitHub returns them; `state.repository` the merge settings. `options.onCreate(body, id)` returns what is STORED for a create
 * (default: the body as sent); `options.reply(call)` may answer a call itself (return undefined to leave it to the fake).
 */
export function fakeGitHub({ rulesets = [], repository = { allow_merge_commit: false, allow_squash_merge: true, allow_rebase_merge: true } } = {}, options = {}) {
  const state = { rulesets: [...rulesets], repository, nextId: 1000 };
  const calls = [];
  const create = call => {
    const body = JSON.parse(call.input);
    const id = state.nextId++;
    state.rulesets.push(asGitHub(options.onCreate ? options.onCreate(body, id) : body, id));
    return result({ id, name: body.name });
  };
  const answer = call => {
    const [path] = pathOf(call.args).split('?');
    const segments = path.split('/');
    if (call.method === 'POST') return create(call);
    if (segments.length === 3) return result(state.repository);
    if (segments.length === 4) return result(state.rulesets.map(({ id, name, target, enforcement }) => ({ id, name, target, enforcement })));
    const found = state.rulesets.find(entry => String(entry.id) === segments[4]);
    return found ? result(found) : ok({ code: 1, signal: null, stdout: '', stderr: 'gh: Not Found (HTTP 404)', timedOut: false, truncated: false, ms: 1 });
  };
  const run = async (exe, args, opts = {}) => {
    const call = { exe, args, method: methodOf(args), input: opts.input, env: opts.env, timeoutMs: opts.timeoutMs };
    calls.push(call);
    ALL_CALLS.push(call);
    return options.reply?.(call) ?? answer(call);
  };
  return { run, calls, state, writes: () => calls.filter(call => call.method !== 'GET') };
}

/** A runner whose every call ends the same way, for the failure cases. */
export function brokenRunner(kind, extra = {}) {
  const calls = [];
  const answers = {
    launch: () => fail('could not start gh: spawn gh ENOENT'),
    timeout: () => ok({ code: null, signal: 'SIGKILL', stdout: '', stderr: '', timedOut: true, truncated: false, ms: 60000 }),
    exit: () => ok({ code: 1, signal: null, stdout: '', stderr: extra.stderr ?? 'gh: HTTP 500', timedOut: false, truncated: false, ms: 1 }),
    truncated: () => ok({ code: 0, signal: null, stdout: '[', stderr: '', timedOut: false, truncated: true, ms: 1 }),
    garbage: () => ok({ code: 0, signal: null, stdout: '<html>not json', stderr: '', timedOut: false, truncated: false, ms: 1 }),
  };
  const run = async (exe, args, opts = {}) => {
    const call = { exe, args, method: methodOf(args), input: opts.input, env: opts.env };
    calls.push(call);
    ALL_CALLS.push(call);
    return answers[kind]();
  };
  return { run, calls };
}

/** One lines collector for `say`. */
export const collector = () => { const lines = []; return { lines, say: line => lines.push(line) }; };
