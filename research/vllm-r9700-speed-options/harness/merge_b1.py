"""merge_b1.py <tune_dir> <out_dir>: phase B1 merge — the five BF16 draft chunks into one fused-MoE config file.

Same checks as merge_moe.py (missing chunk, duplicate key, triton_version mismatch, missing fields). out_dir also
gets a copy of the production int4 file from <tune_dir>/merged, so a server pointed at out_dir keeps that file.
"""
import json
import pathlib
import shutil
import sys

CHUNKS = ["draft-coarse-largeM", "draft-bs-1", "draft-bs-2", "draft-bs-4", "draft-bs-12"]
EXPECTED = {1, 2, 4, 12, 256, 1024, 2048}
FIELDS = {"BLOCK_SIZE_M", "BLOCK_SIZE_N", "BLOCK_SIZE_K", "GROUP_SIZE_M", "num_warps", "num_stages"}
NAME = "E=256,N=512,device_name=AMD_Radeon_R9700.json"
INT4 = "E=256,N=512,device_name=AMD_Radeon_R9700,dtype=int4_w4a16.json"


def main(tune_dir: pathlib.Path, out_dir: pathlib.Path) -> None:
    triton_version = None
    configs: dict[int, dict] = {}
    for chunk in CHUNKS:
        data = json.loads((tune_dir / chunk / NAME).read_text())
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
    shutil.copyfile(tune_dir / "merged" / INT4, out_dir / INT4)
    print(f"wrote {out_dir / NAME}: triton {triton_version}, keys {sorted(configs)}; copied {INT4}")


if __name__ == "__main__":
    main(pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]))
