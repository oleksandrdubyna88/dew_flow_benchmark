#!/usr/bin/env bash
# v2 models (restricted lm_head), outputs in out2/. Usage: run_check2.sh [A B C Be Ce Cs Cm compare compare_s compare_m]
# default: A B C Be Ce Cs Cm compare compare_s compare_m.  Cs = sabotage seqlens (eager C), Cm = sabotage max_seq_len (eager C).
# Runs beside production (container `vllm`); never stops, restarts or execs into it.
set -u
S="$(cd "$(dirname "$0")" && pwd)"; W="/mnt/c/Program Files/WSL/wslc.exe"
IMG=rocm/vllm:rocm10.1.0_ubuntu24.04_py3.14-pytorch_2.13.0_vllm-0.29.0
WT='C:\Users\strug\vllm\tmp\vllm-pr-work\work\optin-test'
R=/opt/python/lib/python3.14/site-packages/vllm/v1/attention/backends/rocm_attn.py
UTIL="${GPU_UTIL:-0.08}"
LOGS="$S/${CK_OUT:-out2}"; mkdir -p "$LOGS"
arms=("$@"); [ ${#arms[@]} -eq 0 ] && arms=(A B C Ae Be Ce Cs Ch As Ah Cm compare)
echo "== production ready? (count can be stale, so also require Up)"
cnt=$("$W" logs vllm 2>&1 | grep -c 'Application startup complete'); echo "startup-complete count: $cnt"
"$W" list | cat
[ "$cnt" -ge 1 ] && "$W" list | tr -d "\r" | grep -E ' vllm *$' | grep -q ' Up ' || { echo "STOP: production not ready"; exit 3; }
"$S/disk_gate.sh" baseline || exit 3
rc=0
for arm in "${arms[@]}"; do
  if [[ "$arm" == compare* ]]; then
    case $arm in compare) cf="";; compare_s) cf="--compare-sabotage seqlens";; compare_h) cf="--compare-sabotage seqlens_half";; compare_m) cf="--compare-sabotage maxseq";; esac
    cmd="python3 custom_kernel_check.py --compare --v2 $cf"; name=vllmprtest-ck2-$arm; m=(); extra=(); gpu=()
  else
    case $arm in A) flags="--arm A";; B) flags="--arm B --expect-fused 0";; C) flags="--arm C --expect-fused 1";;
      Ae) flags="--arm A --eager";; As) flags="--arm A --eager --sabotage seqlens";; Ah) flags="--arm A --eager --sabotage seqlens_half";;
      Be) flags="--arm B --expect-fused 0 --eager";; Ce) flags="--arm C --expect-fused 1 --eager";;
      Cs) flags="--arm C --expect-fused 1 --eager --sabotage seqlens";; Ch) flags="--arm C --expect-fused 1 --eager --sabotage seqlens_half";; Cm) flags="--arm C --expect-fused 1 --eager --sabotage maxseq";; esac
    cmd="python3 custom_kernel_check.py $flags --v2 --gpu-util $UTIL"; name=vllmprtest-ck2-$arm
    m=(); { [ "$arm" = C ] || [ "$arm" = Ce ] || [ "$arm" = Cs ] || [ "$arm" = Ch ] || [ "$arm" = Cm ]; } && m=(-v "$WT\\mounts\\rocm_attn_029_optin.py:$R:ro")
    gpu=(--gpus all)
  fi
  echo "===== arm $arm"
  echo "CMD: \"$W\" run --rm --name $name ${gpu[*]:-} --shm-size 4g -e HIP_VISIBLE_DEVICES=0 -e HF_HUB_OFFLINE=1 -e VLLM_ENABLE_V1_MULTIPROCESSING=0 -e PYTHONHASHSEED=0 -v hf-cache:/root/.cache/huggingface ${m[*]:-} -v \"$WT:/t\" -w /t $IMG sh -c 'nice -n 19 $cmd'"
  timeout 3000 "$W" run --rm --name "$name" "${gpu[@]}" --shm-size 4g -e HIP_VISIBLE_DEVICES=0 -e HF_HUB_OFFLINE=1 \
    -e VLLM_ENABLE_V1_MULTIPROCESSING=0 -e PYTHONHASHSEED=0 -e CK_MODELS=${CK_MODELS:-models2} -e CK_OUT=${CK_OUT:-out2} -e PYTHONPYCACHEPREFIX=/tmp/pyc-fresh -e PYTHONDONTWRITEBYTECODE=1 \
    -v "hf-cache:/root/.cache/huggingface" "${m[@]}" -v "$WT:/t" -w /t $IMG sh -c "nice -n 19 $cmd" 2>&1 | tr -d '\r' | tee "$LOGS/log_$arm.txt" | grep -E "^(acceptance|distinct|sabotaged|assertion|NEGATIVE|COMPARE|--- |vllm.__file__|rocm_attn.__file__|RocmAttentionMetadataBuilder|LLM kwargs|probe:|phase |SUMMARY|RESULT|FAIL|arm |COMPARE|  DIFF)|Traceback|Error" 
  echo "arm $arm exit status ${PIPESTATUS[0]}"
  [ "${PIPESTATUS[0]}" -ne 0 ] && rc=1
  "$S/disk_gate.sh" check || { echo "STOP: disk gate"; exit 4; }
  echo "production still: $("$W" list | tr -d "\r" | grep -E ' vllm *$' | grep -o 'Up [0-9a-z ]*')"
done
exit $rc
