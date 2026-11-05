#!/usr/bin/env bash
# Red / green / red for the shared draft-decode tests, on the 0.29 image, device "cpu". Does not touch the `vllm` container.
set -u
W="/mnt/c/Program Files/WSL/wslc.exe"; IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work\work\optin-test'
R=/opt/python/lib/python3.14/site-packages/vllm/v1/attention/backends/rocm_attn.py
for arm in RED1 GREEN RED2; do
  m=(); [ "$arm" = GREEN ] && m=(-v "$WT\\mounts\\rocm_attn_029_optin.py:$R:ro")
  echo "===== $arm ($([ $arm = GREEN ] && echo 'rocm_attn.py with the opt-in mounted' || echo 'unchanged 0.29 image'))"
  nice -n 19 "$W" run --rm --name vllmprtest-unit-$arm --gpus all -e HIP_VISIBLE_DEVICES=0 \
    -e PYTHONPYCACHEPREFIX=/tmp/pyc-fresh -e PYTHONDONTWRITEBYTECODE=1 "${m[@]}" -v "$WT:/t" -w /t $IMG sh -c '
      uv pip install --system -q tblib==3.1.0 pytest-asyncio==1.4.0 pytest-timeout==2.3.1 >/dev/null 2>&1 || echo "uv install failed"
      python3 -c "
import vllm, vllm.v1.attention.backends.rocm_attn as r
print(\"vllm.__file__\", vllm.__file__, vllm.__version__)
print(\"rocm_attn.__file__\", r.__file__)
print(\"flag on RocmAttentionMetadataBuilder:\", r.RocmAttentionMetadataBuilder.supports_draft_decode_metadata_update)
"
      python3 -m pytest -p no:cacheprovider --no-header -q -rA --tb=short \
        tests/v1/attention/test_triton_attn_metadata_builder.py -k "draft_decode" 2>&1 | grep -v -i "warn" | tail -80' 2>&1 | tr -d '\r'
done
