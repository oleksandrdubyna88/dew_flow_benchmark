// The runner stopped by a signal while a launcher starts or a case runs in a real scope, and the command line end to end through a real scope (E1.S8a part ii-a)
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_interrupt.py');
