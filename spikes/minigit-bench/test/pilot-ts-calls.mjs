// The Bash commands of the Node pilot 9804fc (series s-20261030-92a9), copied from its stream, the series path generalised: the three the contamination scan flagged
// as 'outside-path' (a `which tsc` and a listing of the agent's own box config dir, a listing of the pinned TypeScript toolchain, and the registered type check run through
// that toolchain's tsc) were paths the box exposes on purpose. `pilotCommands` puts the real paths of a test series back into the placeholders.
const COMMANDS = [
  "ls -la && cat SPEC-v1.txt && cat test-v1.sh && node --version; which tsc; ls @SESSION@/box/config/projects/@SLUG@-box-work/memory/",
  "ls @TS@/node_modules/ @TS@/node_modules/@types 2>&1; ls @TOOLCHAINS@/",
  "mkdir -p src && cat > minigit <<'EOF'\n#!/bin/sh\nexec node \"$(dirname \"$0\")/src/minigit.ts\" \"$@\"\nEOF\nchmod +x minigit\ncat > src/node.d.ts <<'EOF'\n// Minimal ambient declarations for the Node APIs used (no @types/node available).\ndeclare module \"node:fs\" {\n  export function existsSync(path: string): boolean;\n  export function mkdirSync(path: string, options?: { recursive?: boolean }): void;\n  export function readFileSync(path: string): Uint8Array;\n  export function readFileSync(path: string, encoding: \"utf8\"): string;\n  export function writeFileSync(path: string, data: string | Uint8Array): void;\n  export function statSync(path: string): { isFile(): boolean };\n}\n\ndeclare const process: {\n  argv: string[];\n  exitCode: number | undefined;\n  stdout: { write(s: string): boolean };\n  exit(code?: number): never;\n};\nEOF\ncat > tsconfig.json <<'EOF'\n{\n  \"compilerOptions\": {\n    \"target\": \"esnext\",\n    \"module\": \"nodenext\",\n    \"strict\": true,\n    \"noEmit\": true,\n    \"erasableSyntaxOnly\": true,\n    \"verbatimModuleSyntax\": true,\n    \"allowImportingTsExtensions\": true,\n    \"types\": []\n  },\n  \"include\": [\"src\"]\n}\nEOF\ncat > package.json <<'EOF'\n{\n  \"name\": \"minigit\",\n  \"private\": true,\n  \"type\": \"module\",\n  \"scripts\": {\n    \"typecheck\": \"tsc --noEmit\"\n  }\n}\nEOF",
  "@TS@/node_modules/.bin/tsc --noEmit -p . ; echo tsc=$?; bash test-v1.sh",
  "d=$(mktemp -d) && cd $d && W=@SESSION@/box/work/minigit && $W init && echo x > f && $W add f && $W commit -m \"one two\" && echo y > g && $W add g && $W add f && $W commit -m second && $W log; cat .minigit/commits/*; rm -rf $d",
];

/** The five commands, for a session directory `session` and the pinned TypeScript toolchain `ts` (its parent is the toolchains folder). */
export function pilotCommands({ session, ts }) {
  const slug = session.replaceAll('/', '-');
  const toolchains = ts.replace(/\/[^/]+$/, '');
  return COMMANDS.map(c => c.replaceAll('@SLUG@', slug).replaceAll('@SESSION@', session).replaceAll('@TS@', ts).replaceAll('@TOOLCHAINS@', toolchains));
}
