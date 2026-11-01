# Language comparison v2 — the bench module (`spikes/lang-bench`)

> One `##` section per E1 story; each story writes only under its own heading (`todo/PLAN_lang_comparison_v2_E1.md` § 3, "Conventions for every story").

## The runner (E1.S8a)

The test-runner scaffold: `node spikes/lang-bench/run-tests.mjs [glob…]` runs this spike's `test/**/*.test.mjs` through `node --test` at lowered priority with at most `min(8, CPUs − 1)` workers, and a Node test file starts the Python units of `test/py/` under `python3 -I`; the runner itself (`runner/`) is not built yet.

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
registered. The installer runs under umask 022 (`INSTALL_UMASK`, set by the command and by `installToolchain` itself, which puts the caller's umask back; the children - tar, `dotnet-install.sh`, npm - inherit it), because the tree hash covers read and execute bits and the same archive extracted under 077 would hash differently. When `stacks-lang.json` already records a tree hash for the toolchain, the staged tree is hashed and compared BEFORE it is sealed and renamed: a difference discards the tree and fails naming both hashes, so a tree that is not the recorded one is never registered. An install that was killed leaves its staging folder (possibly sealed) in the store root; the next install first collects this user's `testsToDelete-u<uid>-bench-stage-*` folders whose owner record says the process is gone (`lib/toolchain-stale.mjs`: unseal, remove, name each on stderr), and leaves a live owner's, an unmarked and anyone else's folder alone. The tree is hashed before it is sealed and sealed only just before the rename. A directory already in the store is left alone (`present`). Never apt, never sudo. `--record` writes the hashes of what the run installed; a recorded hash is
never moved.

**Verify.** `node spikes/lang-bench/install-toolchains.mjs --verify [--deep]`: every directory present and sealed, the SDK directory holding only the pinned SDK, with
`--deep` every tree hash equal to the record. Exit 1 names the problem; exit 2 is usage or a store inside a git worktree.

**Preflight.** `lib/preflight-lang.mjs`, `preflightLang({ stacks, storeRoot, boxPath? })`. It first reads the store back deep (`verifyStore({ deep: true })`, about 1.2 s: sealed, one SDK, every tree hash equal to the record), because a binary replaced in place still answers `--version` with the pin; a toolchain with no recorded hash is refused. The probes run inside a bwrap box built from v1's primitives (root read-only, home /mnt
/run /tmp hidden, the distribution's dotnet hidden and `DOTNET_ROOT` set, the three directories bound back read-only, no network, pid namespace, cleared environment); S8b's `box.mjs` replaces it once it exists. It reads back
`node --version`, `dotnet --list-sdks`, `dotnet --list-runtimes`, `tsc --version`, the sha256 of the pinned `python.path`'s real file (the path is written once, in `stacks-lang.json`; the probe argv appends it), and `locale -a`, and refuses, naming the pin and
the value seen: another Node patch, a second SDK or the wrong one, another runtime, a TypeScript outside 6.0.x or another 6.0 patch, a changed Python binary, and a
missing `de_DE.utf8` (with `sudo locale-gen de_DE.UTF-8`, the remedy derived from the pinned locale name). It also refuses a box PATH that reaches a second `dotnet`. That scan runs INSIDE the box (`which -a dotnet` under the box's environment, then `readlink -f` of each hit and of the pinned one, in the box), so a distribution SDK the box hides is not a second dotnet, a PATH entry that exists only on the host is not counted, and one real file reached through two names is one dotnet. The probes' executables are looked up on the box's PATH by bwrap, not on the host's. The default box PATH is the pinned directories alone.

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
  builds a tiny console app with the pinned SDK inside the box (offline, about 3 s: the SDK bundles the targeting and apphost packs), runs its apphost with `COREHOST_TRACE=1` and refuses
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

not built yet
