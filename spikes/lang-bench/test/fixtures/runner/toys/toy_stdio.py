import os, sys
print("go", flush=True)
line = sys.stdin.readline()
print(os.environ["GREETING"], line.strip(), flush=True)
print("file:", open(os.environ["NOTE_PATH"]).read(), flush=True)
sys.stdin.read()
print("bye", flush=True)
