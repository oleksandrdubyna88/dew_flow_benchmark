import os, json, uuid, signal, threading, datetime
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
orders, keys, lock = {}, {}, threading.Lock()
class H(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    def log_message(self,*a): pass
    def send(self, code, body, extra=()):
        b = json.dumps(body).encode()
        self.send_response(code); self.send_header("Content-Type","application/json"); self.send_header("Content-Length",str(len(b)))
        for k,v in extra: self.send_header(k,v)
        self.end_headers(); self.wfile.write(b)
    def do_POST(self):
        n=int(self.headers.get("Content-Length",0)); body=json.loads(self.rfile.read(n))
        key=self.headers.get("Idempotency-Key")
        with lock:
            if key in keys: code,o=keys[key]
            else:
                o=dict(body); o["id"]=str(uuid.uuid4()); o["at"]=datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
                orders[o["id"]]=o; keys[key]=(201,o); code=201
        self.send(code,o,[("Location","/orders/"+o["id"])])
    def do_GET(self):
        if self.path=="/orders": return self.send(200,{"orders":list(orders.values())})
        o=orders.get(self.path.split("/")[-1])
        self.send(200 if o else 404, o or {})
def term(*a): os._exit(0)
signal.signal(signal.SIGTERM, term)
srv=ThreadingHTTPServer(("127.0.0.1",int(os.environ["PORT"])),H)
srv.daemon_threads=True
srv.serve_forever()
