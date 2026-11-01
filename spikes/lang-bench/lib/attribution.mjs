// The README's `## Attribution` section: the one place in the task repository where a credit, a link or a pointer to the bench may
// stand (parent D2, D39). The section closes the file, so cutting "from its heading to the next `## ` or the end" (the run-base cut)
// and "everything outside it" are the same two halves.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const HEADING = /^## Attribution\s*$/;
const ANY_H2 = /^## /;

/** ok({ firstLine, endLine }) (1-based, both inclusive: the heading and the last line) or a fail saying how the structure is wrong. */
export function attributionSection(readmeText) {
  const lines = readmeText.split('\n');
  const headings = lines.flatMap((l, i) => (HEADING.test(l) ? [i] : []));
  if (headings.length === 0) return fail('the README has no `## Attribution` section');
  if (headings.length > 1) return fail('the README has the `## Attribution` heading more than once');
  const next = lines.findIndex((l, i) => i > headings[0] && ANY_H2.test(l));
  if (next !== -1) return fail(`the \`## Attribution\` section must be the closing section, but a \`## \` section follows it at line ${next + 1}`);
  return ok({ firstLine: headings[0] + 1, endLine: lines.length });
}

/** The README with the attribution section's lines emptied (line numbers kept); the whole text when it has no section. */
export function outsideAttribution(readmeText) {
  const section = attributionSection(readmeText);
  if (!section.ok) return readmeText;
  return readmeText.split('\n').map((l, i) => (i + 1 >= section.value.firstLine ? '' : l)).join('\n');
}

/** The section's text (heading included), or an empty string when the README has no well-formed section. */
export function attributionText(readmeText) {
  const section = attributionSection(readmeText);
  return section.ok ? readmeText.split('\n').slice(section.value.firstLine - 1).join('\n') : '';
}

/** ok(text before the `## Attribution` heading, exactly), or the structure's fail: the run-base cut (D39) of a README. */
export function cutAttribution(readmeText) {
  const section = attributionSection(readmeText);
  if (!section.ok) return section;
  const before = readmeText.split('\n').slice(0, section.value.firstLine - 1);
  return ok(before.length === 0 ? '' : `${before.join('\n')}\n`);
}
