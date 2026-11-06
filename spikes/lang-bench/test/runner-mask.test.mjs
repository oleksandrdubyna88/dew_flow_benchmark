// The signal mask of a launched program: the runner's deferred stopping signals are not passed on to it (rs_launch `_unblock_all`). One case starts a small python child.
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_launch_mask.py');
