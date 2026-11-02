# The execution probe's workload for line (f) (E1.S8b): a toy that holds 40 MiB for a moment, then exits.
data = bytearray(40 * 1024 * 1024)
for i in range(0, len(data), 4096):
    data[i] = 1
