// Pure: deep-replace machine paths in every string of a JSON value.
export function scrub(value, replacements) {
  if (typeof value === 'string') return replacements.reduce((t, [from, to]) => (from ? t.split(from).join(to) : t), value);
  if (Array.isArray(value)) return value.map(v => scrub(v, replacements));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, scrub(v, replacements)]));
  return value;
}

/** Slash variants of a path, longest first, for use as replacement keys. */
export function pathVariants(p) {
  const fwd = p.replace(/\\/g, '/');
  return [...new Set([p, fwd, fwd.replace(/\//g, '\\'), fwd.replace(/\//g, '\\\\')])];
}
