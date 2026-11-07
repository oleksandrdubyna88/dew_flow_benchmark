// The frozen list of a quick-run submission and the folders a copy of it leaves out: ONE source (F1.S3 (b); parent D65). The workspace builder cuts the frozen subset from it, the clean copy for scoring reads the folder list from it, and the
// template test and the stack comparison read the same names. It is its own file so that the contract's identity (`contract-identity.mjs`) does not move when the statement's sentences change.
// Frozen are the scripts, the stack's pin files, `STATEMENT.md`, `PACKAGES.md` and everything under `samples/` and `runner/`. The project file (`App.csproj`), `package.json` and `package-lock.json` are the agent's to edit: it may add a listed package.
export { excludedDirs } from '../../minigit-bench/lib/score-deps.mjs';

export const SCRIPTS = Object.freeze(['build.sh', 'run.sh']);

/** The files the agent was told are frozen, besides the scripts: the stack's pin files. */
export const PIN_FILES = Object.freeze({
  csharp: Object.freeze(['global.json', 'nuget.config', 'Directory.Build.props']),
  node: Object.freeze(['.npmrc', 'tsconfig.json', 'eslint.config.mjs']),
});

/** The files of a workspace that are derived (written from the record and the spec, never from a template). */
export const DERIVED_FILES = Object.freeze(['STATEMENT.md', 'PACKAGES.md']);

/** The folders whose every file is frozen. */
export const FROZEN_FOLDERS = Object.freeze(['samples/', 'runner/']);

/** The template files a stack's agent may edit (not frozen). */
export const AGENT_EDITABLE = Object.freeze({ csharp: Object.freeze(['App.csproj']), node: Object.freeze(['package-lock.json', 'package.json']) });

/** Every entry of the list, for both stacks: file names and folders. */
export const frozenEntries = () => [...SCRIPTS, ...PIN_FILES.csharp, ...PIN_FILES.node, ...DERIVED_FILES, ...FROZEN_FOLDERS];

/** The named files of a stack that are frozen. */
export const frozenNames = stack => [...SCRIPTS, ...PIN_FILES[stack], ...DERIVED_FILES];

/** The template files of a stack that the harness owns: the scripts and the pin files. A moved template moves exactly these. */
export const templateFilesOf = stack => [...SCRIPTS, ...PIN_FILES[stack]];

/** True when a workspace path is frozen for the stack. */
export const isFrozen = (rel, stack) => frozenNames(stack).includes(rel) || FROZEN_FOLDERS.some(folder => rel.startsWith(folder));

/** The frozen subset of a file map `{ rel: file }`. */
export const frozenOf = (files, stack) => Object.fromEntries(Object.entries(files).filter(([rel]) => isFrozen(rel, stack)));
