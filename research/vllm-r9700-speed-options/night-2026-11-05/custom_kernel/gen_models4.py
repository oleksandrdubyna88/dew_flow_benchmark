"""v2 (restricted lm_head). Generate the tiny random target (Qwen3) and EAGLE draft (Llama) used by custom_kernel_check.py.

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
SEED = int(os.environ.get("GEN_SEED", "1234"))
# v3: peaky attention that dominates the residual stream, so the output depends on WHICH context tokens are attended.
# (v2's near-uniform attention averaged the values, which made the output insensitive to dropping a context token.)
QK_STD = float(os.environ.get("QK_STD", "0.02"))
V_STD = float(os.environ.get("V_STD", "0.02"))
O_STD = float(os.environ.get("O_STD", "0.02"))
EMB_STD = float(os.environ.get("EMB_STD", "0.05"))
ALPHA = float(os.environ.get("ALPHA", "1.5"))  # q_norm/k_norm weight: scales attention logits by ALPHA**2
K_PAIRS = int(os.environ.get("K_PAIRS", "2"))  # effective vocabulary = 2*K_PAIRS tokens
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
    tie_word_embeddings=False, rms_norm_eps=1e-6, rope_theta=1000000.0,
)
model = Qwen3ForCausalLM(tcfg)
with torch.no_grad():
    for name, p in model.named_parameters():
        if name == "lm_head.weight":
            continue  # set below
        if p.dim() == 2 and "embed_tokens" in name:
            p.normal_(0.0, EMB_STD)
        elif p.dim() == 2 and ("q_proj" in name or "k_proj" in name):
            p.normal_(0.0, QK_STD)
        elif p.dim() == 2 and "v_proj" in name:
            p.normal_(0.0, V_STD)
        elif p.dim() == 2 and "o_proj" in name:
            p.normal_(0.0, O_STD)
        elif p.dim() == 2:
            p.normal_(0.0, 0.02)
        else:
            p.fill_(1.0)  # RMSNorm weights
# v4: with Qwen3's q/k RMSNorm the q/k projection scale is irrelevant, so attention logits are set by the norm weights
# (ALPHA) and by the angle between q and k. KV head 0 (query heads 0-3): q = 0.5*k + 0.87*noise, so each token attends
# mostly to itself (dropping the last KV position changes the result). KV head 1 (query heads 4-7): independent random
# q/k with sharpened logits, so the result depends on WHICH earlier tokens win (paged lookups elsewhere in the context).
with torch.no_grad():
    for layer in model.model.layers:
        att = layer.self_attn
        wk0 = att.k_proj.weight[0:HD].clone()
        for h in range(4):
            att.q_proj.weight[h * HD:(h + 1) * HD] = 0.5 * wk0 + 0.866 * torch.randn_like(wk0) * wk0.std()
        att.q_norm.weight.fill_(ALPHA)
        att.k_norm.weight.fill_(ALPHA)
# Restricted effective vocabulary: lm_head is zero except 4 seeded, non-special tokens, held as +u, -u, +v, -v.
# Logits are then {a, -a, b, -b} on those rows and 0 elsewhere, so the greedy token is always one of the 4
# (max(|a|,|b|) > 0 beats the zero logits), and which of the 4 it is depends on the final hidden state, i.e. on the
# attention result. The draft has no lm_head of its own and shares this one, so the two pick from the same 4 tokens.
K_TOKENS = sorted(__import__("numpy").random.RandomState(99).choice(range(1000, 50000), size=2 * K_PAIRS, replace=False).tolist())
with torch.no_grad():
    g0 = torch.Generator().manual_seed(SEED + 7)
    head = torch.zeros(VOCAB, HID)
    for pi_ in range(K_PAIRS):
        vec = torch.randn(HID, generator=g0) * 0.05
        head[K_TOKENS[2 * pi_]] = vec
        head[K_TOKENS[2 * pi_ + 1]] = -vec
    model.lm_head.weight.copy_(head)
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
    "layers.0.self_attn.q_proj.weight": w(NH * HD, HID, std=QK_STD),
    "layers.0.self_attn.k_proj.weight": w(NKV * HD, HID, std=QK_STD),
    "layers.0.self_attn.v_proj.weight": w(NKV * HD, HID, std=V_STD),
    "layers.0.self_attn.o_proj.weight": w(HID, NH * HD, std=O_STD),
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
print("K_TOKENS", K_TOKENS)
print(json.dumps({"target": sorted(os.listdir(tdir)), "draft": sorted(os.listdir(ddir))}, indent=1))
