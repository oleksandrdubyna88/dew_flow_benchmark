import os, socket, signal, sys, threading, selectors
port=int(os.environ["PORT"])
store={}
srv=socket.socket(); srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR,1); srv.bind(("127.0.0.1",port)); srv.listen(50)
def serve(c):
    buf=b""
    while True:
        try: d=c.recv(65536)
        except OSError: return
        if not d: return
        buf+=d
        while True:
            if len(buf)<5: break
            n=int(buf[:4]); 
            if len(buf)<5+n: break
            p=buf[5:5+n]; buf=buf[5+n:]
            if p==b"hi": r=b"ok"
            elif p.startswith(b"put "):
                _,name,text=p.split(b" ",2); store[name]=text; r=b"ok"
            elif p.startswith(b"take "): r=store.get(p[5:],b"none")
            else: r=b"?"
            c.sendall(b"%04d|"%len(r)+r)
def term(*a): os._exit(0)
signal.signal(signal.SIGTERM, term)
while True:
    c,_=srv.accept()
    threading.Thread(target=serve,args=(c,),daemon=True).start()
