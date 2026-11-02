// The one place the pilot reads the wall clock for a time it persists (coai finding 0): everything that writes a timestamp or a date (the calibration's `made`, the pause point's `at`, a series' `createdAt`, the note's date, the
// rehearsal record) takes the clock as an argument and defaults to this, so a test injects a fixed one and no other module calls `new Date()`. (`prepare.mjs`, the network step, keeps its own.)
export const systemClock = () => new Date();

/** The day (`yyyy-mm-dd`) the clock gives: what a note is dated by. */
export const dayOf = clock => clock().toISOString().slice(0, 10);
