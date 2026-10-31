// The scoring limits, written once. `perInvocationS` is the time one `../minigit` call may take, in the visible scripts' shim and
// in the held-out runner alike; every consumer takes it from the plan's limits (or from here when nothing gave one).
export const DEFAULT_LIMITS = Object.freeze({ buildS: 600, scriptBaseS: 600, perInvocationS: 10, wallS: 7200 });
