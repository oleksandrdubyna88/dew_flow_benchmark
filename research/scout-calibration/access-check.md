# Scout access check — 2026-10-13 17:58 UTC

- claude 2.1.289 · plugin cache 217b1ff2dbbb · pinned settings 916c89ebba32
- launch: --setting-sources "" --settings <pinned-settings.json> --plugin-dir <pinned feature-dev> beside the §3.4 scout launch (the allow-list, MCP off, the forwarded children, the cap)
- targets: sibling, grant[0], mirror-root, junction
- granted directories: 1 (named by index, never by path)

| target | feature-dev:code-explorer | Explore |
|---|---|---|
| sibling | denied — Read denied, Glob denied, Grep denied | denied — Read denied, Glob denied, Grep denied |
| grant[0] | denied — Read denied, Glob denied, Grep denied | denied — Read denied, Glob denied, Grep denied |
| mirror-root | denied — Read denied, Glob denied, Grep denied | denied — Read denied, Glob denied, Grep denied |
| junction | denied — Read denied, Glob denied, Grep denied | denied — Read denied, Glob denied, Grep denied |

- feature-dev:code-explorer: $0.14526450000000002, 5.0 s
- Explore: $0.11115270000000001, 25.9 s

every target denied for every agent — the launch drops the grant
