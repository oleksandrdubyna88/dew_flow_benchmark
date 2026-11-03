// W1/N7 (fix batch 4): a test's CPU burner is bounded, owned, and dies with its parent; no test starts an unbounded helper process.
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_burner.py');
