// A walker launched by a wrapper takes its paths from the pipe: `echo / | xargs find` walks `/`, and the walker's own segment names no path, so the path scan sees nothing. Wrappers: xargs, parallel, env, nice,
// timeout, command, exec, nohup. Walkers: find, tree, du, tar, rsync, zip, rg, fd, ag, and grep with a recursive flag (a flag BEFORE the grep word is the wrapper's: `xargs -r grep -n`).
// The PRODUCER chain of the pipe (the commands up to the first one no pipe feeds) is judged as a walk: `fedWalk` hands back the producers that name paths (they are judged by outside-path as a walk, so `echo /` is high and
// `ls src` is none) and whether the producer cannot be resolved (it reads a file, a variable or a substitution: its output is unknown), which is high. A filter in the chain (sort, head, grep ...) passes the data on and is no producer.

const WRAPPERS = new Set(['xargs', 'parallel', 'env', 'nice', 'timeout', 'command', 'exec', 'nohup']);
const WALKERS = new Set(['find', 'tree', 'du', 'tar', 'rsync', 'zip', 'rg', 'fd', 'ag']);
const GREPS = new Set(['grep', 'egrep', 'fgrep']);
// what lists or names paths from its own words, the tree and the box: its output is known from its arguments
const LISTERS = new Set(['echo', 'printf', 'ls', 'dir', 'find', 'fd', 'tree', 'git', 'rg', 'ag', 'du']);
const FILTERS = new Set(['sort', 'uniq', 'head', 'tail', 'tr', 'cut', 'grep', 'egrep', 'fgrep', 'sed', 'awk', 'wc', 'tee', 'rev', 'paste', 'xargs', 'nl', 'column']);
const RECURSIVE = /^(?:-[A-Za-z]*[rR][A-Za-z]*|--recursive|--dereference-recursive)$/;
const VARIABLE = /[$`]/;
const ASSIGNMENT = /^[A-Za-z_]\w*=/;

const commandOf = step => step.words.find(w => !ASSIGNMENT.test(w)) ?? '';

/** Whether the words launch a walker: the first walker word, or a grep with a recursive flag after it. */
function walkerIn(words) {
  const at = words.findIndex(w => WALKERS.has(w) || GREPS.has(w));
  if (at < 0) return false;
  return WALKERS.has(words[at]) || words.slice(at + 1).some(w => RECURSIVE.test(w));
}

const launchesWalker = step => WRAPPERS.has(commandOf(step)) && walkerIn(step.words.slice(1));
const isLister = step => LISTERS.has(commandOf(step));

/** A command of the chain whose output is not known from its arguments: a lister that names a variable or a substitution, or a command that is neither a lister nor a filter (`cat file`). */
const opaque = step => (isLister(step) ? VARIABLE.test(step.segment) : !FILTERS.has(commandOf(step)));

/** The index of the command that starts the chain feeding step `i`: walk back while a pipe feeds the command. */
function chainStart(steps, i) {
  let k = i - 1;
  while (k > 0 && steps[k].piped) k -= 1;
  return k;
}

const isFedWalker = (steps, i) => i > 0 && steps[i].piped && launchesWalker(steps[i]);
const producerUnknown = chain => !isLister(chain[0]) || chain.some(opaque);

/**
 * Null when step `i` is not a wrapper-launched walker that a pipe feeds; else `{ listers, unresolvable }`: the producer commands that name paths (judge them as a walk) and whether the producer is unknown
 * (it starts with something that is no lister, or any of the chain is `opaque`).
 */
export function fedWalk(steps, i) {
  if (!isFedWalker(steps, i)) return null;
  const chain = steps.slice(chainStart(steps, i), i);
  return { listers: chain.filter(isLister), unresolvable: producerUnknown(chain) };
}
