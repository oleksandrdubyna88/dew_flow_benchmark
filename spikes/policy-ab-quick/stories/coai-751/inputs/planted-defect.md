# Planted defect — calibrating the codex reader on coai #751 (before any cell)

**One defect, one requirement, a timing one.** Activation *awaits* the side stamp. Statement 2.2 says the stamp is
*"never awaited by activation"*; the planted copy keeps every word of S's comment that says so and awaits it anyway.

## The edit (apply to a copy of the reference = P + S's diff; nothing else changes)

File: `src_vs_code/src/extension.ts` (line numbers as at S `3ebf208c`).

**Edit 1 — line 107, the signature.**

Before:

```ts
export function activate(context: vscode.ExtensionContext): CoaiExports {
```

After:

```ts
export async function activate(context: vscode.ExtensionContext): Promise<CoaiExports> {
```

**Edit 2 — line 390, the stamp. Only the first word changes; the three comment lines above it (387-389, "never awaited,
so a slow store never delays a window's start") stay exactly as they are.**

Before:

```ts
  void stampThisSide(context.globalState, sideHereOf(thisSide(context.globalStorageUri), process.platform), Date.now(), (error) => {
```

After:

```ts
  await stampThisSide(context.globalState, sideHereOf(thisSide(context.globalStorageUri), process.platform), Date.now(), (error) => {
```

Nothing else: line 792 `return { sidesSeen: … };` is unchanged (an async function returns it as the promise's value).

## Why it is realistic

"Make sure the record has landed before the page first renders" is exactly the reason an author would write `await`
here, and the edit is two tokens. It compiles (TS 6.0.3, strict), lints (`no-floating-promises` is *happier* with an
`await` than with a `void`), and VS Code accepts a promise-returning `activate`.

## What it breaks

All 28 `registerCommand` calls, the webview provider (`registerWebviewViewProvider`, line 566) and every listener in
`activate` come **after** line 390. With the `await`, none of them is registered until `stampThisSide` has finished its
`globalState.update` of this side's record **and** one sequential `update(key, undefined)` per pruned record — so a slow or
locked state store delays every command of the extension and its panel, and VS Code reports activation as unfinished
until the store answers. That is precisely what 2.2's "never awaited by activation" rules out.

## Why every test stays green (verify on the planted copy before using it)

- Hold-outs (`configTransfer.test.ts`, `panelView.test.ts`): untouched code paths — pass.
- S's own unit tests drive `stampThisSide` / `stampSide` directly, never `activate` — pass.
- The `test:host` scenario does `await extension.activate()` and reads the record — still passes (it is not run anyway).
- Wiring tests that read `extension.ts` as text (e.g. `chatWiring.test.ts` "the side is bound before any command can be
  invoked") check ordering of other statements — unaffected.
- `tsc -p ./ --noEmit` and `eslint src`: clean (confirm; if lint objects, report it — do not change the defect).

Expected own-suite result: identical to the unplanted reference.

## What a correct reading must mark

| R-item | Expected | Why |
|---|---|---|
| **R6** (never awaited — timing) | **not_met** | `await stampThisSide(…)` on activation's path; `activate` became `async` and is now sequenced behind the store. The checklist's own words: *"an `await` of the stamp anywhere on activation's path is not_met."* |
| R5 (activation stamps its own side) | met | Still stamps this side through `stampSide`, under `coai.sideSeen@<sideKey>`. Marking R5 down is a reader error. |
| R14 (process evidence) | unchanged from the reference's reading | The reference's commit messages are not edited; a reader that marks R14 down because of this edit is reading the wrong item. |

A reader that marks R6 `met` — e.g. on the strength of the unchanged comment "never awaited" — **fails calibration**:
it read the comment, not the code. `partial` on R6 is also a miss (the requirement is binary: awaited or not).
Every other R-item should read as it reads on the unplanted reference; a difference elsewhere is reader noise to record.

## Instructions for the Sonnet coder applying it

Copy the reference tree; apply exactly the two replacements above with an exact-string edit (each `before` occurs once
in the file); do not touch the comments, the return statement, any test or any other file; run `typecheck`, `lint` and
`testAll` from `commands.json` and report their results; record the sha256 of the planted `extension.ts`. Do not commit
in the story's clone.
