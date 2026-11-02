#!/bin/sh
# scope-entry <out-dir> <memory.max bytes> <holder seconds> <argv...> — the first process of the delegated scope (E1.S8b, D43). POSIX sh (dash), part of the layer's identity.
# Inside the scope's cgroup: make the leaves holder/ and prog/; park the scope holder (a sleep, bounded, holding no descriptor of ours) in holder/; move this shell into prog/;
# enable +memory (the scope's own cgroup now holds no process, as cgroup2 requires); set memory.max and memory.swap.max=0 in prog/; write the scope's cgroup path and the scope
# holder's pid to <out-dir>; exec the launch chain. Every move stays inside the delegated subtree. The scope holder keeps the scope, so prog/'s files are readable after the exit.
set -eu
out=$1 mem=$2 hold=$3
shift 3
cg=/sys/fs/cgroup$(sed -n 's/^0:://p' /proc/self/cgroup)
mkdir -p "$out"
mkdir "$cg/holder" "$cg/prog"
(exec 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&-; exec sleep "$hold") </dev/null >/dev/null 2>&1 &
holder=$!
echo "$holder" > "$cg/holder/cgroup.procs"
echo $$ > "$cg/prog/cgroup.procs"
echo +memory > "$cg/cgroup.subtree_control"
echo "$mem" > "$cg/prog/memory.max"
echo 0 > "$cg/prog/memory.swap.max"
echo "$cg" > "$out/scope-cgroup"
echo "$holder" > "$out/holder.pid"
exec "$@"
