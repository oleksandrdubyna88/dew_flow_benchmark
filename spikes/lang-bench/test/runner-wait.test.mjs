// Every wait is a pidfd and a select with the exact remaining time, and the wall rule decides (E1.S8a part ii-a; B6)
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_wait.py');
