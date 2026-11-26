import struct,sys,json
def lz4_block(src,out):
    i=0;n=len(src)
    while i<n:
        t=src[i];i+=1
        l=t>>4
        if l==15:
            while True:
                b=src[i];i+=1;l+=b
                if b!=255:break
        out+=src[i:i+l];i+=l
        if i>=n:break
        off=src[i]|(src[i+1]<<8);i+=2
        m=t&15
        if m==15:
            while True:
                b=src[i];i+=1;m+=b
                if b!=255:break
        m+=4
        s=len(out)-off
        for k in range(m): out.append(out[s+k])
def lz4_frame(d):
    assert d[:4]==b'\x04\x22\x4d\x18'
    flg=d[4];i=6
    if flg&8:i+=8
    if flg&1:i+=4
    i+=1
    out=bytearray()
    while True:
        sz=struct.unpack_from('<I',d,i)[0];i+=4
        if sz==0:break
        raw=sz&0x80000000;sz&=0x7fffffff
        blk=d[i:i+sz];i+=sz
        if raw: out+=blk
        else: lz4_block(blk,out)
        if flg&0x10:i+=4
    return bytes(out)
d=open(sys.argv[1],'rb').read()
i=0;k=0
while i<len(d):
    ln=struct.unpack_from('<Q',d,i)[0];h=d[i+8:i+16];body=d[i+16:i+16+ln]
    if not ln: break
    M=b'\x04\x22\x4d\x18'
    txt=lz4_frame(body[1:]) if body[1:5]==M else (lz4_frame(body) if body[:4]==M else body)
    open(sys.argv[2]+f'.{k}.json','wb').write(txt)
    print(k,hex(i),ln,h.hex(),len(txt),txt[:60])
    i+=16+ln;k+=1
