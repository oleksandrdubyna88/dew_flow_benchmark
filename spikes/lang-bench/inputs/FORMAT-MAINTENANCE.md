# FORMAT maintenance — conformance, amendments and the classes

This document is for the maintainers of the task format: the spec author, the suite author and the people who run the gates. It holds what the
format document (`todo/tasks/FORMAT.md` of the task repository) does not carry: how a spec is checked against it, how the format itself is
amended, and the class rule that protects its parts after a freeze. No workspace and no run base carries this document. It holds no hidden
test and no trap.

It applies to the task format, `Format version: 2`. Where this document and the format disagree, the format wins and this document is wrong.

## 1. Conformance (for the maintainers)

A spec conforms to the format when a checker finds all of the following. The checker is the spec conformance check, a tool of the bench for
the visible samples and for the format document, used by every spec's gate. **It is not built yet; nothing runs it.** Until it exists, the
same list is checked by hand and the result is recorded with the spec's gate. The tools in section 2 check the format document itself, not a spec.

1. **The file.** The name matches 1.1; the title's `NN` equals the file's.
2. **The header.** The five rows of 1.2, in order, each value either its placeholder or its later form; the `Kind` value one of the three
   phrases; the `Freeze rule` sentence exact.
3. **The sections.** Exactly the eight `##` headings of the kind (1.4), in order; section 8 is `None yet.` or a list of amendment items of
   the form 1.4 gives, numbered `A1` … `Ak` without gaps, matching the `Amendments` row.
4. **The R-ids.** Every requirement paragraph opens with `**R<n>.**`; the ids are unique and are exactly `R1` … `Rn`; before the freeze they
   ascend through the document; F-REQ's `{n}` equals n; every id the amendments name exists.
5. **The spans.** Every table, fenced code block and normative sentence outside section 6, section 8 and F-REQ lies inside a requirement's
   span (1.3: from `**R<n>.**` to the next requirement or heading). The check names each one that does not.
6. **The fixed sentences.** Every sentence that 2.1 requires for the kind is present, in its section and, where the table says so, inside a
   requirement paragraph, word for word after whitespace is collapsed, with every slot filled by an allowed value. The sentences are read
   from the format itself and never typed again in the checker; a format that lacks an expected sentence makes the checker refuse to run.
   Also: `{ref}` lists of any length, with `any of` before every range or list of two or more and a bare id for one; F-STOP's `{which}`
   is `A test`, or `A test that does not end at the end of its input` exactly when F-EOF is present; F-BYTES-O-N's items come from 2.6 only;
   exactly one of F-INVALID and F-ERRORS; F-LOAD in the same requirement as F-LIMITS-D; F-MEMORY and then F-PROCS in the same requirement
   as the limits sentence; F-ENV in the same requirement as F-RUN-O or F-RUN-D; F-TIMING present exactly when T is not 10 or G is not 5.
7. **T and G.** Every visible scenario's `start … within` equals the spec's T, and the `within` of the first `wait-exit` after the end trigger
   equals its G. Between the trigger and `wait-exit`, and after it, only `recv`, `expect`, `sleep` and `close` stand; there is exactly one
   `wait-exit`.
8. **The body forms.** A `body json` entry or a `recv … json` entry appears in a sample only where a requirement carries F-JSON for that reply
   or line.
9. **The samples.** `tasks/<NN-slug>/samples/` holds exactly the pairs `s01` … `s<k>` that section 6 shows; the olympiad table's cells (with
   `⏎` read as a line feed) or the daemon and web code blocks equal the files' bytes; every daemon and web pair parses under sections 4 and 5
   through the runner's own parser, never a second one: the head, the steps, the closed keyword list, strings and escapes, every variable
   bound before its use and bound once, `together` and `repeat` well formed, the last statement `wait-exit`, and the transcript's entries
   corresponding one to one, after expansion, to the entries the script produces.
10. **No reading block.** The summary of the grammar that older specs copied is absent; the spec names the runner and its `GRAMMAR.md`.
11. **No forbidden needle.** No hit, case-insensitive, of any needle of a list the spec author supplies with the check (for a known task,
    its id, title and site names; for every task, anything else the test-design notes say a spec must not carry). The list is kept outside
    git with those notes, since it may name what it guards. Always added to it: the bench plan's identifiers (story, epic, decision, gate and
    test ids, by pattern), web links (the http and https schemes), and the names of the implementation languages, runtimes and web frameworks
    of the series.
12. **The `⏎` glyph** occurs only in section 6.

The same parser checks the format document's own examples: the files of 6.2 and 6.3 parse, and the runner, against one stub program per
kind, gives the verdicts the examples state, and each stated failure from its broken stub.

## 2. The tools that check this document

The tools use only their language's standard library and are in `spikes/lang-bench/`. FORMAT is read as raw bytes: UTF-8 with line feeds only, no
byte order mark. A carriage return anywhere, or a byte order mark, is a format error that names the first line. Nothing is normalised, and
the sha256 of the format is taken over the raw bytes. A heading is a line that starts with `## ` or `### ` outside a fenced code block (a
fence opens with a line that starts with three or more backticks or tildes and closes with the same marker), so a `#` line in a scenario
example is never a heading. The structure is exact: `## 1.` to `## 7.` once each, in order, and under `## k` the headings `### k.1`,
`### k.2` … with no gap and no duplicate. Anything else is a format error that names the heading and the line.

Exit codes of both tools: 0 success, 1 a refusal (one line each: the heading, the line, the rule), 4 a usage or format error.

- **`tools/grammar-extract.mjs <FORMAT.md> <out>`** writes `runner/GRAMMAR.md` for a workspace: one header line
  `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)`, one empty line, then the bytes of the format from the `## 3.`
  heading line to the byte before the `## 6.` heading line. The output is the same bytes on every run. It never overwrites a file. A change
  in section 4 changes the output; a change in section 7 changes only the sha256 in the header.
- **`tools/format-classes.mjs`** has two modes.
  `--self <FORMAT>` checks the class table of the document (section 3 below).
  `--frozen <A's FORMAT> --candidate <B's FORMAT> [--record <format.json>]` checks that B changed nothing that is protected in A.
  The frozen file must be the one the record names: the one source of truth for the reference is `A.sha256` in
  `spikes/lang-bench/inputs/specs.d/format.json`. A frozen file with another sha256 is refused (exit 1); there is no other way to name the
  reference.
- **`test/format-bounds.test.mjs`** compares the figures the format states with the figures the runner and the execution layer enforce:
  the 256 MiB standard-error stop (F-STDERR and 4.6) with the runner's limit, F-ENV's `TZ` with the layer's base environment, the variables 4.2
  says an `env` line may not set with what the layer's `boxEnv` refuses, and F-PROCS's 512 with the process bound of the layer's scope. Each
  figure is read by a strict reader that demands exactly one match; a missing or doubled line is the reader's error, never a silent pick.

The record `format.json` holds `A`: the version, the sha256 of the format's raw bytes, the gate result (`null` until the format passes its
gate), the sha256 of the extracted grammar, and the numbers of the tasks whose specs were written against that version.
`node spikes/lang-bench/specs-record.mjs` assembles `inputs/specs.json` from it; never edit `specs.json` by hand.

## 3. The classes and the freeze

Every section of the format has one class: `common` (it serves olympiad tasks and another kind, or every kind), `olympiad`, `daemon`, `web`
or `daemon-web` (it serves daemon and web tasks and no olympiad task). Where a section mixes kinds of task, every subsection has the class
and the lead text of the section, before its first subsection, is `common`. The class table is the table directly after the
`**Section classes.**` paragraph in the preamble; a row is `| <heading without ## or ###> | <class> |`.

**The table (`--self`).** Every row names an existing heading by its whole text, and its class is one of the five. Every `##` section has
exactly one row, or, for a mixed section, each of its `###` has exactly one row and the section has none. A row for a heading that is inside a
fenced block, a heading with two rows, and a malformed row are refused.

**The freeze.** After the freeze of the first group of tasks the `common` and `olympiad` parts do not change and keep their numbers. A
`daemon`, `web` or `daemon-web` part may change before the freeze of a later group, as a new dated version that names the tasks it touches,
and such a change touches no olympiad task. The changelog only grows.

**The parts (`--frozen`).** The format is cut into parts, and no byte is in two parts or in none. A section classed as a whole is one part,
from its heading line to the byte before the next `##` heading; its `###` belong to it. A mixed section has its lead text as one part (class
`common`, from its `##` line to the byte before its first `###`) and each `###` as one part (to the byte before the next `###` or `##`). The
preamble is the part before `## 1.`. A boundary blank line belongs to the part before it. The protected set comes from the frozen file's
class table only.

**What a candidate may change.** A protected part is compared byte for byte, with these exceptions only:

- the three header rows of the preamble (`Format version: …`, `Gate verdict: …`, `Freeze tag: …`, whole lines), and class-table rows after
  the frozen file's last row; a row of a part that is not protected may also change or go;
- section 7 (the changelog) must start with the frozen file's bytes; it may only grow;
- 2.1's table of fixed sentences is compared row by row, the rows matched by their first cell. A row whose sentence is defined only in parts
  that are not protected may differ. Every other row is equal byte for byte. A sentence is defined where its label (`F-…` at the start of a
  line outside fences) stands; a sentence with no label is defined where the paragraph that introduces its fenced block names it.

**What a candidate may not do.** The check refuses, naming the heading, the frozen file's class and the line: protected bytes that differ; a
protected class row that is relabelled or gone; a protected part that is missing; a part added as `common` or `olympiad`; a subsection
inserted inside a protected mixed section, because it changes the numbers of the protected parts after it; a heading number of a protected
part that changed. Appending a subsection at the end of a mixed section with the next free number passes when its class is `daemon`, `web` or
`daemon-web`, and so does turning a `daemon-web` section into subsections of those classes.

## 4. Amendments (for the maintainers)

**This document and the format.** Before the freeze, a change is a revision of the current version, recorded in the format's changelog
(section 7 of the format). After the freeze — the tag `spec-frozen-<yyyymmdd>` that also freezes the format, whose sha256 is registered with
the specs' — a change is a **dated amendment**: it raises `Format version` by one, adds a changelog line
`- v<k> · <YYYY-MM-DD> · amendment · touches <NN, NN | all daemon and web tasks | all> — <what changed and why>`, and names every task it
touches: every task whose spec, samples or held-out cases read differently under the change. A change to the scenario grammar or to the
comparison rules touches every daemon and web task, and `runner/GRAMMAR.md` is extracted again and the runner pinned again; a change to a
fixed sentence touches every spec that carries it. Each touched spec takes an amendment of its own on the same date (1.4), bringing it to the
new version. The class rule of section 3 limits what an amendment may change: after the first freeze no `common` or `olympiad` part.

**What an amendment costs.** An amendment of the format, like an amendment of a spec, **voids the registered limits of every task it
touches** (the bench plan's calibration-order check): those tasks are validated jointly, calibrated and registered again, for both stacks,
before the pre-registration. After the pre-registration, neither the format nor a spec changes.

**A spec.** After its freeze tag a spec changes only by a dated amendment applied to both stacks before the pre-registration: the item in
section 8 of the spec, the `Amendments` row updated, R-ids appended or withdrawn as 1.3 says, never renumbered.

**This document.** It changes with a commit that says why. It never holds a plan identifier, a web link, a name of an implementation
language, runtime or web framework of the series, or any content of a hidden test.
