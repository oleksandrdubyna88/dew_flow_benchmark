// The value shape every expected failure uses: { ok: true, value } | { ok: false, reason }.
export const ok = value => ({ ok: true, value });
export const fail = (reason, extra = {}) => ({ ok: false, reason, ...extra });
