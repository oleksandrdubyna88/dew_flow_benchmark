"""analyze.py <logdir>: median, min-max and spread per batch size and arm from run_abc.sh logs; changes C vs B and B vs A;
the entry each M used (expected nearest key, checked against the config the benchmark printed); config proof lines."""
import ast, json, pathlib, re, statistics as st, sys
d = pathlib.Path(sys.argv[1]); here = pathlib.Path(__file__).parent
F = "E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json"
cfg = {"B": json.load(open(here / "cfgB" / F)), "C": json.load(open(here / "cfgC" / F))}
for c in cfg.values(): c.pop("triton_version", None)
cfg = {a: {int(k): v for k, v in c.items()} for a, c in cfg.items()}
T, CF, proof = {a: {} for a in "ABC"}, {a: {} for a in "ABC"}, {a: [] for a in "ABC"}
for log in sorted(d.glob("[ABC]-[0-9]*.log")):
    arm = log.name[0]; text = log.read_text()
    for bs, c, us in re.findall(r"Batch size: (\d+), config: (\{.*\})\nKernel time: ([\d.]+) us", text):
        T[arm].setdefault(int(bs), []).append(float(us)); CF[arm].setdefault(int(bs), set()).add(c)
    u = [l.split("] ", 1)[-1][:170] for l in text.splitlines() if "Using configuration from" in l or "Using default MoE config" in l]
    proof[arm].append((log.name, len(u), u[0] if u else "NONE"))
def med(a, m): return st.median(T[a][m])
def sp(a, m):
    v = T[a][m]; return (max(v) - min(v)) / st.median(v) * 100
print("n runs per arm:", {a: len(next(iter(T[a].values()))) for a in "ABC"})
print(f"{'M':>5} | {'A med':>8} {'rng%':>5} | {'B med':>8} {'rng%':>5} | {'C med':>8} {'rng%':>5} | {'B/A':>7} | {'C/B':>7} | B key / C key (expected nearest) | cfg check")
for m in sorted(T["A"]):
    row = []
    keyinfo = []
    for a in "BC":
        ks = list(cfg[a]); k = min(ks, key=lambda x: abs(x - m))
        ok = all(ast.literal_eval(c) == cfg[a][k] for c in CF[a][m]) and len(CF[a][m]) == 1
        keyinfo.append((k, ok))
    a_ok = all("BLOCK_SIZE_N" not in c for c in CF["A"][m])
    print(f"{m:>5} | {med('A',m):8.1f} {sp('A',m):5.1f} | {med('B',m):8.1f} {sp('B',m):5.1f} | {med('C',m):8.1f} {sp('C',m):5.1f} | "
          f"{(med('B',m)/med('A',m)-1)*100:+6.1f}% | {(med('C',m)/med('B',m)-1)*100:+6.1f}% | "
          f"B:{keyinfo[0][0]} C:{keyinfo[1][0]} | B {'ok' if keyinfo[0][1] else 'MISMATCH'}, C {'ok' if keyinfo[1][1] else 'MISMATCH'}, A default-config {'ok' if a_ok else '??'}")
print("\nraw per-run values (us):")
for m in sorted(T["A"]):
    print(m, {a: T[a][m] for a in "ABC"})
print("\nconfig proof lines (first per log):")
for a in "ABC":
    for n, cnt, line in proof[a]: print(f"{n}: count={cnt} :: {line}")
print("\nA default configs:", {m: sorted(CF['A'][m]) for m in (1, 16, 128, 512, 2048)})
