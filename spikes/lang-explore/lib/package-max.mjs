// The largest of a list, by a running maximum: no argument list, so a list of any length is safe (code round 1, C5). `null` for an empty list.
export const maxOf = list => list.reduce((best, v) => (best === null || v > best ? v : best), null);
