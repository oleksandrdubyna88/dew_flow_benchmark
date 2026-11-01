// The AST scans over runner/: imports, function and file shape, no float in a comparison (E1.S8a acceptance).
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_shape.py');
