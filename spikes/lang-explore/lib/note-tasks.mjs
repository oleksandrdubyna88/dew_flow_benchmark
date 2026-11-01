// What the public note says per task, as data: the banner, the predictions recorded BEFORE the run (task 1: research/PLAN_lang_explore_task1.md; 02, 03, 09: research/PLAN_lang_explore_olympiad.md section 4), the closing
// sentence and the differences from v2 that only this task has. Task 1's sentences are the ones its recorded note carries, byte for byte. A task without an entry (a test's toy task) gets a generic text with no
// prediction, never another task's.
const NO_PREDICTION = 'no prediction recorded';
const EVIDENCE = '> **Exploratory, not evidence.**';
const SCOPE = 'This note licenses sentences about THIS run only; it settles nothing about C# against TypeScript (`common.measurement` rule 3).';

/** The ungated-draft differences of the olympiad tasks (plan section 6). */
const DRAFT_DIFFERENCES = Object.freeze([
  'The spec of this task is an ungated draft (v2 freezes gated specs, E1.S2).',
  'The held-out suite is the pilot\'s own (v2\'s is E2.S2\'s, validated jointly with final references).',
]);

const olympiad = ({ cost, wall, accepted, trap, activeModel }) => Object.freeze({ cost, wall, accepted, trap, activeModel });

/** The predictions of the olympiad plan's section 4, per task. */
const PREDICTED = Object.freeze({
  '02': olympiad({ cost: '$0.20 to $0.80', wall: '1 to 5 min', accepted: '3 per stack', trap: '0 to 1 per stack', activeModel: 'C# > TS in most pairs (the build)' }),
  '03': olympiad({ cost: '$0.50 to $3.00', wall: '5 to 25 min', accepted: '1 to 3 per stack', trap: '1 to 3 per stack', activeModel: 'no prediction' }),
  '09': olympiad({ cost: '$1.00 to $6.00', wall: '10 to 45 min', accepted: '0 to 3 per stack', trap: '1 to 3 per stack', activeModel: 'no prediction' }),
});

const noneRecorded = olympiad({ cost: NO_PREDICTION, wall: NO_PREDICTION, accepted: NO_PREDICTION, trap: NO_PREDICTION, activeModel: NO_PREDICTION });

/** The task's predictions (the olympiad table's shape), or null for task 1 (its rows are its own). */
export const predictedOf = task => (task.id === '01' ? null : (PREDICTED[task.id] ?? noneRecorded));

/** The banner: the first line of every note. */
export const bannerOf = task => (task.id === '01'
  ? `${EVIDENCE} Three pairs of the easiest task on a harness that differs from v2's. ${SCOPE}`
  : `${EVIDENCE} Three pairs of olympiad task ${task.noteLabel} (${task.title}, an ungated draft spec) on a harness that differs from v2's. ${SCOPE}`);

/** The closing sentence of "What this does not settle". */
export const notSettledOf = task => (task.id === '01'
  ? 'What it does not settle: anything about C# against TypeScript. Three pairs of the easiest task, on a harness that differs from v2\'s, license sentences about this run only.'
  : 'What it does not settle: anything about C# against TypeScript. Three pairs of one olympiad task, on a harness that differs from v2\'s and over an ungated draft spec, license sentences about this run only.');

/** The differences from v2 only this task has (task 1 has none beyond the common list). */
export const extraDifferencesOf = task => (task.id === '01' ? [] : DRAFT_DIFFERENCES);
