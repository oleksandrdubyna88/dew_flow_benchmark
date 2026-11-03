#!/bin/bash
# A toy, never a task of the series: serves ping -> PING on 127.0.0.1 and writes its port to ready.port. Ends with exec, so the program is node itself.
exec node server.mjs
