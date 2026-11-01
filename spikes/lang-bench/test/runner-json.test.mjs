// Exact-decimal JSON (E1.S8a part i, decision V2): numbers by value, repeated names and NaN refused, bad-utf8 and bad-json verdicts.
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_json.py');
