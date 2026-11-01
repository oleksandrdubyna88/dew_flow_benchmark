// What may appear where in the public task repository: the attribution section is the one home of every credit, link and pointer
// to the bench; the agent-visible folders carry none of them (and no plan ids); the stack folders hold only the template.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { attributionSection, outsideAttribution } from '../lib/attribution.mjs';
import { parseKnownNeedles } from '../lib/push-needles.mjs';
import { findStackFolderFiles, findVisibleRules, isAgentVisible } from '../lib/push-visible.mjs';

const known = parseKnownNeedles(JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/needles-lang.json', import.meta.url)), 'utf8'))).value;
const README = ['# Title', '', 'What this is.', '', '## Tasks', '', 'A list.', '', '## Attribution', '', 'Based on CSES, see https://cses.fi/x', 'Plan: dew_flow_benchmark and its plan file', ''].join('\n');
const rules = (path, text) => findVisibleRules(path, text, { known });

test('The_attribution_section_is_found_once_and_must_close_the_readme', () => {
  const section = attributionSection(README);
  assert.equal(section.ok, true);
  assert.deepEqual([section.value.firstLine, section.value.endLine], [9, 13]);
  assert.match(attributionSection('# T\n\n## Attribution\nx\n## Tasks\ny\n').reason, /closing section/);
  assert.match(attributionSection('# T\n').reason, /no `## Attribution`/);
  assert.match(attributionSection('# T\n## Attribution\n## Attribution\n').reason, /more than once/);
});

test('A_link_in_the_attribution_section_is_allowed_and_the_same_link_elsewhere_in_the_readme_is_not', () => {
  assert.deepEqual(rules('README.md', outsideAttribution(README)), [], 'the section is blanked, every other section is clean');
  const leaky = README.replace('A list.', 'A list, see https://example.org/x and CSES.');
  const found = rules('README.md', outsideAttribution(leaky));
  assert.deepEqual(found.map(f => [f.line, f.what]), [[7, 'link'], [7, 'needle CSES']]);
});

test('The_outside_text_keeps_every_line_number', () => {
  assert.equal(outsideAttribution(README).split('\n').length, README.split('\n').length);
});

test('An_agent_visible_path_carries_no_plan_id_link_or_known_task_needle', () => {
  const text = ['fine', 'Story E1.S8a says', 'probe T-L31 and G-12', 'decision D41 and D7', 'link http://x.test', 'Redis', ''].join('\n');
  const found = rules('todo/tasks/05-key-value-server.md', text);
  assert.deepEqual(found.map(f => [f.line, f.what]), [
    [2, 'plan story id'], [3, 'probe id'], [3, 'gate id'], [4, 'decision id'], [5, 'link'], [6, 'needle Redis'],
  ]);
});

test('Plan_ids_apply_to_the_agent_visible_folders_but_links_and_needles_apply_to_every_path', () => {
  assert.equal(isAgentVisible('research/architecture.md'), false);
  assert.deepEqual(rules('research/architecture.md', 'E1.S8a D41').map(f => f.what), [], 'ids stay in the agent-visible folders and the README');
  for (const p of ['research/architecture.md', 'AGENTS.md', '.agents/PROJECT.md', 'todo/README.md', '.github/workflows/ci.yml', 'LICENSE', 'tasks/01/NOTES.md']) {
    assert.deepEqual(rules(p, 'see https://x.test about Werewolf').map(f => f.what), ['link', 'needle Werewolf'], p);
  }
  for (const p of ['csharp/a/x.cs', 'node/a/x.ts', 'tasks/01/s01.in', 'runner/run.py', 'todo/tasks/FORMAT.md']) assert.equal(isAgentVisible(p), true, p);
});

test('The_gitmodules_file_is_exempt_from_links_and_needles_and_a_pin_file_from_links_only', () => {
  assert.deepEqual(rules('.gitmodules', 'url = https://github.com/o/r.git # Werewolf'), []);
  assert.deepEqual(rules('node/t/package.json', '{"registry":"https://registry.npmjs.org/","dependencies":{"redis":"1"}}').map(f => f.what), ['needle redis'], 'a registry URL is a pin file\'s nature, a needle is not');
  assert.deepEqual(rules('csharp/t/nuget.config', '<add value="https://api.nuget.org/v3/index.json" />').map(f => f.what), []);
});

test('The_family_names_are_needles_except_in_gitmodules', () => {
  const text = 'a dew_flow_benchmark, a PLAN_lang_comparison_v2 and a dew_flow_rag_qln but not dew_flow_ai_language_comparison\n';
  assert.deepEqual(rules('runner/x.py', text).map(f => f.what), ['needle dew_flow_benchmark', 'needle PLAN_lang_comparison', 'needle dew_flow_rag_qln']);
  assert.deepEqual(rules('.gitmodules', 'url = https://github.com/o/dew_flow_conventions.git\n'), [], '.gitmodules is the one excluded path (its conventions URL is allowed)');
  assert.equal(rules('node/t/package.json', 'dew_flow_conventions').length, 1, 'the same name in any other path is a hit');
  assert.equal(rules('research/architecture.md', 'see dew_flow_benchmark').length, 1, 'the family names are scanned in every file, not only in the agent-visible folders');
});

test('A_pin_file_may_hold_a_registry_url_that_any_other_file_may_not', () => {
  assert.deepEqual(rules('csharp/t/nuget.config', '<add value="https://api.nuget.org/v3/index.json" />\n'), []);
  assert.equal(rules('csharp/t/Program.cs', '// https://api.nuget.org\n').length, 1);
});

test('Push_check_refuses_a_source_file_under_a_stack_folder_outside_the_template_set', () => {
  const paths = ['csharp/.gitkeep', 'node/.gitkeep', 'csharp/01/Program.cs', 'node/01/src/main.ts', 'tasks/.gitkeep'];
  assert.deepEqual(findStackFolderFiles(paths).map(f => f.path), ['csharp/01/Program.cs', 'node/01/src/main.ts'], 'before the templates exist only .gitkeep is allowed');
  const templateFiles = ['csharp/<t>/Program.cs', 'csharp/<t>/global.json', 'node/<t>/package.json'];
  assert.deepEqual(findStackFolderFiles(['csharp/01/Program.cs', 'csharp/01/Extra.cs', 'node/01/package.json', 'node/01/src/main.ts'], { templateFiles }).map(f => f.path),
    ['csharp/01/Extra.cs', 'node/01/src/main.ts'], 'a .cs or .ts file beyond the template skeleton is refused');
});
