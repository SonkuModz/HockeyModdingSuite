import numpy as np, sys, struct
path = sys.argv[1]
raw = np.memmap(path, dtype=np.uint8, mode='r')
CODE_OFF, CODE_SZ = 0x608f0, 0xbfad8cc
RO_OFF, RO_VA = 0xc01fe80, 0xbfb0000
code = np.asarray(raw[CODE_OFF:CODE_OFF + CODE_SZ])
# REX.W lea r, [rip+disp32]: 48|4C 8D modrm(00 reg 101)
p = np.nonzero(((code[:-7] == 0x48) | (code[:-7] == 0x4C)) & (code[1:-6] == 0x8D) & ((code[2:-5] & 0xC7) == 0x05))[0]
disp = code[p + 3].astype(np.int64) | (code[p + 4].astype(np.int64) << 8) | (code[p + 5].astype(np.int64) << 16) | (code[p + 6].astype(np.int64) << 24)
disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
target = p + 7 + disp
np.save(sys.argv[2] + '_p.npy', p); np.save(sys.argv[2] + '_t.npy', target)
print(len(p), 'rip-relative leas')
