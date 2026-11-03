import os
import socket

listener = socket.socket()
listener.bind(("127.0.0.1", 0))
listener.listen(4)
with open("ready.port.tmp", "w") as f:
    f.write(str(listener.getsockname()[1]))
os.rename("ready.port.tmp", "ready.port")
while True:
    connection, _ = listener.accept()
    with connection:
        line = connection.makefile("rb").readline()
        connection.sendall(line.strip().upper() + b"\n")
