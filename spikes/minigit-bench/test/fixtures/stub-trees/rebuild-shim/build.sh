#!/bin/sh
# STUB: a build whose output is a minigit; running it again REWRITES minigit (what a phony make target can do).
printf "#!/bin/sh\nexit 0\n" > minigit
chmod +x minigit
