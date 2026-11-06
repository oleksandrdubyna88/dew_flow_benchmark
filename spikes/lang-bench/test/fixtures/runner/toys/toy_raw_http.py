import os, signal, socket, threading
s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(os.environ["PORT"]))); s.listen(5)
signal.signal(signal.SIGTERM, lambda *a: os._exit(0))
def serve(c):
    while True:
        d = c.recv(4096)
        if not d:
            return
        path = d.split(b" ")[1]
        if path == b"/chunked":
            c.sendall(b"HTTP/1.1 200 OK\r\nX-K: v\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n6\r\n world\r\n0\r\n\r\n")
        elif path == b"/close":
            c.sendall(b"HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\nclosing"); c.close(); return
        else:
            c.sendall(b"HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nagain")
while True:
    c, _ = s.accept()
    threading.Thread(target=serve, args=(c,), daemon=True).start()
