# Packages

TypeScript 6.0.3 (`tsc`) is installed as part of the toolchain, and Node 26.12.1 runs the program. The one package you can
install is `@types/node` {TYPES_NODE}, from the local offline cache (`.npmrc` sets `offline=true`); `bash build.sh` restores it.
No other package is available. The standard library of Node is all the program needs.
