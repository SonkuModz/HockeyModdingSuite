import re, sys, mmap
f = open(sys.argv[1], 'rb'); m = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
out = open(sys.argv[2], 'w', encoding='latin1')
for x in re.finditer(rb'[\x20-\x7e]{6,}', m):
    out.write(f'{x.start():09X} {x.group().decode("latin1")}\n')
