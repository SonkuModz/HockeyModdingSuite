import cv2,numpy as np,json,sys
im=cv2.imread(sys.argv[1],cv2.IMREAD_UNCHANGED)
if im.shape[2]==4:
    a=im[:,:,3:4]/255.0; rgb=(im[:,:,:3]*a+255*(1-a)).astype(np.uint8)
else: rgb=im[:,:,:3]
b,g,r=[rgb[:,:,i].astype(int) for i in range(3)]
H,W=b.shape
masks={
 'black':((r<90)&(g<90)&(b<90)),
 'red':((r>150)&(g<90)&(b<110)),
 'blue':((b>100)&(r<90)&(g<130)&(b>r+40)),
}
cols={'black':0x111111,'red':0xD6203A,'blue':0x1D5A9E}
out=[]
for k,m in masks.items():
    m=m.astype(np.uint8)*255
    S=6
    m=cv2.resize(m,(W*S,H*S),interpolation=cv2.INTER_CUBIC)
    m=cv2.GaussianBlur(m,(0,0),S*0.9)
    m=((m>127)*255).astype(np.uint8)
    cs,hier=cv2.findContours(m,cv2.RETR_CCOMP,cv2.CHAIN_APPROX_NONE)
    cmd=[]
    for c in cs:
        x,y,w,h=cv2.boundingRect(c)
        if cv2.contourArea(c)<30*S*S: continue
        if k=='black' and w<25*S and x>240*S and y<30*S: continue
        ap=cv2.approxPolyDP(c,0.35*S,True)[:,0,:]/S
        ap=[(round(float(a),2),round(float(b),2)) for a,b in ap]
        cmd+= [1,ap[0][0],ap[0][1]]
        for x,y in ap[1:]: cmd+=[2,x,y]
        cmd+=[2,ap[0][0],ap[0][1]]
    print(k,len(cmd)//3,file=sys.stderr)
    out.append([cols[k],"evenOdd",cmd])
print(json.dumps(out,separators=(',',':')))
print(W,H,file=sys.stderr)
