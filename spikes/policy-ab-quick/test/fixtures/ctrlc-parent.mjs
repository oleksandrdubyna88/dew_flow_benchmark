// A parent that started one detached child through lib/proc.mjs and then idles: the shape of a harness at Ctrl-C.
import { run } from '../../lib/proc.mjs';

const mark = process.argv[2];
run('sleep', ['37'], { env: { ...process.env, CTRLC_MARK: mark }, timeoutMs: 60_000 });
setInterval(() => {}, 1000);
