#!/bin/sh
# scope-entry <out-dir> <memory.max bytes> <holder seconds> <argv...> — the first process of the delegated scope (E1.S8b, D43). POSIX sh (dash), part of the layer's identity.
# Inside the scope's cgroup: make the leaves holder/ and prog/; park the scope holder (a sleep, bounded, holding NO inherited descriptor above 2: dash can close only 0-9, so bash closes them all) in holder/; move this shell into prog/;
# enable +memory +pids (the scope's own cgroup now holds no process, as cgroup2 requires); set memory.max, memory.swap.max=0 and pids.max=512 (a fork bomb stops there) in prog/; write the scope's cgroup path and the scope
# holder's pid to <out-dir>; exec the launch chain. Every move stays inside the delegated subtree. The scope holder keeps the scope, so prog/'s files are readable after the exit.
set -eu
out=$1 mem=$2 hold=$3
shift 3
cg=/sys/fs/cgroup$(sed -n 's/^0:://p' /proc/self/cgroup)
mkdir -p "$out"
mkdir "$cg/holder" "$cg/prog"
(exec bash -c 'for f in /proc/self/fd/*; do n=${f##*/}; if [ "$n" -gt 2 ] 2>/dev/null; then eval "exec $n>&-"; fi; done; exec sleep "$0"' "$hold") </dev/null >/dev/null 2>&1 &
holder=$!
echo "$holder" > "$cg/holder/cgroup.procs"
echo $$ > "$cg/prog/cgroup.procs"
echo "+memory +pids" > "$cg/cgroup.subtree_control"
echo "$mem" > "$cg/prog/memory.max"
echo 0 > "$cg/prog/memory.swap.max"
echo 512 > "$cg/prog/pids.max"
echo "$cg" > "$out/scope-cgroup"
echo "$holder" > "$out/holder.pid"
exec "$@"
