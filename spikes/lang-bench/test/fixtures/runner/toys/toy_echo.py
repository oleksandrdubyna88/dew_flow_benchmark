import os, signal, socket
s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(os.environ["PORT"]))); s.listen(5)
signal.signal(signal.SIGTERM, lambda *a: os._exit(0))
while True:
    c, _ = s.accept()
    c.sendall(c.recv(10))
