const fs = require('fs'); const E = process.argv[2]; const base = fs.readFileSync(`${E}/briefs/1-code.template.md`, 'utf8');
const RED = "Watch each test fail first, and compare the failure with the table's last column (when it\ndiffers, say why in your notes). Then make it pass.";
const PARA = '## How you write code in this run\n\n{{ARM_PARAGRAPH}}';
const FINAL = /Your final answer: a short report[\s\S]*$/;
for (const x of [RED, PARA]) if (!base.includes(x)) { console.error('anchor missing: ' + x.slice(0, 50)); process.exit(1); }
const para = (f) => fs.readFileSync(`${E}/briefs/${f}`, 'utf8').trim();
const skeleton = base.replace('You are the CODER (Sonnet)', 'You are the CODER (Sonnet), in the SKELETON step,')
  .replace(RED, 'In THIS step you do not make the tests pass: you write the skeleton described below.\nThe red and green checks come in a later step.')
  .replace(PARA, '## How you write code in this run — the skeleton\n\n' + para('q-skeleton.para.md'))
  .replace(FINAL, 'Your final answer: a short report — the regions you made (id, kind, file), the build result, and your decisions.\n');
const polish = base.replace('You are the CODER (Sonnet)', 'You are the CODER (Sonnet), in the POLISH step,')
  .replace(RED, 'The bodies are now filled. Make every scenario test pass, after checking that it fails first for\nthe right reason (below).')
  .replace(PARA, '## How you write code in this run — the polish\n\n' + para('q-polish.para.md'));
fs.writeFileSync(`${E}/briefs/q1-skeleton.template.md`, skeleton);
fs.writeFileSync(`${E}/briefs/q3-polish.template.md`, polish);
console.log('written', skeleton.length, polish.length);
