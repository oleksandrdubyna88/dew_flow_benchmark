// The time categories and their priority (parent §10). One place, so the classifier, the span builder and the partition
// cannot disagree about a name. WORK first, then the waits; `unattributed` is its own bucket and is never work.
export const BUILD_TEST = 'build_test';
export const MODEL_API = 'model_api';
export const OTHER_TOOL = 'other_tool';
export const HOOKS = 'hooks';
export const COAI_GATE = 'coai_gate';
export const CONSULTANT = 'consultant';
export const CI_WAIT = 'ci_wait';
export const CODERABBIT_WAIT = 'coderabbit_wait';
export const SLEEP_300 = 'sleep300';
export const UNATTRIBUTED = 'unattributed';

/** Not partition categories: a generic wait awaiting GitHub's intervals, and an Agent/Task launch whose time is its children's. */
export const WAIT = 'wait';
export const CONTAINER = 'container';

export const WORK = Object.freeze([BUILD_TEST, MODEL_API, OTHER_TOOL, HOOKS]);
export const WAITS = Object.freeze([COAI_GATE, CONSULTANT, CI_WAIT, CODERABBIT_WAIT, SLEEP_300]);
/** Highest priority first: an elementary interval takes the first of these active in it, across ALL agents. */
export const PRIORITY = Object.freeze([...WORK, ...WAITS]);
