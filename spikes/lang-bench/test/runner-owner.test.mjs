// A cgroup the runner can trust: cgroup2, the box's own, counters at zero, cgroup.kill never created, what cannot be read is alive (E1.S8a part ii-a round 4: N2, N3, N6, N7)
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_owner.py');
