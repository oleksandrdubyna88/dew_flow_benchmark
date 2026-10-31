// The ONE list of hosts a session's box may reach through the host-side CONNECT proxy (parent D22), per arm.
// Exact names, no suffixes. Imported by the egress proxy (E2.S2) and by the contamination scan's `web` rule (E2.S1):
// one source, so the prevention and the detection cannot drift apart.
//
// `api.anthropic.com` is in no arm's list: the API leaves the box through the credential broker only (parent D23).
// `raw.githubusercontent.com` is in none either: the contamination scan names it as a foreign read.
const GITHUB = Object.freeze(['github.com', 'api.github.com', 'codeload.github.com']);
const BARE = Object.freeze(['registry.npmjs.org', 'api.nuget.org']);

/** Hosts reachable through the proxy, by arm. */
export const EGRESS_HOSTS = Object.freeze({
  bare: BARE,
  process: Object.freeze([...BARE, ...GITHUB]),
});

/** Hosts the CLI is known to try and is refused, without effect on the session (the API goes through the broker). */
export const KNOWN_REFUSED = Object.freeze(['api.anthropic.com', 'http-intake.logs.us5.datadoghq.com']);
