"""rs_stop: the runner stopped by a signal (SIGTERM, SIGINT, SIGHUP): the handler raises `Interrupted` (a BaseException, so no handler of a case
swallows it), the cleanup that follows is not interrupted again, and every launcher still registered is stopped (`cgroup.kill`, or the group on
the plain path) before the runner says it reached no verdict."""
import signal
import sys

import rs_exec as ex
import rs_launch as rl


class Interrupted(BaseException):
    def __init__(self, signum):
        super().__init__(signum)
        self.signum = signum


def _on_signal(signum, frame):
    signal.pthread_sigmask(signal.SIG_BLOCK, rl.STOPPING_SIGNALS)  # first, all at once: no second stopping signal gets in while the rest is set
    for other in rl.STOPPING_SIGNALS:  # the cleanup that follows must not be interrupted again
        signal.signal(other, signal.SIG_IGN)
    signal.pthread_sigmask(signal.SIG_UNBLOCK, rl.STOPPING_SIGNALS)  # ignored now: no blocked mask outlives the handler, and children inherit it
    raise Interrupted(signum)


def install_handlers():
    for sig in rl.STOPPING_SIGNALS:
        signal.signal(sig, _on_signal)


def stopped(interrupted):
    """Stop what is still running and say so on standard error; the caller exits 5."""
    try:
        rl.abandon_active()
    except ex.CleanupFailed as failed:
        print(f"run_samples: {failed}", file=sys.stderr)
    print(f"run_samples: interrupted by {signal.Signals(interrupted.signum).name}: no verdict was reached", file=sys.stderr)
