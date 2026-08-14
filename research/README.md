# research/ — the system as it is

This folder holds **documentation of what exists**: architecture, module deep-dives, and the design
records of decisions that already shipped — including plans whose work is done, kept because the code
references them and they explain why the system looks the way it does.

Open work lives in [todo/](../todo/README.md). The test is a single question:

> Is someone still supposed to build this?

Yes → `todo/`. No → `research/`.

## Currently here

| Document | What it is |
|---|---|
| [MEASURED_LESSONS.md](MEASURED_LESSONS.md) | The evidence base this project is built on — carried over from an earlier measurement programme against a different codebase, so nothing here depends on that repository being checked out. Every guard in the domain traces to a numbered finding in it |

## Conventions

- A plan arrives here only when its work is done, with `> Status: **IMPLEMENTED, <YYYY-MM-DD>.**` and a
  record of what shipped **differently** from the plan. The deviations are the most valuable part.
- Architecture notes follow the `architecture.md` + `module_<name>.md` split once there is enough
  system to describe. Until then this folder holds only what is genuinely settled.
- Cross-repository citations are **paths, not links** (see [todo/README.md](../todo/README.md)): a
  relative link that resolves on one machine is worse than a citation that names its source.
