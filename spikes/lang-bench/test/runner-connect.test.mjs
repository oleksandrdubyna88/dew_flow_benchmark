// A connection of a daemon or web scenario: only a program not listening yet is retried and then `refused`; the host's own socket errors are a runner fault, never a verdict. No program starts.
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_session_connect.py');
