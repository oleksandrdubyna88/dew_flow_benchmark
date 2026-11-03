#!/bin/bash
# A toy, never a task of the series: serves ping -> PING on 127.0.0.1 and writes its port to ready.port. Built offline by the test (no packages); ends with exec, so the program is dotnet itself.
exec dotnet bin/server.dll
