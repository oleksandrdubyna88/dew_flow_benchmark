import { parseArgs } from 'node:util';

/** Parse argv against `{name: {type, default?, help}}`; returns values, prints help text when asked. */
export function parseCli(argv, spec, usage) {
  const options = Object.fromEntries(Object.entries(spec).map(([name, o]) => [name, { type: o.type, default: o.default }]));
  options.help = { type: 'boolean' };
  const { values } = parseArgs({ args: argv, options, strict: true });
  if (values.help) return { help: helpText(spec, usage), values };
  return { help: '', values };
}

export function helpText(spec, usage) {
  const lines = Object.entries(spec).map(([name, o]) => `  --${name} <${o.type === 'boolean' ? 'flag' : 'value'}>  ${o.help ?? ''}${o.default === undefined ? '' : ` (default ${o.default})`}`);
  return `${usage}\n\nOptions:\n${lines.join('\n')}\n  --help  show this text\n`;
}

export function requireValues(values, names) {
  const missing = names.filter(n => values[n] === undefined || values[n] === '');
  if (missing.length > 0) throw new Error(`missing required option(s): ${missing.map(n => `--${n}`).join(', ')}`);
}
