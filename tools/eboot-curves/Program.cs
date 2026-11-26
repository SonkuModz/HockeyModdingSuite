using System.Buffers.Binary;
using System.Text;
using Iced.Intel;
var b = File.ReadAllBytes(args[0]);
// segments (same as Tunables.Segments): PS4 SELF
int count = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(0x18));
int elf = 0x20 + count * 0x20;
long phOff = BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(elf + 0x20));
int phn = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(elf + 0x38));
var segs = new List<(long va,long size,long off,uint flags)>();
for (int i=0;i<count;i++){ long flags=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x20+i*0x20)); long off=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x28+i*0x20)); long msz=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(0x38+i*0x20));
  int ph=(int)((flags>>20)&0xFFF); if ((flags & 0x800)==0) continue;
  int p=elf+(int)phOff+ph*0x38; long va=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p+0x10)); uint pf=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p+4)); long filesz=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p+0x20)); long memsz=BinaryPrimitives.ReadInt64LittleEndian(b.AsSpan(p+0x28));
  if(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p))!=1) continue; segs.Add((va,Math.Min(msz,filesz),off,pf)); Console.Error.WriteLine($"seg va={va:X} filesz={filesz:X} memsz={memsz:X} off={off:X} flags={pf}");}
long Off(long va){ foreach(var s in segs) if(va>=s.va&&va<s.va+s.size) return s.off+va-s.va; return -1;}
var code = segs.First(s=>s.va==0);
string mode = args[1];
if (mode=="find") {
  // find string, then lea sites
  var needle = Encoding.ASCII.GetBytes("\0"+args[2]+"\0");
  int at = b.AsSpan().IndexOf(needle)+1; long sva=-1;
  foreach(var s in segs) if(at>=s.off&&at<s.off+s.size) sva=s.va+at-s.off;
  Console.WriteLine($"string at file {at:X} va {sva:X}");
  int c0=(int)code.off, c1=(int)(code.off+code.size)-7;
  for(int i=c0;i<c1;i++){ if(b[i+1]!=0x8D||(b[i]!=0x48&&b[i]!=0x4C)||(b[i+2]&0xC7)!=5) continue; long site=i-c0; long t=site+7+BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i+3)); if(t==sva){ Console.WriteLine($"lea site {site:X}"); Dis(site-int.Parse(args[3]), int.Parse(args[4])); } }
}
else if (mode=="objs") {
  // every "\0gName\0" string and the lea sites to it; decode forward and collect stores into the object
  var names=new Dictionary<long,string>();
  foreach(var sg in segs){ int end=(int)(sg.off+sg.size); for(int i=(int)sg.off+1;i<end-2;i++){ if(b[i]!='g'||b[i-1]!=0||b[i+1]<'A'||b[i+1]>'Z') continue; int j=i+1; while(j<end&&(char.IsAsciiLetterOrDigit((char)b[j])||b[j]=='_')) j++; if(j<end&&b[j]==0&&j-i>1) names.TryAdd(sg.va+i-sg.off,Encoding.ASCII.GetString(b,i,j-i)); i=j; } }
  int c0=(int)code.off, c1=(int)(code.off+code.size)-7;
  var hist=new Dictionary<long,int>();
  var w=new StreamWriter(args[2]);
  w.WriteLine("name,typecode,count,ptr,fileoff,values,extra,site");
  for(int i=c0;i<c1;i++){
    if(b[i+1]!=0x8D||(b[i]!=0x48&&b[i]!=0x4C)||(b[i+2]&0xC7)!=5) continue;
    long site=i-c0; long t=site+7+BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(i+3));
    if(!names.TryGetValue(t,out var nm)) continue;
    var d=Iced.Intel.Decoder.Create(64,new ByteArrayCodeReader(b,i,0x140),(ulong)site);
    Register nreg=Register.None; long baseA=0; var lea=new Dictionary<Register,long>(); var imm=new Dictionary<long,long>(); var regs=new Dictionary<long,long>(); var xl=new Dictionary<Register,long>(); var xst=new Dictionary<long,long>();
    int n=0;
    while(d.IP<(ulong)(site+0x140)&&n<60){ var x=d.Decode(); n++; if(x.IsInvalid) break;
      if(n==1){ nreg=x.Op0Register.GetFullRegister(); continue; }
      if(x.Mnemonic==Mnemonic.Lea&&x.Op1Kind==OpKind.Memory&&x.IsIPRelativeMemoryOperand){ lea[x.Op0Register.GetFullRegister()]=(long)x.IPRelativeMemoryAddress; continue; }
      if((x.Mnemonic==Mnemonic.Vmovsd||x.Mnemonic==Mnemonic.Vmovss||x.Mnemonic==Mnemonic.Vmovq)&&x.Op0Kind==OpKind.Register&&x.Op1Kind==OpKind.Memory&&x.IsIPRelativeMemoryOperand){ xl[x.Op0Register]=(long)x.IPRelativeMemoryAddress; continue; }
      if(x.Op0Kind==OpKind.Memory&&x.IsIPRelativeMemoryOperand){ long m=(long)x.IPRelativeMemoryAddress;
        if(baseA==0&&x.Mnemonic==Mnemonic.Mov&&x.Op1Kind==OpKind.Register&&x.Op1Register.GetFullRegister()==nreg){ baseA=m-0x10; continue; }
        if(baseA==0) continue; long off=m-baseA; if(off<0||off>=0x60) { if(off>0x60) break; continue; }
        if(x.Mnemonic==Mnemonic.Mov&&(x.Op1Kind==OpKind.Immediate32||x.Op1Kind==OpKind.Immediate8||x.Op1Kind==OpKind.Immediate32to64)) imm[off]=x.Op1Kind==OpKind.Immediate8?x.Immediate8:(int)x.Immediate32;
        else if(x.Mnemonic==Mnemonic.Mov&&x.Op1Kind==OpKind.Register&&lea.TryGetValue(x.Op1Register.GetFullRegister(),out var pv)) regs[off]=pv;
        else if(x.Op1Kind==OpKind.Register&&xl.TryGetValue(x.Op1Register,out var xv)) xst[off]=xv;
      }
      if(x.Mnemonic==Mnemonic.Call && imm.ContainsKey(0x20) && imm.ContainsKey(0x38)) break;
    }
    if(baseA==0||!imm.TryGetValue(0x20,out var tc)) continue;
    hist[tc]=hist.GetValueOrDefault(tc)+1;
    if(tc==1||tc==2||tc==4) continue;
    long cnt=imm.GetValueOrDefault(0x38,-1); long ptr=regs.GetValueOrDefault(0x40,0); long fo=ptr!=0?Off(ptr):-1;
    string vals="";
    if(fo>0&&cnt>0&&cnt<=64){ vals=string.Join(" ",Enumerable.Range(0,(int)cnt).Select(k=>BitConverter.ToSingle(b,(int)fo+k*4).ToString("G6",System.Globalization.CultureInfo.InvariantCulture))); }
    string extra=string.Join(" ",xst.Select(kv=>$"+{kv.Key:X}<-[{kv.Value:X}]={(Off(kv.Value) is var q && q>0? BitConverter.ToSingle(b,(int)q).ToString("G6",System.Globalization.CultureInfo.InvariantCulture)+"/"+BitConverter.ToSingle(b,(int)q+4).ToString("G6",System.Globalization.CultureInfo.InvariantCulture):"?")}"))+" "+string.Join(" ",imm.Where(kv=>kv.Key!=0x20&&kv.Key!=0x38).Select(kv=>$"+{kv.Key:X}={kv.Value}"))+" "+string.Join(" ",regs.Where(kv=>kv.Key!=0x40&&kv.Key>0x20).Select(kv=>$"+{kv.Key:X}=&{kv.Value:X}"));
    w.WriteLine($"{nm},{tc},{cnt},{ptr:X},{fo:X},{vals},{extra.Trim()},{site:X}");
  }
  w.Close();
  foreach(var kv in hist.OrderBy(k=>k.Key)) Console.WriteLine($"typecode {kv.Key}: {kv.Value}");
}
else if (mode=="dis") Dis(Convert.ToInt64(args[2],16), int.Parse(args[3]));
else if (mode=="hex") { long va=Convert.ToInt64(args[2],16); long o=Off(va); int n=int.Parse(args[3]); for(int i=0;i<n;i+=16){ Console.Write($"{va+i:X8}: "); for(int j=0;j<16;j++) Console.Write($"{b[o+i+j]:X2} "); Console.Write(" "); for(int j=0;j<16;j+=4) Console.Write($"{BitConverter.ToSingle(b,(int)(o+i+j)),10:G5} "); Console.WriteLine(); } }
void Dis(long start,int len){
  var d=Iced.Intel.Decoder.Create(64,new ByteArrayCodeReader(b,(int)(code.off+start),len),(ulong)start);
  var f=new NasmFormatter(); var o=new StringOutput();
  while(d.IP<(ulong)(start+len)){ var x=d.Decode(); f.Format(x,o); string extra="";
    if(x.IsIPRelativeMemoryOperand){ long m=(long)x.IPRelativeMemoryAddress; long fo=Off(m); if(fo>=0&&fo+8<b.Length){ extra=$"   ; [{m:X}] f={BitConverter.ToSingle(b,(int)fo):G6} i={BitConverter.ToInt32(b,(int)fo)}"; int e=Array.IndexOf(b,(byte)0,(int)fo,200); if(e>fo+2){ var s=Encoding.Latin1.GetString(b,(int)fo,e-(int)fo); if(s.All(ch=>ch>=32&&ch<127)) extra+=$" \"{s}\"";} } }
    Console.WriteLine($"{x.IP:X8} {o.ToStringAndReset(),-60}{extra}"); }
}
