# FORMAT maintenance — conformance, amendments and the classes

This document is for the maintainers of the task format: the spec author, the suite author and the people who run the gates. It holds what the
format document (`todo/tasks/FORMAT.md` of the task repository) does not carry: how a spec is checked against it, how the format itself is
amended, and the class rule that protects its parts after a freeze. No workspace and no run base carries this document. It holds no hidden
test and no trap.

It applies to the task format, `Format version: 2`. Where this document and the format disagree, the format wins and this document is wrong.

## 1. Conformance (for the maintainers)

A spec conforms to the format when a checker finds all of the following. The checker is the spec conformance check, a tool of the bench for
the visible samples and for the format document, used by every spec's gate. **It is built: `node spikes/lang-bench/conformance.mjs --repo <task repository> --task NN-slug
--format <FORMAT.md> [--needles <file>]`.** It prints one line per check, `ok <check>` or `FAIL <check> — <what, where>`, and exits 0 when every check is ok, 1 on a
deviation, 4 when it cannot run (a missing task, spec or samples folder; a format it cannot read or that lacks a sentence it expects; no `python3`, no runner, a runner that
does not finish in 60 seconds): a refusal to run is never a pass. The result of the run is recorded with the spec's gate. The tools in section 2 check the format document itself, not a spec.

The ten checks of the command, and the items below they cover: `file` (1), `header` (2), `sections` (3), `r-ids` (4 and 5), `sentences` (6), `T/G` (7), `samples` (9),
`sample-forms` (8, and the forms of the sample files that the runner leaves to the format), `sample-env` (the `env` lines of the samples) and `no-needle` (10, 11 and 12). Four things
it does only in part, and they stay with the reviewer: a normative sentence outside every requirement that is not a fixed sentence (5); a `body json` or `recv … json` entry is
refused only when no requirement carries F-JSON, not reply by reply (8); the reading block is recognised as a paragraph that names eight or more of the script's keywords in code
spans (10). The needles of check 11 are the push check's plan ids, links and known-task needles, the stack words of `inputs/stack-words-lang.json` (the implementation languages, runtimes,
frameworks and flags of the series, public, named by the word in a finding) and the author's `--needles` file. In group B each olympiad spec of A is checked under A's FORMAT and under B's.

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
the sha256 of the format is taken over the raw bytes. A heading is a line that starts with `## ` or `### ` at column 0 outside a fenced code block, so a `#`
line in a scenario example is never a heading. A fence opens with three or more backticks or tildes (at column 0, or, inside a list item, at the item's
content column up to three columns more) and closes with a line of the same character, at least as long, indented at most three columns, and nothing else
on it; a fence in a list item ends where the item ends. The structure is exact: `## 1.` to `## 7.` once each, in order, and under `## k` the headings
`### k.1`, `### k.2` … with no gap and no duplicate. Anything else is a format error that names the heading and the line.

The tools read the format as CommonMark does, and a line that the two would read differently is a format error (exit 4) that names the line, in either file
of a comparison and in both tools: a backtick fence whose opening line has a backtick after the marker; a fence line indented one to three spaces outside a list
item; an HTML block start at columns 0 to 3 outside a fence (`<!--`, `<?`, `<!X`, `<![CDATA[`, the tags of the HTML block kinds one to six, and a complete tag
alone on a line where no paragraph is open); an ATX heading in a form the tools do not take (a tab after the `#`s, one to three spaces before the `#`s, `#`s
with nothing after them); a setext heading (a paragraph line followed by a line of only `=` or only `-`, indented at most three spaces: a `---` line after a blank
line is a thematic break and is allowed); an ATX heading or a fence opener inside a block quote (`> ## x`, `> ```x`) or after a list marker on the same line
(`- ## x`, `1. ```x`). A fence inside a list item is read (an unprotected part of a candidate may not hold one: section 3). A dash line directly under a list item line is refused too, with the message "a dash line under a list item (a thematic break or a setext underline: refused)" (it may be a thematic break, but the tools do
not tell). What the tools do not look at: a character reference or a link label that spells a heading.

Exit codes of both tools: 0 success, 1 a refusal (one line each: the heading, the line, the rule), 4 a usage or format error.

- **`tools/grammar-extract.mjs <FORMAT.md> <out>`** writes `runner/GRAMMAR.md` for a workspace: one header line
  `# Scenario and sample grammar (from FORMAT.md v<version>, sha256 <hex>)`, one empty line, then the bytes of the format from the `## 3.`
  heading line to the byte before the `## 6.` heading line. The output is the same bytes on every run. It never overwrites a file. A change
  in section 4 changes the output; a change in section 7 changes only the sha256 in the header.
- **`tools/format-classes.mjs`** has two modes.
  `--self <FORMAT>` checks the class table of the document (section 3 below) and also the restricted grammar over its unprotected parts and the whole-file rules (exit 1), so a
  document that would fail as a frozen reference shows it before its gate.
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
fenced block, a heading with two rows, and a malformed row are refused. The changelog (section 7) must be one whole `common` row: a row of another class, a missing
row, or rows for subsections of it are refused, so no class can take the changelog out of the rule that it only grows.

**The freeze.** After the freeze of the first group of tasks the `common` and `olympiad` parts do not change and keep their numbers. A
`daemon`, `web` or `daemon-web` part may change before the freeze of a later group, as a new dated version that names the tasks it touches,
and such a change touches no olympiad task. The changelog only grows.

**The parts (`--frozen`).** The format is cut into parts, and no byte is in two parts or in none. A section classed as a whole is one part,
from its heading line to the byte before the next `##` heading; its `###` belong to it. A mixed section has its lead text as one part (class
`common`, from its `##` line to the byte before its first `###`) and each `###` as one part (to the byte before the next `###` or `##`). The
preamble is the part before `## 1.`. A boundary blank line belongs to the part before it. The protected set comes from the frozen file's
class table only.

**What a candidate may change.** A protected part is compared byte for byte, with these exceptions only:

- the three header rows of the preamble (`Format version: …`, `Gate verdict: …`, `Freeze tag: …`, whole lines). Each stays on the line the frozen file has it
  on, and its value matches a strict pattern: `Format version: <digits>` or `<digits>.<digits>`; `Gate verdict:` one to four words of lower-case letters, digits
  and `_ . -` (no colon, at most 80 characters: `not yet gated`, `proceed`, `passed on 2026-12-01`) or the form of a spec header (a verdict word in backticks, optionally
  followed by ` · ` and a date `yyyy-mm-dd`); `Freeze tag:` `none yet` or `spec-frozen-<yyyymmdd>` or
  `spec-frozen-<one capital letter>-<yyyymmdd>`. Class-table rows after the frozen file's last row may be added. A row of a part that is not protected may
  change or go, and it is matched to the frozen row by the number of the heading it names, so an unprotected heading can be renamed together with its row; a
  protected row stays byte for byte and in its place;
- section 7 (the changelog) must start with the frozen file's bytes; it may only grow, and the text appended after them must start after a blank line (it may not continue the
  frozen last line or its list item) and obey the line forms of the restricted grammar below, like an unprotected part;
- 2.1's table of fixed sentences is compared row by row. The rows are the contiguous block of `|` lines right under the `| Id |` header row and its separator
  row; any other `| F-…` line is plain text of its part and compared as such. The rows are matched by their first cell, and an F-id has one row (a second row,
  in either file, is refused, naming the id and both lines). A row whose sentence is defined only in parts that are not protected may differ, but the kinds cell of a changed or added row may not
  name `all` or `olympiad` (such a change touches no olympiad task) and may hold only the words `daemon`, `web`, `and`, `or`, commas, and the words that the kinds cells of
  the frozen file's free rows already use (read from the frozen file, never typed into the tool), as plain ASCII letters, spaces and commas: no `&`, no entity, no look-alike
  letter. Every other row is
  equal byte for byte, and the protected rows keep the order the frozen file has. A sentence is defined where its label (`F-…` at the start of a
  line outside fences) stands; a sentence with no label is defined where the paragraph that introduces its fenced block names it.

**Who this protects.** Agents read the format and `runner/GRAMMAR.md` as raw bytes; the byte comparison protects the bytes of the protected parts (the exceptions are listed here: the header rows, the class-table rows after the last row, the changelog's appended text, the free rows of 2.1), and it does not judge what an unprotected part says. A free part can still contradict protected text in plain words (`Notwithstanding 2.6, any normalisation is allowed for olympiad tasks.` in 2.5 passes), which remains a point for human review of any amendment. The restricted grammar and the inline-HTML rule below
defend a human reviewer who reads a RENDERED page against an amendment that is misleading on screen while its bytes look harmless. They were checked with markdown-it in CommonMark
mode, and they are not a proof about every renderer. One thing is spacing only: a blank line before an appended changelog entry makes that list "loose" when rendered (more space
between its items); the words do not change.

Direct evidence on the real draft (fix batch 5): three changes to every line (delete the line, append ` zz` to it, insert an empty line after it), 3,435 candidates; the only passes in protected parts are the `Gate verdict` value, the 14 free 2.1 rows, and one extra line feed at the end of the file. `test/format-evidence.test.mjs` runs a sample of this (every 53rd line and the lines that matter, about 3 seconds); the full sweep takes about two minutes and is recorded here only.

**The restricted grammar (every candidate).** Patching the reader one form of the markup at a time cannot close the class "the reader and a renderer read a candidate
differently": a deletion can turn a literal inside a fence into a heading, and a link reference definition acts at a distance. So the guarantee is by construction, and the
check (`lib/format-grammar.mjs`) applies it to every line of every part that is not protected in the CANDIDATE (parts as the candidate's own class rows cut them; the same check
runs on the frozen file as a sanity check, and a frozen file that fails is exit 4). Every line of such a part is one of these forms, and a line that is none is refused (exit 1,
naming the part, its class, the line and the form):

1. a blank line;
2. a heading at column 0 in the exact form `## k. Title` or `### k.n Title`, without a closing sequence of `#`; an ADDED heading (one the frozen file's unprotected parts do not
   have) whose title, without its number, is the title of ANY heading inside a protected part (a subsection of a whole protected section included), compared without regard to case and
   to runs of spaces, is refused (no look-alike section); the check also runs on headings in the text appended to the changelog;
3. a paragraph line: it starts at column 0 with a character that does not start a block: not a space or a tab, not `#`, `<`, `>`, `|`, `~`, `=`, `-`, `+`, `_`, `]`; not three
   backticks; not `*`, except `**` that is not `***` (emphasis); not an ordered list marker (digits, `.` or `)`, then a space, a tab or the end of the line, so `4.6).` is text), and not a list marker followed by a tab (`-` and a tab: a renderer reads a list item); not `[`
   unless a `]` closes on the line, no backslash stands inside the first bracket and no `:` follows it (no reference definition, not even one whose label runs over two lines). One or two backticks and `**` may start a line;
4. a list item: `- ` or one to three digits and `. ` at column 0, then text that is a paragraph line (rule 3 on the text); its continuation lines are indented by exactly the marker
   width (2 for `- `, the digits plus 2 for a numbered one) and the text after the indent obeys rule 3; no deeper indent, no nested list, no fence and no heading inside an item;
   an ordered item whose number is not 1 may not stand directly after a paragraph line or a bullet item (CommonMark reads it as text of that paragraph, so a code span can pair across
   it; after a blank line, a heading, a fence or an ordered item it starts or continues a list);
   an item ends at any column-0 line that is not an item;
5. a table row: `|` at column 0 and `|` at the end, with no reference definition in it, and no `\\|` (renderers disagree on whether the pair of backslashes escapes the pipe);
6. a fenced block: an opener at column 0 that is exactly three backticks or three backticks and lower-case letters, a closer of exactly three backticks at column 0, both in the
   SAME part; the lines between are free text, except that none is a fence marker line (three or more backticks or tildes, up to three spaces of indent) other than the closer.

**Four rules for the whole file** (both files, every part, protected ones too, outside fences): no link reference definition (a line whose first non-space character is a `[` that a `]:` closes, at ANY indent; an escaped bracket in the label and the footnote form `[^x]:` count); no line whose first
non-space character is `<` (no raw HTML block start); no table row (a line that starts with a pipe) that holds `\\|`; and no raw inline HTML: a `<` followed by a letter, `/`, `!` or `?` that stands outside a code span and is not written `\<` (autolinks
and comments included). A rendered page lets such a tag act at a distance (`Read on <details>` leaves everything after it inside a closed element), and text between two
tags, or a tag in a list item or a table cell, is no safer. Code spans follow CommonMark's rule: a run of backticks is closed by the next run of exactly the same length, over a PARAGRAPH (the
consecutive non-blank lines; a span may pair across lines) and not across a heading, a list item, a quote, a thematic break or a fenced block; a table is read in two ways, as text of a
paragraph and as rows, one row at a time and cell by cell (a pipe that a backslash escapes does not cut a cell), and a tag that either reading leaves outside a span is refused. A
guard: `` `<id>` `` in a code span passes, and `` `<id> `` with the span not closed on the line is refused. The argument: every protected part starts at a column-0 heading, where
CommonMark starts a fresh block; no unprotected construct can stay open across a part boundary (a fence closes in its part, there is no HTML block, no list continues across a heading); and
nothing acts at a distance (no reference definition, no raw inline HTML). So a renderer shows a protected part as it shows the same bytes of the frozen file (the protected bytes are
compared byte for byte); the argument is about CommonMark with the reader's checks, not about every renderer. The checks of the reader (the format errors of section 2) stay and run first.

**What a candidate may not do.** The check refuses, naming the heading, the frozen file's class and the line: protected bytes that differ; a
protected class row that is relabelled or gone; a protected part that is missing; a part added as `common` or `olympiad`; a subsection
inserted inside a protected mixed section, because it changes the numbers of the protected parts after it; a heading number of a protected
part that changed; a protected heading whose number stays and whose title changed. When a section that was one unprotected part becomes a mixed section, its
lead text may only be its `##` line and blank lines, or the bytes it had before (anything else is a part added as `common`). Appending a subsection at the end of a mixed section with the next free number passes when its class is `daemon`, `web` or
`daemon-web`, and so does turning a `daemon-web` section into subsections of those classes.

## 4. Amendments (for the maintainers)

**This document and the format.** Before the freeze, a change is a revision of the current version, recorded in the format's changelog
(section 7 of the format). After the freeze — the tag `spec-frozen-<yyyymmdd>` (or `spec-frozen-<letter>-<yyyymmdd>`) that also freezes the format, whose sha256 is registered with
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
