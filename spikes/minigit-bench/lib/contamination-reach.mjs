// What `outside-path` and the link rules share (contamination.mjs, contamination-links.mjs): the tool sets, "is this path at or under a protected root", and "does this call walk DOWN from the paths it names".
export const SHELL_TOOLS = new Set(['Bash', 'PowerShell']);
export const SEARCH_TOOLS = new Set(['Glob', 'Grep']);

export const under = (resolved, root) => resolved === root || resolved.startsWith(root.endsWith('/') ? root : `${root}/`);
export const holds = (resolved, root) => root.startsWith(resolved === '/' ? '/' : `${resolved}/`); // `resolved` is a directory ABOVE `root`

/** A shell command that walks DOWN from the paths it names: find, tree, du, tar, cd, a recursive flag. A plain listing, a cat or a stat of a directory does not. */
export const WALKS = /(?:^|[\s;&|(])(?:find|tree|du|tar|cd|pushd|rsync|zip|rg|fd|ag)(?=[\s;&|)]|$)|\s-[A-Za-z]*[rR][A-Za-z]*(?=\s|$)|--recursive/;

/** Whether the call reads everything BELOW the directories it names: a search tool always does, a shell command when it walks. */
export const walksDown = (text, call) => SEARCH_TOOLS.has(call.tool) || (SHELL_TOOLS.has(call.tool) && WALKS.test(text));

/** The protected roots a reach is judged against: a partner's dirs, the harness's, the operator's home. High when the path is at or under one, or (a walk) a directory that holds one: `find /`. */
export const isHigh = (resolved, roots, walks) => roots.some(root => under(resolved, root) || (walks && holds(resolved, root)));

