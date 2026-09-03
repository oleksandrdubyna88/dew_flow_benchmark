// Variables for the `.http` contract suite.
//
// TWO hosts, deliberately: the read API and the collector are separate processes because the
// collector is the one an agent's hook may reach, and a store that can write has no business in the
// host that serves reads. So the suite addresses two origins — `baseUrl` (which `--target` sets) and
// `collectorUrl`, which has its own variable because it is its own deployment.
//
// Nothing here authenticates: both hosts bind loopback and are spoken to by the machine they run on.

module.exports = {
  environments: {
    local: {
      baseUrl: process.env.BENCH_BASE_URL ?? 'http://127.0.0.1:5411',
      collectorUrl: process.env.BENCH_COLLECTOR_URL ?? 'http://127.0.0.1:5177',

      // A commit sha is 40 hex characters and abbreviations are refused — the value below is a
      // well-formed sha that names nothing, which is exactly what a planning request needs.
      wellFormedSha: '0123456789abcdef0123456789abcdef01234567',
      unknownRunId: '00000000-0000-0000-0000-000000000000',
    },
  },
};
