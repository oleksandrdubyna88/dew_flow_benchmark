// The invented programs of the real-box tests (R1, R2, M7), laid on the REAL daemon templates of lang-bench (`templates/<stack>/daemon/`): a TCP daemon that echoes `echo:<word>`, that answers `oops:<word>` instead when its own
// `Cpus_allowed_list` is exactly CPUs 6 and 7 (R1; the same five bytes, a different answer), and that on the words `boom` and `boomobj` allocates until the memory limit stops it (M7: typed buffers, and ordinary objects). One C# source and
// one TypeScript source; the C# one is BUILT by the template's own `build.sh` in the build box. No task, no reference, nothing of the held-out side.
import { cpSync, mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const TEMPLATES = fileURLToPath(new URL('../../lang-bench/templates/', import.meta.url));

export const NODE_DAEMON_TS = `import net from 'node:net';
import { readFileSync } from 'node:fs';

function cpus(): Set<number> {
  const listed = /^Cpus_allowed_list:\\s*(\\S+)/m.exec(readFileSync('/proc/self/status', 'utf8'))?.[1] ?? '';
  const found = new Set<number>();
  for (const part of listed.split(',')) {
    const ends = part.split('-').map(Number);
    for (let n = Number(ends[0]); n <= Number(ends.at(-1)); n += 1) found.add(n);
  }
  return found;
}
const onTwo = (): boolean => { const c = cpus(); return c.size === 2 && c.has(6) && c.has(7); };
const WRONG_ON_TWO = process.env['WRONG_ON_TWO'] === '1';
const hold: unknown[] = [];

const server = net.createServer((socket) => {
  socket.on('data', (data: Buffer) => {
    const word = data.toString('latin1');
    if (word === 'boom') for (;;) { const b = Buffer.alloc(16 * 1024 * 1024); b.fill(1); hold.push(b); }
    if (word === 'boomobj') for (;;) hold.push(Array.from({ length: 1024 * 1024 }, () => ({})));
    socket.write(Buffer.from((WRONG_ON_TWO && onTwo() ? 'oops:' : 'echo:') + word, 'latin1'));
  });
  socket.on('error', () => {});
});
server.listen(Number(process.env['PORT']), '127.0.0.1');
process.on('SIGTERM', () => { server.close(); process.exit(0); });
`;

export const CSHARP_DAEMON_CS = `using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

static bool OnTwo()
{
    var line = File.ReadAllLines("/proc/self/status").First(l => l.StartsWith("Cpus_allowed_list:"));
    var found = new HashSet<int>();
    foreach (var part in line.Split(':')[1].Trim().Split(','))
    {
        var ends = part.Split('-').Select(int.Parse).ToArray();
        for (var n = ends[0]; n <= ends[^1]; n++) found.Add(n);
    }
    return found.Count == 2 && found.Contains(6) && found.Contains(7);
}

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

var wrongOnTwo = Environment.GetEnvironmentVariable("WRONG_ON_TWO") == "1";
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
                // a thread of its own: an exception in a task would be swallowed and the program would sit at its heap limit; on a thread it ends the process, as in the probe
                if (word == "boom") new Thread(Boom).Start();
                if (word == "boomobj") new Thread(BoomObjects).Start();
                await stream.WriteAsync(Encoding.Latin1.GetBytes((wrongOnTwo && OnTwo() ? "oops:" : "echo:") + word));
            }
        });
    }
});
done.Wait();
return 0;
`;

/** Lay the daemon template of `stack` and the invented program into `dest`. The Node tree needs no build (the template's `run.sh` starts `src/main.ts`); the C# tree is built by the template's `build.sh`. */
export function layDaemonTree({ stack, dest }) {
  cpSync(path.join(TEMPLATES, stack, 'daemon'), dest, { recursive: true });
  if (stack === 'node') {
    mkdirSync(path.join(dest, 'src'), { recursive: true });
    writeFileSync(path.join(dest, 'src', 'main.ts'), NODE_DAEMON_TS);
  } else {
    writeFileSync(path.join(dest, 'Program.cs'), CSHARP_DAEMON_CS);
  }
}

/** The scenario: send `word`, read the reply of `5 + word.length` bytes, stop the program with TERM. */
export const echoScenario = (word, { wrongOn26 = false } = {}) => `scenario 2
default within 2s${wrongOn26 ? '\nenv WRONG_ON_TWO "1"' : ''}
start ready port within 10s
connect c tcp
send c "${word}"
recv c bytes ${5 + word.length}
signal TERM
wait-exit within 5s
`;
export const echoTranscript = word => `transcript 2\nready\nrecv c "echo:${word}"\nexit 0\n`;

/** The scenario that blows the limit: the program is stopped by the memory limit, whatever else the transcript says. */
export const boomScenario = word => `scenario 2
default within 2s
start ready port within 10s
connect c tcp
send c "${word}"
sleep 15s
signal TERM
wait-exit within 60s
`;
export const boomTranscript = 'transcript 2\nready\nexit 137\n';
