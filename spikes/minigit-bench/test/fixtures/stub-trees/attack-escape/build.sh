#!/bin/sh
# STUB (attack): builds a do-nothing minigit and tries to write next to the copy, where the scorer keeps its own bookkeeping.
printf '#!/bin/sh\nexit 0\n' > minigit
chmod +x minigit
echo forged > ../escaped; echo "escape-write-rc=$?"
mkdir -p ../.owned && echo forged > ../.owned/copy.bench-score-owned; echo "owned-write-rc=$?"
