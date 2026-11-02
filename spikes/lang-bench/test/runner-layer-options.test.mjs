// The layer's options as the command line takes them: what is refused (exit 4), what the host must have (exit 3), a cgroup the case does not own (exit 3) and SIGHUP (E1.S8a part ii-a; P1, P5)
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_layer_options.py');
