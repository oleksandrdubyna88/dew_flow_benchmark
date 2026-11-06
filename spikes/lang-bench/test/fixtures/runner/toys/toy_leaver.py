import os, signal, socket, subprocess
s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(os.environ["PORT"]))); s.listen(5)
subprocess.Popen(["sleep", "20"], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
signal.signal(signal.SIGTERM, lambda *a: os._exit(0))
signal.pause()
