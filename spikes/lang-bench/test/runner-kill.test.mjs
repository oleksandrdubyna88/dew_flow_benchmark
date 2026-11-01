// No group signal to a reaped leader, an interrupt keeps its exit, the group scan is cheap, the scope fallback reaps and refuses a vanished cgroup (E1.S8a part ii-a round 4: N8, N9, D0, D1, D4, D6)
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_kill.py');
