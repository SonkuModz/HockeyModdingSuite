import struct, sys
d = open(sys.argv[1], 'rb').read(0x4000)
segs = struct.unpack_from('<H', d, 0x18)[0]
print('self segments', segs)
for i in range(segs):
    fl, off, csz, dsz = struct.unpack_from('<QQQQ', d, 0x20 + i * 0x20)
    print(f'  seg{i}: flags {fl:#x} off {off:#x} comp {csz:#x} dec {dsz:#x}')
e = d.find(b'\x7fELF')
print('ELF at', hex(e))
ph_off, = struct.unpack_from('<Q', d, e + 0x20)
phnum, = struct.unpack_from('<H', d, e + 0x38)
print('entry', hex(struct.unpack_from('<Q', d, e + 0x18)[0]), 'phnum', phnum)
for i in range(phnum):
    p = e + ph_off + i * 56
    t, fl, off, va, pa, fs, ms, al = struct.unpack_from('<IIQQQQQQ', d, p)
    print(f'  ph{i}: type {t:#x} flags {fl} off {off:#x} va {va:#x} filesz {fs:#x} memsz {ms:#x}')
