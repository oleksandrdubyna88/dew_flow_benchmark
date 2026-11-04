import { text } from 'node:stream/consumers';

const input = await text(process.stdin);
for (const line of input.split('\n').filter(part => part !== '')) {
  process.stdout.write(`hello ${line}\n`);
}
