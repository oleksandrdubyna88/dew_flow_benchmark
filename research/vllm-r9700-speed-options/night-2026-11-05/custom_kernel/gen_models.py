"""Generate the tiny random target (Qwen3) and EAGLE draft (Llama) used by custom_kernel_check.py.

Run inside the 0.29 image (CPU only):  python3 gen_models.py <out_dir>
Fixed seed, fp16 weights. Head size 128, 8 query heads, 2 KV heads (ratio 4), hidden 1024, 2 target layers,
1 draft layer. The tokenizer files are copied from JunHowie/Qwen3-0.6B-GPTQ-Int4 in the hf-cache volume.
"""
import glob
import json
import os
import shutil
import sys

import torch
from safetensors.torch import save_file
from transformers import LlamaConfig, Qwen3Config, Qwen3ForCausalLM

out = sys.argv[1]
SEED = 1234
VOCAB = 151936
HID, NH, NKV, HD, INTER = 1024, 8, 2, 128, 2048
snap = glob.glob("/root/.cache/huggingface/hub/models--JunHowie--Qwen3-0.6B-GPTQ-Int4/snapshots/*")[0]
TOK_FILES = ["tokenizer.json", "tokenizer_config.json", "vocab.json", "merges.txt",
             "special_tokens_map.json", "added_tokens.json", "chat_template.jinja"]


def copy_tok(dst):
    for f in TOK_FILES:
        if os.path.exists(os.path.join(snap, f)):
            shutil.copy(os.path.join(snap, f), dst)


# ---- target: Qwen3ForCausalLM, 2 layers
torch.manual_seed(SEED)
tdir = os.path.join(out, "target")
os.makedirs(tdir, exist_ok=True)
tcfg = Qwen3Config(
    vocab_size=VOCAB, hidden_size=HID, intermediate_size=INTER, num_hidden_layers=2,
    num_attention_heads=NH, num_key_value_heads=NKV, head_dim=HD, max_position_embeddings=4096,
    tie_word_embeddings=True, rms_norm_eps=1e-6, rope_theta=1000000.0,
)
model = Qwen3ForCausalLM(tcfg)
with torch.no_grad():
    for name, p in model.named_parameters():
        if p.dim() == 2 and "embed_tokens" in name:
            p.normal_(0.0, 0.05)
        elif p.dim() == 2:
            p.normal_(0.0, 0.02)
        else:
            p.fill_(1.0)  # RMSNorm weights
model = model.half()
model.save_pretrained(tdir, safe_serialization=True)
copy_tok(tdir)
embed = model.model.embed_tokens.weight.detach().clone()

# ---- draft: EAGLE-1 head with a Llama layer (vLLM maps LlamaForCausalLM -> EagleLlamaForCausalLM).
# Tensor names are the checkpoint's own (the loader adds "model."): no input_layernorm in layer 0, no final norm,
# no lm_head (the target's is shared). embed_tokens == the target's, so vLLM shares it.
ddir = os.path.join(out, "draft")
os.makedirs(ddir, exist_ok=True)
g = torch.Generator().manual_seed(SEED + 1)


def w(*shape, std=0.02):
    return (torch.randn(*shape, generator=g) * std).half()


tensors = {
    "embed_tokens.weight": embed,
    "fc.weight": w(HID, 2 * HID),
    "layers.0.self_attn.q_proj.weight": w(NH * HD, HID),
    "layers.0.self_attn.k_proj.weight": w(NKV * HD, HID),
    "layers.0.self_attn.v_proj.weight": w(NKV * HD, HID),
    "layers.0.self_attn.o_proj.weight": w(HID, NH * HD),
    "layers.0.mlp.gate_proj.weight": w(INTER, HID),
    "layers.0.mlp.up_proj.weight": w(INTER, HID),
    "layers.0.mlp.down_proj.weight": w(HID, INTER),
    "layers.0.post_attention_layernorm.weight": torch.ones(HID).half(),
}
save_file(tensors, os.path.join(ddir, "model.safetensors"), metadata={"format": "pt"})
dcfg = LlamaConfig(
    vocab_size=VOCAB, hidden_size=HID, intermediate_size=INTER, num_hidden_layers=1,
    num_attention_heads=NH, num_key_value_heads=NKV, head_dim=HD, max_position_embeddings=4096,
    rms_norm_eps=1e-6, rope_theta=1000000.0, tie_word_embeddings=False, torch_dtype="float16",
)
dcfg.architectures = ["LlamaForCausalLM"]
dcfg.save_pretrained(ddir)
copy_tok(ddir)
print(json.dumps({"target": sorted(os.listdir(tdir)), "draft": sorted(os.listdir(ddir))}, indent=1))
