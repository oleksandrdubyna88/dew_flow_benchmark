// Pure: which files under the mounted conventions are NOT rules (and so must not enter the session context).
export const KEEP_DIRS = Object.freeze(['common', 'csharp', 'typescript', 'rust']);

/** relPaths are relative to the mount, forward slashes. Returns those outside every rule directory. */
export const selectRemovals = relPaths => relPaths.filter(p => !KEEP_DIRS.includes(p.split('/')[0]) || !p.includes('/'));
