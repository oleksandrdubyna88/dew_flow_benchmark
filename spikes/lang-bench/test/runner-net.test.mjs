// The network side of a daemon or web case with fake sockets (no program): a program that dies on a request is a reject, host errors are runner faults, a start-together group loses no byte silently, the buffer cap.
import { pythonUnits } from './support/py-units.mjs';

pythonUnits('test_rs_session_net.py');
