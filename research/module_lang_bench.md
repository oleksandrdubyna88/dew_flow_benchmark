# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").

## The runner (E1.S8a)

**Purpose.** `runner/` is the one Python standard-library program the agent, CI, the pre-freeze gate and E2's held-out scoring share (D37). It
reads a folder of `<id>.in` / `<id>.out` pairs, builds with `bash build.sh`, runs `bash run.sh` per case and reports `PASS` / `FAIL` with the
first mismatch under FORMAT v2's rules (grammar 2). It knows nothing of boxes, scopes or namespaces: the execution layer hands it a launch prefix.
**Built so far: part i** (olympiad runs, parsing and validation of daemon and web samples, the matcher, the report, the pin). **Part ii** (process
control by host pid, sockets, the daemon and web runs, signals, the cgroup path) is not built; a daemon or web run ends with exit 5 and says so.

**Files** (`spikes/lang-bench/`):

| File | Role |
|---|---|
| `runner/run_samples.py` | the entry: arguments, kind, validation of every pair, build, olympiad cases, report, exit codes |
| `runner/rs_lex.py` | lines, tokens, durations, strings and escapes, variables, normalised fields; problems are `(file, line, message)` |
| `runner/rs_model.py`, `rs_script.py`, `rs_check.py` | the `.in` parser into frozen dataclasses, its validation, the entries a script produces |
| `runner/rs_transcript.py`, `rs_pair.py`, `rs_assign.py` | the `.out` parser, script/transcript correspondence, one-to-one assignment |
| `runner/rs_json.py`, `rs_fields.py`, `rs_jmatch.py`, `rs_match.py` | exact-decimal JSON, fields and the backtracking matcher, JSON bodies with `$unordered`, entries and V6 checks |
| `runner/rs_exec.py`, `rs_olympiad.py` | the build, the launch argv, the pidfd wait, the lifecycle seam; the streaming olympiad case |
| `runner/rs_samples.py`, `rs_report.py` | pairs, `cases.txt`, limits; report lines and the JSON result |
| `pin-runner.mjs`, `lib/runner-pin.mjs` | the pin: writes `inputs/specs.d/runner.json` (`runner: { version, files, manifestSha256 }`), `--check` |
| `specs-record.mjs`, `lib/specs-record.mjs` | assembles `inputs/specs.json` from the writers' files in `inputs/specs.d/`; `--check`; never edit `specs.json` by hand |

**CLI.** `python3 -I runner/run_samples.py [--workspace DIR] [--kind olympiad|daemon|web] [--no-build] [--limits FILE] [--launch-prefix JSON]
[--json OUT] [--only ID] [--parse-only] SAMPLES_DIR`. The kind is read from the `Kind` row of the workspace's `STATEMENT.md` unless `--kind` is given.
`--limits` is a JSON object with `timeS`, `readyS`, `graceS` (exact seconds, from 1 to 600; unknown keys refused); without it the olympiad limit is the
floor (1 s) and every time verdict says `against the floor`. `--launch-prefix` is a JSON argv put before `bash run.sh`, `{case}` replaced.
`--parse-only` parses and validates every pair and starts nothing. A `cases.txt` beside the pairs attaches its R-ids to each case. Part ii's
`--cgroup-of`, `--launch-status` and `--case-view` are **refused with exit 4** until they exist, never ignored. Without a cgroup every case says
`memory: not certified`.

**Exit codes.** `0` every case passed (or `--parse-only` found no problem) · `1` a case failed, a failing build counts · `3` the environment (no
workspace, no `bash`) · `4` configuration: usage, a malformed sample (file and line), a version 1 file, a script/transcript mismatch, a bad limits
file or `cases.txt` · `5` the runner itself failed (a traceback, or a daemon/web run before part ii).

**Result format.** `--json OUT` writes `{ format: 1, runner, grammar: 2, kind, build, cases: [{ id, status, reason, rIds, entries, firstMismatch,
lifecycle: { stop, leftover, leftoverEvidence, memory, stderr }, wallMs }], python }`; what was not measured is `{ known: false, why }`, never zero.

**Bounds.** An olympiad case's stdout is compared as a stream and stops at the first difference; at most 16 MiB of it is kept for the report, and a
truncated capture is never what decides a `PASS`; stderr goes to a file (killed past 256 MiB); a case is cut off at its limit and the group is
killed; a wall time over the limit is `time limit` even when the program exited by itself; the build has a 600 s ceiling; a transcript's
`unordered` group holds 2 to 64 entries; a regex or field match is cut off after 500,000 steps; a string expression expands to at most 200 parts.

**Encoding (B10).** `python3 -I` ignores `PYTHONIOENCODING` and `PYTHONUTF8`, so the entry reconfigures stdout and stderr to UTF-8 with
`backslashreplace`; every file is read as bytes; a file name that is not UTF-8 is printed as `\xHH`; an invalid-UTF-8 JSON body is the verdict `bad-utf8`.

**Seams for part ii.** `rs_exec.PlainLifecycle` (`memory`, `kill`, `leftover`) is what a scope-based lifecycle replaces through `Config.lifecycle`; the
pidfd wait is `rs_exec.wait_exit`; `run_samples._after_parsing` is where daemon and web dispatch; `rs_transcript.Actual` and `rs_match.compare` take the
observations part ii records; `Actual.step_line` and `step_text` feed the report's `step` line; `rs_script.Step` carries what each step needs.
The tests are in [module_tests.md](module_tests.md), section "The language-comparison runner scenarios".

## The execution layer (E1.S8b)

not built yet

## FORMAT v2 (E1.S9)

not built yet

## The conformance check (E1.S8)

not built yet

## Toolchains and the preflight (E1.S5)

Built 2026-11-01 (`todo/PLAN_lang_comparison_v2_E1.md` § E1.S5). The gate, the references and every later session run on the toolchains below, from
read-only directories under the private store `~/rsd/lang-v2/toolchains/` (outside every git tree; the store root is refused inside a worktree). An unattended
`apt upgrade` cannot move them: v1's SDK pin was moved that way on 2026-10-30.

| Toolchain | Pin | Directory in the store | Official source | Checksum kept in `inputs/stacks-lang.json` |
|---|---|---|---|---|
| .NET SDK (bundles runtime 10.0.14) | 10.0.403 | `dotnet-sdk-10.0.403/` | `dotnet-install.sh` pinned at commit `5147e32300a8e908f5d737c8cff63a76b4b63531` of `dotnet/install-scripts` (`https://raw.githubusercontent.com/dotnet/install-scripts/<commit>/src/dotnet-install.sh`, the file `https://dot.net/v1/dotnet-install.sh` served on 2026-11-01; its sha256 is recorded and checked before it runs, and a moving URL is refused by the loader); the archive it downloads, `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz` | sha512, equal to the hash in `https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json` |
| Node | 26.13.0 | `node-v26.13.0-linux-x64/` | `https://nodejs.org/dist/v26.13.0/node-v26.13.0-linux-x64.tar.xz` | sha256, equal to its line in `https://nodejs.org/dist/v26.13.0/SHASUMS256.txt` |
| TypeScript | 6.0.3 (newest 6.0 patch on npm, read 2026-11-01; 7.0.2 is superseded, parent D6) | `typescript-6.0.3/` (`node_modules/.bin/tsc`) | `npm install --prefix` run by the pinned Node against `https://registry.npmjs.org/` | `dist.integrity` (sha512, base64) of the packument, equal to the integrity in the installed `node_modules/.package-lock.json` |
| Python 3 | 3.14.4, the distribution's | `/usr/bin/python3` (not copied) | the host | sha256 of the real file, `52e0a13e…cb41` |

**Layout and permissions.** The store root is mode 0755 (the installer writes into it); each toolchain directory is sealed `chmod -R a-w` (`dr-xr-xr-x` directories,
`-r--r--r--` or `-r-xr-xr-x` files), so `find <dir> -perm /222 ! -type l` lists nothing. The SDK directory holds `sdk/10.0.403` only and `shared/*/10.0.14` only. Each
directory's tree hash (v1's `treeHash`: type, path, mode without write bits, content hash; never mtimes) is recorded in `inputs/stacks-lang.json` under `treeSha256`.

**Install.** `node spikes/lang-bench/install-toolchains.mjs [--only dotnetSdk,node,typescript] [--record]`. Per toolchain: the official source is read first and must list the
checksum the pin holds (otherwise nothing is downloaded and the refusal names both values); the artefact is downloaded with `curl -fsSL --proto =https` into an owned
folder and checked against the pin (Node sha256; the SDK archive that `dotnet-install.sh --keep-zip` downloaded, sha512; the integrity in npm's lock); the tree is built in
an owned staging folder beside its final place (`testsToDelete-u<uid>-bench-stage-…`), sealed, and only then renamed into the store, so a half-written directory is never
registered. The TypeScript install starts from a fixed `package.json` (`lang-v2-toolchain`), because npm otherwise names the project after the random staging folder in `package-lock.json` and `node_modules/.package-lock.json`, which made the tree hash differ on every install; the one recorded hash that was ever corrected: `typescript-6.0.3` was first recorded (commit `a2628cf4`) as `36dbb26c8a1afe6edbd919d6f121f8d7579e36d07d14f5a8eed4cf21062b7649`, a hash that had the random staging folder's name (`testsToDelete-u1000-bench-stage-typescript-jXe53n`) inside `package-lock.json` and could not be reproduced by any other install; after the fixed manifest it was replaced, by hand and once (commit `916d5979`), by `31b71ea9cbabcce7cf1b7936368be664b1f7e21840f02c0c11c59fffef1a8c6b`, which a second install under a different staging name and umask 077 reproduced. The package bytes were never in question: the install's own integrity check (the pinned `dist.integrity`) passed both times, and Node's and the SDK's recorded hashes never changed. Every install child (tar, `dotnet-install.sh`, npm) is started through `/bin/sh -c 'umask 022 && exec "$@"' sh <exe> <argv>` (`underInstallUmask` in `lib/toolchain-io.mjs`; the argv travels as arguments, never in the shell string), and the manifest the install writes itself is chmod 644. Library code never calls `process.umask` (a scan test pins that); only the command `install-toolchains.mjs` sets 022 for itself, once, before any async work. This is needed because the tree hash covers read and execute bits and the same archive extracted under 077 would hash differently. When `stacks-lang.json` already records a tree hash for the toolchain, the staged tree is hashed and compared BEFORE it is sealed and renamed: a difference discards the tree and fails naming both hashes, so a tree that is not the recorded one is never registered. An install that was killed leaves its staging folder (possibly sealed) in the store root; the next install first collects this user's `testsToDelete-u<uid>-bench-stage-*` folders whose owner record says the process is gone (`lib/toolchain-stale.mjs`: `lstat` first, only a real directory directly in the store root is touched - a symlink named like a staging folder is never followed; unseal, remove, name each on stderr), and leaves a live owner's, an unmarked and anyone else's folder alone. The tree is hashed before it is sealed and sealed only just before the rename. A directory already in the store is left alone (`present`). Each step prints one line to stderr before it starts, `<toolchain>: <step>` (checking the official source, downloading, verifying, extracting or installing, hashing, sealing, and recording for `--record`), so a long SDK install is not silent; stdout stays one JSON line per toolchain. Never apt, never sudo. `--record` writes the hashes of what the run installed; a recorded hash is
never moved. `inputs/stacks-lang.json` is `JSON.stringify(json, null, 2)` plus a newline and `--record` rewrites it by parse, set, stringify (no text patching); a test pins that the committed file round-trips byte for byte.

**Verify.** `node spikes/lang-bench/install-toolchains.mjs --verify [--deep]`: every directory present and sealed, the SDK directory holding only the pinned SDK, with
`--deep` every tree hash equal to the record. Exit 1 names the problem; exit 2 is usage or a store inside a git worktree.

**Preflight.** `lib/preflight-lang.mjs`, `preflightLang({ stacks, storeRoot, boxPath? })`. It first reads the store back deep (`verifyStore({ deep: true })`, about 1.2 s: sealed, one SDK, every tree hash equal to the record), because a binary replaced in place still answers `--version` with the pin; a toolchain with no recorded hash is refused. The probes run inside a bwrap box built from v1's primitives (root read-only, home /mnt
/run /tmp hidden, the distribution's dotnet hidden and `DOTNET_ROOT` set, the three directories bound back read-only, no network, pid namespace, cleared environment); S8b's `box.mjs` replaces it once it exists. It reads back
`node --version`, `dotnet --list-sdks`, `dotnet --list-runtimes`, `tsc --version`, the sha256 of the pinned `python.path`'s real file (the path is written once, in `stacks-lang.json`; the probe argv appends it), and `locale -a`, and refuses, naming the pin and
the value seen: another Node patch, a second SDK or the wrong one, another runtime, a TypeScript outside 6.0.x or another 6.0 patch, a changed Python binary, and a
missing `de_DE.utf8` (with `sudo locale-gen de_DE.UTF-8`, the remedy derived from the pinned locale name). It also refuses a box PATH that reaches a second `dotnet`. That scan runs INSIDE the box (`which -a dotnet` under the box's environment, then `readlink -f` of each hit and of the pinned one, in the box), so a distribution SDK the box hides is not a second dotnet, a PATH entry that exists only on the host is not counted, and one real file reached through two names is one dotnet. The probes' executables are looked up on the box's PATH by bwrap, not on the host's. Every probe executable is resolved INSIDE the box before it runs (`resolveProbeExecutables`: `which node|tsc|dotnet`, then `readlink -f` of the hit and of the pinned file in the box); one that resolves to another real file than the pinned one (a fake `node` that prints `v26.13.0`, earlier on a caller-supplied `boxPath`) is refused naming both and is never trusted; the probes then run the pinned files by absolute path, and a refused `node` also refuses `tsc` (a Node script). The pinned Python path is resolved in the box too and must be the file the host resolves (`facts.pythonReal`); its checksum is the pin. The default box PATH is the pinned directories alone. The host PATH `bwrap` is found on is a parameter (`hostPath`, default this process's), so no test changes `process.env`.

**Tests** (`node spikes/lang-bench/run-tests.mjs test/toolchain-*.test.mjs test/preflight-lang.test.mjs`). The pins and the three official-source readers
(`toolchain-pins`), digests / seal / tree hash / SDK versions / store root (`toolchain-store`), the installer through a fixture `io` (`toolchain-install`: a download that
is not the published bytes, an official source that disagrees with the pin, an install script that is not the recorded one, a second SDK, an integrity that is not the pinned
one - each leaves nothing in the store), the store read back and the hash record (`toolchain-verify`, including the real store), the preflight (`preflight-lang`: the pure
judgement, `A_PATH_that_still_finds_the_apt_SDK_is_refused`, `A_TypeScript_outside_the_pinned_line_is_refused`, and three cases inside a real box). A test that needs bwrap or the installed
store skips with its reason; `BENCH_REQUIRE_SANDBOX=1` turns the skip into a failure.

**Notes for later stories.**
- **Contract for S8b and E3 (the apphost root).** A program launched through its own apphost executable does not use PATH: it takes its dotnet root from `DOTNET_ROOT`, then
  `/etc/dotnet/install_location`, then the default install locations. With the distribution's SDK (`/usr/lib/dotnet`, 10.0.114) on the host that root was `/usr/lib/dotnet`
  (verified with `COREHOST_TRACE=1`: `Using dotnet root path [/usr/lib/dotnet]`), whatever the PATH said. Every box that runs a pinned-toolchain program (the execution layer's, the agent's,
  the scoring box) must therefore **hide `/usr/lib/dotnet`, `/usr/share/dotnet` and `/etc/dotnet` (an empty tmpfs over each that exists) AND set `DOTNET_ROOT` to the pinned SDK directory**;
  either alone leaves a road to the distribution's runtime. The preflight's box does both (`lib/preflight-box.mjs`, `DISTRIBUTION_DOTNET_PATHS`), and an **apphost probe** (`lib/apphost-probe.mjs`)
  builds a tiny console app with the pinned SDK inside the box (its scratch folder comes from the owned-name helper, `testsToDelete-u<uid>-bench-apphost-…` with an owner claim beside it, so a run killed during the build leaves something the next sweep collects) (offline, about 3 s: the SDK bundles the targeting and apphost packs), runs its apphost with `COREHOST_TRACE=1` and refuses
  any dotnet root other than the pinned directory. A test shows the open road: the same probe in a box without the hiding resolves `/usr/lib/dotnet`.
- `dotnet-install.sh` does not verify what it downloads; the check is ours (the archive's sha512 against the pin, which the release metadata confirms). The script's sha256 is pinned
  too, so a newer script makes the installer refuse until the pin is moved deliberately.
- Node's `SHASUMS256.txt` is read over HTTPS from nodejs.org; its GPG signature is not checked (no keyring is pinned).
- The Claude CLI copy (parent D8) is not part of this story.

## Package sets and snapshots (E1.S4)

not built yet

## Workspaces (E1.S3)

not built yet

## Visible samples (E1.S6)

not built yet

## Specs, gates and the freeze (E1.S2)

not built yet

## Draft references and the pre-freeze tool (E1.S2a)

not built yet

## Process-arm templates and run bases (E1.S7)

not built yet

## Skeleton and the push path (E1.S1)

Built: the pre-push check, the credit terms, the needle list, `generateRunBaseTree` with `needleScan`, and `workspace-diff`. Not built here: the task
repository's README, adapters and research stubs (`chore/skeleton`, the team lead's text) and the pushes themselves (the team lead's, with the owner's go).

**The command.** `node spikes/lang-bench/push-check.mjs` never pushes and never writes to GitHub; its modes are
`--repo <task repo> --branch <b>` (the commits `git rev-list <b> --not --remotes=origin` would add), `--text <file>` (a PR or review body; with
`--self`, a body of a bench PR: the leak and secret scans only), `--self --branch <b>` (the bench's own branch: the leak and secret scans only) and
`--setup-remote --repo <task repo>` (origin set idempotently, `fetch --prune --no-tags` over HTTPS with gh's credential helper for that one command,
`origin/main` checked to hold `LICENSE`). Exit 0 clean, 1 refused, 3 environment (git, the network, an aid or a named secret file that cannot be
read, public plans unreadable at `origin/main`: the check never passes without them), 4 configuration (no such branch, an origin that is not the task
repository, a remote-tracking ref that is not what `git ls-remote` says now). A report line is `<commit> <path>:<line> <reason>`; it never carries the
text that matched (a secret in a file NAME is masked to `[secret]`). The report also prints `leak tokens N (source)`, the `tip` sha it checked and
`push git push <url> <tip>:refs/heads/<branch>`: **publish that sha, not the branch name**, so what was checked is what is published.

**The enforced push path (task repository only).** `push-check.mjs --install-hook --repo <task repo>` writes `<repo>/.git/hooks/pre-push` (where git
looks), a small POSIX `sh` script. For every ref of a push it reads git's stdin line and runs `node <this checker> --repo <repo> --branch <ref>
--tip <local sha>` (with `--origin-url` and any options given at install baked in), so the commit checked is exactly the commit pushed, and
`--tip` makes a branch that moved off that sha exit 4. It refuses the push when a check fails or the bench checkout cannot be found, and for
a ref outside `refs/heads` (a tag is checked by hand, then pushed with `--no-verify`); a deleted ref passes. Installing twice changes nothing; a
hook that is not ours is refused (exit 4) unless `--force-hook`, which keeps it as `pre-push.foreign`; `--uninstall-hook` removes only a hook
with our marker line (`# lang-v2-push-check-hook v1`). **It is never installed into the bench repository**: the bench is shared by other
sessions, so its rule stays the manual `push-check.mjs --self --branch <b>` run before a push (and `--text --self` for a body); the install
refuses a repository that holds `spikes/lang-bench/push-check.mjs` or whose origin is not the task repository. The team lead installs it in
`/home/jinx/git/dew_flow_ai_language_comparison` after review.

**What the hook cannot do (N5), and where to install it from.** The hook is **client-side only**: it runs on this machine for a person or agent who pushes with
git here; it does not stop `git push --no-verify`, a push from another clone or machine, or a write made through the GitHub API or web UI. That is inherent in a
git hook, so the rest of the defence stays what it is (the PAT without Workflows, rulesets, the review of every PR, the check on PR bodies). **Install it from
the MAIN bench checkout (`/home/jinx/git/dew_flow_benchmark`), once this story is merged and that checkout is updated, not from a worktree:** the hook bakes the absolute
path of the `push-check.mjs` it was installed from, and a worktree is removed when its branch is done, which would leave a hook that refuses every push (it fails closed:
"bench checkout not found").

**The named checks** (one line each): `leakcheck` (six-word shingles of the two private aids, read from `~/rsd/lang-v2/test-design/` at run time,
and the literal tokens of `leak-tokens.txt` in that folder when it is present, or of `--tokens`), `secrets` (named shapes: fine-grained token,
`gh[pousr]_`, Anthropic key, any `BEGIN … PRIVATE KEY`, AWS key id; and the values of the two real secret files `~/.config/lang-v2/github-pat` and
`deploy_key_lang_v2`, or of `--secret-file`), `attribution` (the README closes with one `## Attribution` section; no link, known-task needle or plan id
elsewhere in it), `run-base-text` (links and the D18 needles in every file's text and path, `.gitmodules` excepted and pin files excepted from links;
plan, probe, gate and decision ids in `csharp/ node/ tasks/ runner/ todo/tasks/` and the README), `family-names` (`dew_flow_*` other than the
repository's own and `PLAN_lang_comparison`, in every file's text and path at the tip, in commit messages, `.gitmodules` excepted),
`known-task-credits` (a known-task text — the spec, or a task folder under `tasks/ csharp/ node/`, anywhere in the range or anywhere in the tip tree — needs its entry
in `inputs/credits-lang.json` and, in the README's attribution section outside HTML comments (an unclosed `<!--` hides the rest of the README, as CommonMark renders it), its credit as a substring, the CC BY-NC-SA 4.0
sentence naming both files, and RealWorld's MIT notice), `stack-folders` (a `.gitkeep` is the empty blob only, and only at the root of `csharp/` and
`node/` until E1.S3 writes `templates/stack-files.json`, a JSON list of path patterns with `<t>` for the task folder) and `tree-shape` (no symlink,
no submodule but `.agents/conventions`, no `.gitmodules` path or URL but the conventions repository's, no file with a NUL byte in a task repository;
UTF-16 is decoded and other binaries are read as latin1 for every scan).

**The whole tip tree, bounded and visible.** `stack-folders`, the symlink rule and the credit terms read the whole tip tree (`scan.tree`), not only the
range's files, because a run base carries the whole tree. `verifyFresh` compares the local `refs/remotes/origin/*` with `git ls-remote --heads origin`
both ways (a ref the remote lacks or has elsewhere, and a remote branch never fetched). Every external process goes through the one shared launcher
(`runBounded`: exe + argv, timeout, process-tree kill; `input` and `bytes` options added for the archive extraction and the byte reads); the blobs of one
check are read by one `git cat-file --batch` under an aggregate ceiling (256 MiB, one blob 16 MiB; exit 3 beyond it). The command prints progress
(`push-check: scanning N commits`, `reading N blobs`, ...) on stderr; stdout is the report.

**What is new, in one rule.** The published base is the remote ref of the branch being updated (`--dest`, which the hook passes from the pushed remote ref), otherwise
`origin/main`, never "any remote ref". The commits of a check are `git rev-list <tip> --not <base>` (their messages in one `git log --no-walk --stdin`), and the blobs it
scans and counts are exactly `git rev-list --objects <tip> --not <base>`: every parent of every merge is covered, and only what the destination already holds is skipped (a merge
of `origin/main` into an older branch is not charged for main's content; a blob that only another branch holds is scanned when a new branch adds it). The batch readers work in chunks
of at most 500 objects with the 16 MiB and 256 MiB ceilings kept across the chunks. `workspace-diff` treats only `ENOENT` as an empty folder: any other read error is a finding.

**Tags.** `push-check.mjs --tag <name> --repo <task repo>` is what the freeze procedure runs BEFORE it pushes a tag (a tag is pushed alone, the hook refuses a ref outside `refs/heads`,
and no branch check reads a tag): an annotated tag's message gets the task-repository body rules (leak, secrets, needles, family names; never printed), and the commit the tag points at
(annotated or lightweight) must be an ancestor of, or equal to, a commit the remote has NOW - `git ls-remote --heads --tags origin`, never the local tracking refs, which go stale when the remote
moves or deletes a branch - so it was checked when its branch was pushed; otherwise `tag-target` refuses it, naming the commit. A current remote commit this clone lacks cannot be tested: when nothing
present holds the tag's commit and some are absent, the finding says "fetch first" (the check never fetches for you); a failing `ls-remote` is exit 3. Then the tag is pushed with
`git push --no-verify` on the team lead's word.

**Residuals the reviewers accepted (named so nobody mistakes them for coverage).**
- `--dest <other>` trusts the other branch's history: the base is that branch's remote ref, so what it holds is not scanned again. A wrong or omitted `--dest` therefore changes what is
  "new"; the hook passes the pushed remote ref itself, and by hand `--dest` must name the branch really being updated.
- `--self` freshness: the bench's own check compares against the bench's `origin/main` as fetched; it does not run the task repository's freshness check against `ls-remote`, does not
  run the placement rules and does not refuse binaries. A stale local `origin/main` makes it read more or less than the push adds; fetch before running it.
- The own-value scan finds a real secret's literal text and its base64 forms (full, and the three alignments). It does not find a base64 of anything that is not one of the two real secret
  files, a JSON-escaped spelling (`\/`, `\u00xx`), or a partial token (a prefix or a few characters of it): those are covered only by the secret SHAPES, which need the whole token.

**What reads what.** History is published, so the leak and secret scans read every commit and every message. A run base carries the tip tree and
never the history, so every other rule reads the files as they stand at the tip (credited to the commit that brought the blob). Needles match by
D18's rule (change 4.1): case-sensitive, on word boundaries with ASCII letters and digits as the word characters, plus exact substrings
(`inputs/needles-lang.json`, the public list); a multi-word needle is matched over the whole text with any whitespace between its words. Shingles
tokenise like v1's `normalise` and drop nothing, and **no window is exempt for lying inside one whitespace token**: a passage joined with `/` or `-`
is one token and still a hit (the risk consult's D52(3) closed the earlier "one-token" exemption, which was a bypass). A path or URL is not a hit only
because the approved public texts hold its words. A finding says how many tokens its window spans (`[window spans N tokens]`), never which words. In the bench's own branch check (`--self`, and a bench body) a six-word run that the approved public texts already hold is not an aid hit. **In the task
repository (a branch, and a task PR body: `--text` without `--self`) only a window that spans at most two whitespace tokens and that the public texts hold
verbatim is exempt** (a path, a URL, the README's bench pointer): an aid sentence that also stands in a public plan is a hit there (re-review N2). The **approved public texts** are an explicit, reviewed set (D52(3)): the two plan files and the files named by
`spikes/lang-bench/inputs/public-texts.json` (schema 1, a `texts` list; absent at the ref means no extras), read at `origin/main` of the bench
repository with one `git cat-file --batch` per stage, never from the working tree and never "every markdown file" (a private paragraph spread over
two unlisted public files stays a hit); an unreadable text, an invalid list or a missing listed file is exit 3 (`--public <file>` names the public texts
instead, in tests); an aid left with no run of its own is exit 3, because it would check nothing.

**The credit terms** (`inputs/credits-lang.json`, the owner's of 2026-11-01): CSES problem pages and licence page read on that date, RealWorld's
notice copied verbatim from its `LICENSE` as it stood then (two copyright lines), the protocol page, the olympiad's statistics page for task 09. The
`credit` lines are checked as substrings of the README's squashed attribution section; the README sentence of task 09 ("… is based on IOI 2018
Werewolf (…)") carries the recorded credit.

**The run base's tree.** `lib/run-base-tree.mjs`: `generateRunBaseTree({ repo, ref, out, needles })` unpacks `git archive <ref>` into an empty
folder outside every worktree, cuts the README from `## Attribution` to the end (a README without the section, with it twice, or with a section after
it is refused), removes credit files (`CREDITS*`, `credits*`, `ATTRIBUTION*`) and runs `needleScan` over every entry; any hit (a symlink, a binary and
a file name count) refuses the cut, removes the folder and names `path:line needle`. E1.S7 commits that tree as the orphan run base.
`workspace-diff.mjs --root <checkout>`: per task, `csharp/<t>` and `node/<t>` hold the same files byte for byte except each stack's own pin files
(C#: `global.json`, `nuget.config`, `Directory.Build.props`, `*.csproj`; Node: `package.json`, `package-lock.json`, `tsconfig.json`, `.npmrc`,
`eslint.config.mjs`) and the one line after `<!-- stack-sentence -->` and `<!-- framework-sentence -->` in `STATEMENT.md`.

**Tests.** `node spikes/lang-bench/run-tests.mjs 'test/push-*.test.mjs' test/run-base-tree.test.mjs test/workspace-diff.test.mjs`. Every test builds
throw-away repositories with a bare `origin` in owned folders (`test/support/push-fixtures.mjs`) and a synthetic aid; no test reads the real aids,
the real secret files or the network.
