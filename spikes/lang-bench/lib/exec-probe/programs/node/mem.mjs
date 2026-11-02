// The execution probe's Node program (E1.S8b): a toy, never a task of the series. One argument, the mode: report, culture, churn, overlimit.
import fs from 'node:fs';
import v8 from 'node:v8';

const MIB = 1024 * 1024;
const modes = {
  report() {
    console.log(JSON.stringify({ constrainedBytes: process.constrainedMemory(), heapSizeLimitBytes: v8.getHeapStatistics().heap_size_limit, cgroupVisible: fs.existsSync('/sys/fs/cgroup') }));
  },
  culture() {
    console.log(Intl.DateTimeFormat().resolvedOptions().locale);
  },
  churn() { // bounded live data (32 MiB), 4 GiB allocated over the run
    const live = new Uint8Array(32 * MIB).fill(1);
    for (let i = 0; i < 4096; i += 1) live[i % live.length] = new Uint8Array(MIB).fill(i)[0];
    console.log('OK');
  },
  overlimit() { // live data that only grows, to 3 GiB
    const kept = [];
    for (let i = 0; i < 48; i += 1) kept.push(new Uint8Array(64 * MIB).fill(1));
    console.log('OK');
  },
};

modes[process.argv[2] ?? 'report']();
