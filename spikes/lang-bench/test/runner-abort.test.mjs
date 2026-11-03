// A runtime's own out-of-memory abort is `memory limit` (plan change 5.1; D15's OP-K): .NET's `Out of memory.` and V8's `JavaScript heap out of memory`, each with exit status 134, never from 134 alone (E1.S8a, fixed forward in E1.S8b).
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_abort.py');
