import sys, mmap, capstone
f = open(sys.argv[1], 'rb'); m = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
CODE_OFF = 0x608f0
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
for a in sys.argv[2:]:
    va = int(a, 16); before = 0x60; after = 0x80
    code = m[CODE_OFF + va - before: CODE_OFF + va + after]
    print('====', a)
    # disassemble from a few starting points to resync
    for ins in md.disasm(code, va - before):
        if ins.address >= va - 0x40: print(f'{ins.address:9x}  {ins.mnemonic:8} {ins.op_str}')
