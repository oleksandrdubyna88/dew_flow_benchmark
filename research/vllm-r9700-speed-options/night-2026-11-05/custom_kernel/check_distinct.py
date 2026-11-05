"""Pre-flight for model selection only: greedy-generate the 12 check prompts with HF transformers (CPU, fp32) and
print the distinct token count per request. Not part of the check itself."""
import sys

import numpy as np
import torch
from transformers import AutoModelForCausalLM

PHASES = [[240], [500], [240, 500, 100, 255], [17, 129, 240, 256, 500, 777]]
m = AutoModelForCausalLM.from_pretrained(sys.argv[1], torch_dtype=torch.float32).eval()
d = []
for pi, lens in enumerate(PHASES):
    for i, n in enumerate(lens):
        ids = np.random.RandomState(20261105 + 1000 * pi + i).randint(100, 50000, size=n).tolist()
        with torch.no_grad():
            out = m.generate(torch.tensor([ids]), max_new_tokens=64, min_new_tokens=64, do_sample=False)
        d.append(len(set(out[0, n:].tolist())))
print("DISTINCT", sys.argv[1], d, "min", min(d), flush=True)
