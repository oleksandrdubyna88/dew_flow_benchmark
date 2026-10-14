// Pure: deep-replace machine paths in every string of a JSON value.
export function scrub(value, replacements) {
  if (typeof value === 'string') return replacements.reduce((t, [from, to]) => (from ? t.split(from).join(to) : t), value);
  if (Array.isArray(value)) return value.map(v => scrub(v, replacements));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, scrub(v, replacements)]));
  return value;
}

/** Apply `fn` to every string in a JSON value. */
export function scrubStrings(value, fn) {
  if (typeof value === 'string') return fn(value);
  if (Array.isArray(value)) return value.map(v => scrubStrings(v, fn));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, scrubStrings(v, fn)]));
  return value;
}

const PROFILE_PATTERNS = [/[A-Za-z]:(?:\\{1,2}|\/)Users(?:\\{1,2}|\/)[^\\/\s"']+/g, /\/[a-zA-Z]\/Users\/[^/\s"']+/g];

/** A string scrubber for committed files: runs root -> <runs>, creds clone -> <creds>, any user profile -> ~. */
export function machineScrubber({ runsRoot, creds }) {
  const withBash = p => [...pathVariants(p), p.replace(/\\/g, '/').replace(/^([A-Za-z]):/, (_, d) => `/${d.toLowerCase()}`)];
  const pairs = [...withBash(runsRoot).map(v => [v, '<runs>']), ...withBash(creds).map(v => [v, '<creds>'])];
  return text => {
    const mapped = pairs.reduce((t, [from, to]) => t.split(from).join(to), text);
    return PROFILE_PATTERNS.reduce((t, re) => t.replace(re, '~'), mapped);
  };
}

/** Slash variants of a path, longest first, for use as replacement keys. */
export function pathVariants(p) {
  const fwd = p.replace(/\\/g, '/');
  return [...new Set([p, fwd, fwd.replace(/\//g, '\\'), fwd.replace(/\//g, '\\\\')])];
}
