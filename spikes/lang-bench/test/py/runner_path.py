"""Puts runner/ on sys.path for the unit tests (python -I leaves the script's directory off it)."""
import os
import sys

RUNNER_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "runner"))
if RUNNER_DIR not in sys.path:
    sys.path.insert(0, RUNNER_DIR)
