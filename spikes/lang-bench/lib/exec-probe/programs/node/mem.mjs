// The execution probe's Node program (E1.S8b): a toy, never a task of the series. One argument, the mode: report, culture, churn, overlimit.
import fs from 'node:fs';
import v8 from 'node:v8';
import vm from 'node:vm';

const MIB = 1024 * 1024;
const modes = {
  report() {
    console.log(JSON.stringify({ constrainedBytes: process.constrainedMemory(), heapSizeLimitBytes: v8.getHeapStatistics().heap_size_limit, cgroupVisible: fs.existsSync('/sys/fs/cgroup'), cgroupSelf: fs.readFileSync('/proc/self/cgroup', 'utf8').trim(), cgroupChildren: fs.existsSync('/sys/fs/cgroup') ? fs.readdirSync('/sys/fs/cgroup', { withFileTypes: true }).filter(e => e.isDirectory()).length : -1 }));
  },
  culture() {
    console.log(Intl.DateTimeFormat().resolvedOptions().locale);
  },
  churn() { // bounded live data (32 MiB), 4 GiB allocated over the run
    const live = new Uint8Array(32 * MIB).fill(1);
    for (let i = 0; i < 4096; i += 1) live[i % live.length] = new Uint8Array(MIB).fill(i)[0];
    console.log('OK');
  },
  'medium-buffers'(limitMiB) { // live typed arrays = 45% of the limit, kept to the end, plus 2 GiB of short-lived typed arrays
    const kept = [];
    for (let held = 0; held < (limitMiB * MIB * 45) / 100; held += MIB) kept.push(new Uint8Array(MIB).fill(1));
    for (let i = 0; i < 8192; i += 1) new Uint8Array(256 * 1024).fill(i);
    console.log(kept.length > 0 ? 'OK' : 'EMPTY');
  },
  'near-limit'(limitMiB) { // live typed arrays = 80% of the limit, kept to the end, plus 1 GiB of short-lived typed arrays
    const kept = [];
    for (let held = 0; held < (limitMiB * MIB * 80) / 100; held += MIB) kept.push(new Uint8Array(MIB).fill(1));
    for (let i = 0; i < 4096; i += 1) new Uint8Array(256 * 1024).fill(i);
    console.log(kept.length > 0 ? 'OK' : 'EMPTY');
  },
  'objects-70'(limitMiB) { // live ordinary objects = 70% of the limit by the heap after a full GC, kept to the end, plus 1 GiB of short-lived small objects
    v8.setFlagsFromString('--expose-gc');
    const gc = vm.runInNewContext('gc');
    const kept = [];
    const heap = () => { gc(); return process.memoryUsage().heapUsed; };
    while (heap() < (limitMiB * MIB * 70) / 100) for (let i = 0; i < 20000; i += 1) kept.push({ a: i, b: i * 2, c: i * 3, arr: [i, 1, 2, 3] });
    console.error(`live-heap-after-full-gc-MiB=${Math.round(heap() / MIB)}`);
    let sink = null;
    for (let i = 0; i < 11_000_000; i += 1) sink = { a: i, b: 1, c: 2, arr: [i, 1, 2, 3] };
    console.log(kept.length > 0 && sink !== null ? 'OK' : 'EMPTY');
  },
  mixed(limitMiB) { // an object graph at 45% of the limit plus typed arrays at 35% (both kept to the end), plus 1 GiB of short-lived allocations of both kinds
    v8.setFlagsFromString('--expose-gc');
    const gc = vm.runInNewContext('gc');
    const objects = [];
    const heap = () => { gc(); return process.memoryUsage().heapUsed; };
    while (heap() < (limitMiB * MIB * 45) / 100) for (let i = 0; i < 20000; i += 1) objects.push({ a: i, b: i * 2, c: i * 3, arr: [i, 1, 2, 3] });
    const bytes = [];
    for (let held = 0; held < (limitMiB * MIB * 35) / 100; held += MIB) bytes.push(new Uint8Array(MIB).fill(1));
    console.error(`live-heap-after-full-gc-MiB=${Math.round(heap() / MIB)}`);
    let sink = null;
    for (let i = 0; i < 5_500_000; i += 1) sink = { a: i, b: 1, c: 2, arr: [i, 1, 2, 3] };
    for (let i = 0; i < 2048; i += 1) new Uint8Array(256 * 1024).fill(i);
    console.log(objects.length > 0 && bytes.length > 0 && sink !== null ? 'OK' : 'EMPTY');
  },
  'medium-objects'(limitMiB) { // live ordinary objects = 40% of the limit by the heap after a full GC, kept to the end, plus 2 GiB of short-lived small objects
    v8.setFlagsFromString('--expose-gc');
    const gc = vm.runInNewContext('gc');
    const kept = [];
    const heap = () => { gc(); return process.memoryUsage().heapUsed; };
    while (heap() < (limitMiB * MIB * 40) / 100) for (let i = 0; i < 20000; i += 1) kept.push({ a: i, b: i * 2, c: i * 3, arr: [i, 1, 2, 3] });
    console.error(`live-heap-after-full-gc-MiB=${Math.round(heap() / MIB)}`);
    let sink = null;
    for (let i = 0; i < 22_000_000; i += 1) sink = { a: i, b: 1, c: 2, arr: [i, 1, 2, 3] };
    console.log(kept.length > 0 && sink !== null ? 'OK' : 'EMPTY');
  },
  overlimit() { // live data that only grows, to 3 GiB
    const kept = [];
    for (let i = 0; i < 48; i += 1) kept.push(new Uint8Array(64 * MIB).fill(1));
    console.log('OK');
  },
};

modes[process.argv[2] ?? 'report'](Number(process.argv[3] ?? 0));
