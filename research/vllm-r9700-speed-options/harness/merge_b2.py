"""merge_b2.py <tune_dir> <out_dir> [chunk ...]: phase B2 merge — int4 keys 24, 32, 64, 128 added to the production int4 file.

Reads <tune_dir>/merged (keys 1-16, 256, 1024, 2048) and the chunks named (default: the four full-search
int4-bs-<n> chunks; on 2026-11-01 the operator chose the coarse chunk int4-coarse-mid instead); same checks as merge_moe.py
(missing chunk, duplicate key, triton_version mismatch, missing fields). Writes the int4 file to out_dir and copies
nothing else, so a server pointed at out_dir sees only this file (the BF16 draft file failed in § 13.2).
"""
import json
import pathlib
import sys

CHUNKS = ["int4-bs-24", "int4-bs-32", "int4-bs-64", "int4-bs-128"]
EXPECTED = {1, 2, 4, 8, 12, 16, 24, 32, 64, 128, 256, 1024, 2048}
FIELDS = {"BLOCK_SIZE_M", "BLOCK_SIZE_N", "BLOCK_SIZE_K", "GROUP_SIZE_M", "num_warps", "num_stages"}
NAME = "E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json"


def main(tune_dir: pathlib.Path, out_dir: pathlib.Path, chunks: list[str]) -> None:
    sources = [("merged", tune_dir / "merged" / NAME)] + [(c, tune_dir / c / NAME) for c in chunks]
    triton_version = None
    configs: dict[int, dict] = {}
    for chunk, path in sources:
        data = json.loads(path.read_text())
        version = data.pop("triton_version")
        if triton_version not in (None, version):
            sys.exit(f"triton_version mismatch in {chunk}: {version} vs {triton_version}")
        triton_version = version
        for key, cfg in data.items():
            bs = int(key)
            if bs in configs:
                sys.exit(f"duplicate key {bs} in {chunk}")
            if not FIELDS <= cfg.keys():
                sys.exit(f"key {bs} in {chunk} misses {sorted(FIELDS - cfg.keys())}")
            configs[bs] = cfg
    if set(configs) != EXPECTED:
        sys.exit(f"keys {sorted(configs)} != expected {sorted(EXPECTED)}")
    merged = {"triton_version": triton_version} | {str(bs): configs[bs] for bs in sorted(configs)}
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / NAME).write_text(json.dumps(merged, indent=4) + "\n")
    print(f"wrote {out_dir / NAME}: triton {triton_version}, keys {sorted(configs)}")


if __name__ == "__main__":
    main(pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3:] or CHUNKS)
