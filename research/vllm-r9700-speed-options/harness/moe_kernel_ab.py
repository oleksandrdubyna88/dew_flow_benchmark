"""moe_kernel_ab.py <outdir>: median kernel time per batch size and arm from moe_kernel_ab.sh logs, and the decision."""
import pathlib
import re
import statistics
import sys

out = pathlib.Path(sys.argv[1])
times: dict[str, dict[int, list[float]]] = {"default": {}, "tuned": {}}
configs: dict[str, dict[int, str]] = {"default": {}, "tuned": {}}
for log in sorted(out.glob("*-[123].log")):
    arm = log.stem.rsplit("-", 1)[0]
    text = log.read_text()
    for bs, cfg, us in re.findall(r"Batch size: (\d+), config: (.*)\nKernel time: ([\d.]+) us", text):
        times[arm].setdefault(int(bs), []).append(float(us))
        configs[arm][int(bs)] = cfg
gain = {}
print("bs  default_us  tuned_us  change   (median of reps)")
for bs in sorted(times["default"]):
    d, t = statistics.median(times["default"][bs]), statistics.median(times["tuned"].get(bs, [float("nan")]))
    gain[bs] = (t - d) / d * 100
    print(f"{bs:>2}  {d:10.2f}  {t:8.2f}  {gain[bs]:+6.1f} %   n={len(times['default'][bs])}/{len(times['tuned'].get(bs, []))}")
for arm in configs:
    for bs in (1, 4):
        print(f"{arm} bs {bs} config: {configs[arm].get(bs)}")
ok = all(gain.get(bs, 0) < -3 for bs in (1, 4))
print(f"B1 KERNEL: {'faster' if ok else 'not faster'}")
