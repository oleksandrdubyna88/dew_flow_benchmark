// The invented programs of the package tool's real-box tests (F2.S1 scenarios 30 to 33 and the plan round's finding 1): a Node and a C# daemon laid on the REAL daemon templates, an echo of `echo:<word>` and, by option, one defect each: it
// answers `oops:<word>` on a named word, it answers wrongly only under the 2-CPU mask (`WRONG_ON_TWO`), it allocates without end on `boom` (typed buffers) or `boomobj` (ordinary objects), and on `spawn` it starts a detached grandchild that
// ignores TERM and then hangs. The reference of a package is the echo; a witness is the same program with its defect. Nothing of a task, a reference or the held-out side. Not a test file.
const JS_FUNCS = `function cpus(): Set<number> {
  const listed = /^Cpus_allowed_list:\\s*(\\S+)/m.exec(readFileSync('/proc/self/status', 'utf8'))?.[1] ?? '';
  const found = new Set<number>();
  for (const part of listed.split(',')) {
    const ends = part.split('-').map(Number);
    for (let n = Number(ends[0]); n <= Number(ends.at(-1)); n += 1) found.add(n);
  }
  return found;
}
const onTwo = (): boolean => { const c = cpus(); return c.size === 2 && c.has(6) && c.has(7); };
`;

/** The Node daemon (`src/main.ts`). `options`: `{ wrongWord, wrongOnTwo, boom, boomObj, grandchildMarker }`. */
export function nodeToy({ wrongWord = null, wrongOnTwo = false, boom = false, boomObj = false, grandchildMarker = null } = {}) {
  const blocks = [
    boom ? "    if (word === 'boom') for (;;) { const b = Buffer.alloc(16 * 1024 * 1024); b.fill(1); hold.push(b); }" : '',
    boomObj ? "    if (word === 'boomobj') for (;;) hold.push(Array.from({ length: 1024 * 1024 }, () => ({})));" : '',
    grandchildMarker ? `    if (word === 'spawn') { spawn(process.execPath, ['-e', 'process.on("SIGTERM", () => {}); setInterval(() => {}, 1000);', '${grandchildMarker}'], { detached: true, stdio: 'ignore' }).unref(); return; }` : '',
    wrongWord ? `    if (word === '${wrongWord}') { socket.write(Buffer.from('oops:' + word, 'latin1')); return; }` : '',
  ].filter(Boolean).join('\n');
  return `import net from 'node:net';
import { spawn } from 'node:child_process';
import { readFileSync } from 'node:fs';

${wrongOnTwo ? JS_FUNCS : ''}const WRONG_ON_TWO = process.env['WRONG_ON_TWO'] === '1';
const hold: unknown[] = [];
void spawn;
void readFileSync;
void WRONG_ON_TWO;

const server = net.createServer((socket) => {
  socket.on('data', (data: Buffer) => {
    const word = data.toString('latin1');
${blocks}
    socket.write(Buffer.from(${wrongOnTwo ? "(WRONG_ON_TWO && onTwo() ? 'oops:' : 'echo:')" : "'echo:'"} + word, 'latin1'));
  });
  socket.on('error', () => {});
});
server.listen(Number(process.env['PORT']), '127.0.0.1');
process.on('SIGTERM', () => { server.close(); process.exit(0); });
`;
}

/** The C# daemon (`Program.cs`). `options`: `{ wrongWord, boom, boomObj }`. */
export function csharpToy({ wrongWord = null, boom = false, boomObj = false } = {}) {
  return `using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

static void Boom()
{
    var hold = new List<byte[]>();
    while (true) { var b = new byte[16 * 1024 * 1024]; Array.Fill(b, (byte)1); hold.Add(b); }
}

static void BoomObjects()
{
    var hold = new List<object[]>();
    while (true) { var o = new object[1024 * 1024]; for (var i = 0; i < o.Length; i++) o[i] = new object(); hold.Add(o); }
}

var listener = new TcpListener(IPAddress.Loopback, int.Parse(Environment.GetEnvironmentVariable("PORT")!));
listener.Start();
var done = new ManualResetEventSlim();
PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; done.Set(); });
_ = Task.Run(async () =>
{
    while (true)
    {
        var client = await listener.AcceptTcpClientAsync();
        _ = Task.Run(async () =>
        {
            var stream = client.GetStream();
            var buf = new byte[65536];
            int n;
            while ((n = await stream.ReadAsync(buf)) > 0)
            {
                var word = Encoding.Latin1.GetString(buf, 0, n);
${boom ? '                if (word == "boom") { new Thread(Boom).Start(); continue; }' : ''}
${boomObj ? '                if (word == "boomobj") { new Thread(BoomObjects).Start(); continue; }' : ''}
${wrongWord ? `                if (word == "${wrongWord}") { await stream.WriteAsync(Encoding.Latin1.GetBytes("oops:" + word)); continue; }` : ''}
                await stream.WriteAsync(Encoding.Latin1.GetBytes("echo:" + word));
            }
        });
    }
});
done.Wait();
return 0;
`;
}

/** The program of `stack` as the file the template takes it in: `{ rel, text }`. */
export const toyFile = (stack, options) => (stack === 'node' ? { rel: 'src/main.ts', text: nodeToy(options) } : { rel: 'Program.cs', text: csharpToy(options) });

/** The scenario that sends `word` and expects the echo back; `wrongOnTwo` sets the env variable the Node toy reads. */
export const wordScenario = (word, { wrongOnTwo = false, within = 2 } = {}) => `scenario 2
default within ${within}s${wrongOnTwo ? '\nenv WRONG_ON_TWO "1"' : ''}
start ready port within 10s
connect c tcp
send c "${word}"
recv c bytes ${5 + word.length}
signal TERM
wait-exit within 5s
`;
export const wordTranscript = word => `transcript 2\nready\nrecv c "echo:${word}"\nexit 0\n`;

/** A scenario that sends `word` to a program that never answers (a hang): the receive step ends at its deadline. */
export const silentScenario = word => `scenario 2
default within 2s
start ready port within 10s
connect c tcp
send c "${word}"
recv c bytes ${5 + word.length}
signal TERM
wait-exit within 5s
`;
