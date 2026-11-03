"""Every tuning variable the eboot registers: name, type, default, min, max, group path, second string."""
import sys, mmap, struct, csv, re, capstone, numpy as np
f = open(sys.argv[1], 'rb'); m = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
SEG = [(0x0, 0xbfad8cc, 0x608f0), (0xbfb0000, 0x2395dc4, 0xc01fe80), (0xe348000, 0x8efad8, 0xe3ba3d0), (0xec38000, 0x5b76b0, 0xecacc70)]
def off(va):
    for v, s, o in SEG:
        if v <= va < v + s: return o + va - v
def cstr(va):
    o = off(va)
    if o is None: return None
    e = m.find(b'\0', o, o + 512)
    return m[o:e].decode('latin1')
def f32(va):
    o = off(va); return None if o is None else struct.unpack_from('<f', m, o)[0]
CTOR = {0x13139e0: 'bool', 0x1313ae0: 'int', 0x1313c00: 'float'}
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64); md.detail = False
p = np.load('lea_p.npy'); t = np.load('lea_t.npy')
RO_OFF, RO_VA = 0xc01fe80, 0xbfb0000
ro = m[RO_OFF:RO_OFF + 0x2395dc4]
names = {}
# names sit back to back (\0gA\0gB\0), so the leading \0 is a lookbehind; matching it would eat the next name's \0 and skip that name
for x in re.finditer(rb'(?<=\x00)(g[A-Z][A-Za-z0-9_]+)\x00', ro): names.setdefault(RO_VA + x.start(), x.group(1).decode())
order = np.argsort(t); ts = t[order]
rip = re.compile(r'\[rip ([+-]) 0x([0-9a-f]+)\]')
def ripva(ins):
    mm = rip.search(ins.op_str)
    if not mm: return None
    d = int(mm.group(2), 16); return ins.address + ins.size + (d if mm.group(1) == '+' else -d)
def imm(s):
    try: return int(s, 0)
    except ValueError: return None
rows = []; seen = set()
for nva, name in names.items():
    i = np.searchsorted(ts, nva)
    while i < len(ts) and ts[i] == nva:
        site = int(p[order[i]]); i += 1
        # sync: a start before the site whose decoding lands on it
        ins_list = None
        for back in range(0x50, 0, -1):
            L = list(md.disasm(m[0x608f0 + site - back: 0x608f0 + site + 0x50], site - back))
            if any(x.address == site for x in L): ins_list = L; break
        if not ins_list: continue
        k = next(j for j, x in enumerate(ins_list) if x.address == site)
        c = next((j for j in range(k, len(ins_list)) if ins_list[j].mnemonic == 'call'), None)
        if c is None: continue
        tgt = imm(ins_list[c].op_str)
        if tgt not in CTOR: continue
        s0 = max((j for j in range(k) if ins_list[j].mnemonic == 'call'), default=-1) + 1
        reg, xmm, stack = {}, {}, {}
        for x in ins_list[s0:c]:
            ops = [o.strip() for o in x.op_str.split(',')]
            if x.mnemonic == 'lea' and ripva(x) is not None: reg[ops[0]] = ('ptr', ripva(x))
            elif x.mnemonic == 'mov' and len(ops) == 2 and ops[1] in reg and not ops[0].startswith(('dword', 'qword', 'byte')): reg[ops[0]] = reg[ops[1]]
            elif x.mnemonic == 'mov' and len(ops) == 2 and imm(ops[1]) is not None:
                if ops[0].startswith(('dword ptr [rsp', 'qword ptr [rsp')): stack[ops[0]] = imm(ops[1])
                else: reg[ops[0]] = ('imm', imm(ops[1]))
            elif x.mnemonic == 'xor' and len(ops) == 2 and ops[0] == ops[1]: reg[ops[0]] = ('imm', 0)
            elif x.mnemonic == 'vmovss' and ripva(x) is not None and ops[0].startswith('xmm'): xmm[ops[0]] = f32(ripva(x))
            elif x.mnemonic in ('vxorps', 'vxorpd') and ops[1] == ops[2]: xmm[ops[0]] = 0.0
            elif x.mnemonic in ('vmovaps', 'vmovss') and len(ops) == 2 and ops[1] in xmm: xmm[ops[0]] = xmm[ops[1]]
        def R(*names_):
            for n in names_:
                if n in reg: return reg[n]
        g = R('rsi'); n2 = R('rcx')
        group = cstr(g[1]) if g and g[0] == 'ptr' else None
        second = cstr(n2[1]) if n2 and n2[0] == 'ptr' else None
        kind = CTOR[tgt]
        if kind == 'float': vals = [xmm.get('xmm0'), xmm.get('xmm1'), xmm.get('xmm2')]
        elif kind == 'int':
            a = R('r8d', 'r8'); b = R('r9d', 'r9')
            vals = [a[1] if a and a[0] == 'imm' else None, b[1] if b and b[0] == 'imm' else None, stack.get('dword ptr [rsp]', stack.get('qword ptr [rsp]'))]
        else:
            a = R('r8d', 'r8b', 'r8'); vals = [a[1] if a and a[0] == 'imm' else None, None, None]
        key = (name, site)
        if key in seen: continue
        seen.add(key)
        rows.append([name, kind] + vals + [group, second if second != name else '', hex(site)])
# a group path kept in a register (rsi from r13) carries over from the registration before it in the same function
rows.sort(key=lambda r: int(r[7], 16))
last = None
for r in rows:
    if r[5]: last = (r[5], int(r[7], 16))
    elif last and int(r[7], 16) - last[1] < 0x2000: r[5] = last[0]; last = (last[0], int(r[7], 16))
for r in rows:
    if r[1] == 'int':
        for k in (2, 3, 4):
            if isinstance(r[k], int) and r[k] >= 1 << 31: r[k] -= 1 << 32
    if r[1] == 'float':
        for k in (2, 3, 4):
            if isinstance(r[k], float): r[k] = float(f'{r[k]:.6g}')
with open(sys.argv[2], 'w', newline='', encoding='utf-8') as o:
    w = csv.writer(o); w.writerow(['name', 'type', 'default', 'min', 'max', 'group', 'label', 'site']); w.writerows(sorted(rows, key=lambda r: r[0]))
print(len(rows), 'tunables;', sum(1 for r in rows if r[2] is None), 'with no default read')
