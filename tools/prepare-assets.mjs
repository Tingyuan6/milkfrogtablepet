import fs from 'node:fs/promises';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import crypto from 'node:crypto';
const require=createRequire(import.meta.url);
const sharp=require('sharp');
const cv=await require(process.env.OPENCV_JS_MODULE || '@techstark/opencv-js');
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const out=path.join(root,'assets/derived');
await fs.mkdir(out,{recursive:true});
const W=320,H=320;
const sprites=new Map(), poses={}, clips=[], hashes={};
const guide={idle:[30,53,70,118,193,239,290],sit_1:[30,53,70,115,174,202,224],sit_2:[30,53,70,115,174,202,224],peek_left:[30,53,70,115,174,202,224],peek_right:[30,53,70,115,174,202,224],bottom:[169,186,198,220,258,278,290]};
const canonicalGuide=guide.idle;
function bounds(d){let l=W,t=H,r=-1,b=-1;for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(d[(y*W+x)*4+3]>=96){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}return [l,t,r+1,b+1];}
async function save(file,d){await fs.mkdir(path.dirname(path.join(out,file)),{recursive:true});await sharp(d,{raw:{width:W,height:H,channels:4}}).png().toFile(path.join(out,file));}
function profile(d){const rows=[];for(let y=0;y<H;y++){let l=W,r=-1;for(let x=0;x<W;x++)if(d[(y*W+x)*4+3]>32){l=Math.min(l,x);r=Math.max(r,x);}rows.push(r>=l?[l-1,r+1]:null);}for(let y=0;y<H;y++)if(!rows[y]){let n=1;while(!rows[Math.max(0,y-n)]&&!rows[Math.min(H-1,y+n)]&&n<H)n++;rows[y]=rows[Math.max(0,y-n)]||rows[Math.min(H-1,y+n)]||[159,161];}return rows;}
function mapGuide(y,a,b){let i=0;while(i<a.length-2&&y>a[i+1])i++;return b[i]+(b[i+1]-b[i])*(y-a[i])/(a[i+1]-a[i]);}
function sample(d,x,y){if(x<0||x>W-1||y<0||y>H-1)return [0,0,0,0];const x0=Math.floor(x),y0=Math.floor(y),fx=x-x0,fy=y-y0;const acc=[0,0,0,0];for(let j=0;j<2;j++)for(let i=0;i<2;i++){const p=(Math.min(H-1,y0+j)*W+Math.min(W-1,x0+i))*4,w=(i?fx:1-fx)*(j?fy:1-fy),a=d[p+3]/255;for(let c=0;c<3;c++)acc[c]+=d[p+c]*a*w;acc[3]+=a*w;}return acc;}
function put(d,p,c){d[p+3]=Math.round(c[3]*255);if(c[3]>.0001)for(let k=0;k<3;k++)d[p+k]=Math.round(c[k]/c[3]);}
function canonical(d,rows,g){const result=Buffer.alloc(W*H*4);for(let y=30;y<290;y++){const sy=mapGuide(y,canonicalGuide,g),r=rows[Math.max(0,Math.min(H-1,Math.round(sy)))];for(let x=86;x<=234;x++)put(result,(y*W+x)*4,sample(d,r[0]+(x-86)/148*(r[1]-r[0]),sy));}return result;}
function optical(a,b){const aa=new cv.Mat(H,W,cv.CV_8UC1),bb=new cv.Mat(H,W,cv.CV_8UC1);for(let p=0;p<W*H;p++){const i=p*4;aa.data[p]=(a[i]*.299+a[i+1]*.587+a[i+2]*.114)*(a[i+3]/255)+96*(1-a[i+3]/255);bb.data[p]=(b[i]*.299+b[i+1]*.587+b[i+2]*.114)*(b[i+3]/255)+96*(1-b[i+3]/255);}const f=new cv.Mat(),r=new cv.Mat();cv.calcOpticalFlowFarneback(aa,bb,f,.5,4,31,5,7,1.5,256);cv.calcOpticalFlowFarneback(bb,aa,r,.5,4,31,5,7,1.5,256);const forward=new Float32Array(f.data32F),reverse=new Float32Array(r.data32F);aa.delete();bb.delete();f.delete();r.delete();return [forward,reverse];}
function flowAt(f,x,y){const p=(Math.max(0,Math.min(H-1,Math.round(y)))*W+Math.max(0,Math.min(W-1,Math.round(x))))*2;return [Math.max(-48,Math.min(48,f[p])),Math.max(-48,Math.min(48,f[p+1]))];}
function inverse(f,x,y,t){let sx=x,sy=y;for(let i=0;i<3;i++){const v=flowAt(f,sx,sy);sx=x-t*v[0];sy=y-t*v[1];}return [sx,sy];}
function morph(a,b,t){const ga=guide[a.id]||guide.idle,gb=guide[b.id]||guide.idle,gm=ga.map((v,i)=>v*(1-t)+gb[i]*t),d=Buffer.alloc(W*H*4);for(let y=Math.max(0,Math.floor(gm[0]));y<Math.min(H,Math.ceil(gm[6]));y++){const ya=mapGuide(y,gm,ga),yb=mapGuide(y,gm,gb),cy=mapGuide(y,gm,canonicalGuide),ra=a.rows[Math.max(0,Math.min(H-1,Math.round(ya)))],rb=b.rows[Math.max(0,Math.min(H-1,Math.round(yb)))],l=ra[0]*(1-t)+rb[0]*t,r=ra[1]*(1-t)+rb[1]*t;for(let x=Math.max(0,Math.floor(l));x<Math.min(W,Math.ceil(r));x++){const cx=86+(x-l)/(r-l)*148,pa=inverse(a.flow,cx,cy,t),pb=inverse(b.flow,cx,cy,1-t),ca=sample(a.canonical,...pa),cb=sample(b.canonical,...pb);put(d,(y*W+x)*4,ca.map((v,i)=>v*(1-t)+cb[i]*t));}}return d;}
async function add(id,data,contact){const rows=profile(data),g=guide[id]||guide.idle;sprites.set(id,{id,data,rows,canonical:canonical(data,rows,g)});await save(`poses/${id}.png`,data);poses[id]={file:`poses/${id}.png`,bounds:bounds(data),contact,head:[160,53],dragPivot:[160,150]};}
for(const id of ['idle','sit_1','sit_2','peek_left','bottom']){const file=path.join(root,'assets/source',`${id}.png`);hashes[id]=crypto.createHash('sha256').update(await fs.readFile(file)).digest('hex');const d=await sharp(file).ensureAlpha().raw().toBuffer();await add(id,d,id==='peek_left'?[114,130]:id.startsWith('sit')?[160,224]:[160,290]);}
await add('peek_right',await sharp(path.join(root,'assets/source/peek_left.png')).flop().ensureAlpha().raw().toBuffer(),[206,130]);
// Generated key poses supply actual arm/bending silhouettes for large pose changes.
for(const spec of [{name:'bottom',heights:[218,174,130],maxWidths:[220,260,266]},{name:'sit',heights:[194,194,194],maxWidths:[160,160,160]},{name:'peek',heights:[250,194,194],maxWidths:[198,100,96]}]){
 const file=path.join(root,'assets/source/bridges',`${spec.name}-strip.png`);const m=await sharp(file).metadata(),cell=Math.floor(m.width/3);
 for(let col=0;col<3;col++){
  const raw=await sharp(file).extract({left:col*cell,top:0,width:cell,height:m.height}).ensureAlpha().raw().toBuffer();
  let l=cell,t=m.height,r=-1,b=-1;for(let y=0;y<m.height;y++)for(let x=0;x<cell;x++){const p=(y*cell+x)*4,R=raw[p],G=raw[p+1],B=raw[p+2];if((R>150&&R>G*1.8)||(R>230&&G>225&&B<45)||raw[p+3]<160)raw[p+3]=0;if(raw[p+3]>200){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}}
  if(r<l)throw new Error('Empty generated key pose');
  const resized=await sharp(raw,{raw:{width:cell,height:m.height,channels:4}}).extract({left:l,top:t,width:r-l+1,height:b-t+1}).resize({width:spec.maxWidths[col],height:spec.heights[col],fit:'inside'}).png().toBuffer();const dim=await sharp(resized).metadata();
  const bottom=spec.name==='bottom'||(spec.name==='peek'&&col===0)?290:224,top=bottom-dim.height,left=Math.round(160-dim.width/2);
  const d=await sharp({create:{width:320,height:320,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:resized,left,top}]).raw().toBuffer();
  const id=`${spec.name}_key_${col+1}`,height=dim.height;guide[id]=[top,top+height*.14,top+height*.25,top+height*.44,top+height*.70,top+height*.90,bottom];
  if(spec.name==='sit')guide[id]=guide.sit_1;
  await add(id,d,[160,bottom]);
  if(spec.name==='peek'){const right=`peek_right_key_${col+1}`;guide[right]=guide[id];await add(right,await sharp(d,{raw:{width:320,height:320,channels:4}}).flop().raw().toBuffer(),[160,bottom]);}
 }
}
const gif=path.join(root,'assets/source/sway.gif'),meta=await sharp(gif,{animated:true}).metadata();
const packed=await sharp(gif,{animated:true}).ensureAlpha().raw().toBuffer(),durations=meta.delay;
if(meta.pages!==37||durations.reduce((a,b)=>a+b,0)!==4390)throw new Error('Unexpected sway timing');
hashes.sway=crypto.createHash('sha256').update(await fs.readFile(gif)).digest('hex');
for(let i=0;i<meta.pages;i++)await add(`sway_${String(i).padStart(2,'0')}`,packed.subarray(i*W*H*4,(i+1)*W*H*4),[160,290]);
async function bridge(from,to,count,duration){const a=sprites.get(from),b=sprites.get(to),id=`${from}__${to}`,files=[poses[from].file],flow=optical(a.canonical,b.canonical);a.flow=flow[0];b.flow=flow[1];for(let i=1;i<count-1;i++){const raw=i/(count-1),t=raw*raw*(3-2*raw),file=`clips/${id}/${String(i).padStart(2,'0')}.png`;await save(file,morph(a,b,t));files.push(file);}files.push(poses[to].file);clips.push({from,to,files,durationMs:duration,method:'silhouette correspondence + bidirectional dense optical flow',review:'prototype; human visual acceptance pending'});console.log(`bridge ${from} -> ${to}`);}
await bridge('idle','sway_00',9,220);
for(let i=0;i<37;i++)await bridge(`sway_${String(i).padStart(2,'0')}`,'idle',9,220);
async function through(from,to,keys,duration){const ids=[from,...keys,to],begin=clips.length;for(let i=0;i<ids.length-1;i++)await bridge(ids[i],ids[i+1],5,duration/(ids.length-1));const parts=clips.splice(begin);clips.push({from,to,files:parts.flatMap((c,i)=>i?c.files.slice(1):c.files),durationMs:duration,method:'authored key poses + silhouette correspondence + bidirectional optical flow',review:'prototype; human visual acceptance pending'});}
await through('idle','bottom',['bottom_key_1','bottom_key_2','bottom_key_3'],360);
await through('idle','peek_left',['peek_key_1','peek_key_2','peek_key_3'],360);
await through('idle','peek_right',['peek_right_key_1','peek_right_key_2','peek_right_key_3'],360);
for(const target of ['sit_1','sit_2'])await bridge('idle',target,11,300);
await through('sit_1','sit_2',['sit_key_1','sit_key_2','sit_key_3'],300);
// A clip and its reverse use exactly the same pixels, including both endpoints.
for(const c of [...clips])if(!clips.some(r=>r.from===c.to&&r.to===c.from))clips.push({from:c.to,to:c.from,files:[...c.files].reverse(),durationMs:c.durationMs,method:c.method,review:c.review});
const idle=sprites.get('idle').data;
for(let iy=-2;iy<=2;iy++)for(let ix=-2;ix<=2;ix++){const d=Buffer.from(idle);for(const e of [{cx:144.4,cy:52.1,rx:7.3,ry:9.0},{cx:175.4,cy:51.8,rx:7.0,ry:8.9}])for(let y=Math.floor(e.cy-e.ry);y<=Math.ceil(e.cy+e.ry);y++)for(let x=Math.floor(e.cx-e.rx);x<=Math.ceil(e.cx+e.rx);x++){const r=((x-e.cx)/e.rx)**2+((y-e.cy)/e.ry)**2;if(r>=1)continue;const fall=Math.pow(1-r,1.5);put(d,(y*W+x)*4,sample(idle,x-ix*1.0*fall,y-iy*.8*fall));}await save(`gaze/${ix+2}_${iy+2}.png`,d);}
const manifest={version:1,canvas:[W,H],sourceSha256:hashes,poses,sway:{frames:Array.from({length:37},(_,i)=>`sway_${String(i).padStart(2,'0')}`),durationsMs:durations,totalMs:4390,loop:false},eyes:[{cx:144.4,cy:52.1,rx:7.3,ry:9.0},{cx:175.4,cy:51.8,rx:7.0,ry:8.9}],belly:{center:[160,166],radius:[65,70],minY:102,maxY:235},clips};
await fs.writeFile(path.join(out,'manifest.json'),JSON.stringify(manifest,null,2));
const keep=new Set(['manifest.json',...Object.values(poses).map(p=>p.file),...clips.flatMap(c=>c.files)]);for(let y=0;y<5;y++)for(let x=0;x<5;x++)keep.add(`gaze/${x}_${y}.png`);
for(const relative of await fs.readdir(out,{recursive:true})){const file=relative.replaceAll('\\','/');if(file.endsWith('.png')&&!keep.has(file))await fs.unlink(path.join(out,relative));}
const preview=[];for(const [i,id]of ['idle','sway_10','sit_1','sit_2','peek_left','peek_right','bottom'].entries())preview.push({input:path.join(out,poses[id].file),left:i*320,top:0});await sharp({create:{width:2240,height:320,channels:4,background:'#b6c5d0'}}).composite(preview).png().toFile(path.join(root,'docs/poses-preview.png'));
const rowClips=['idle__sway_00','sway_15__idle','sit_1__sit_2','idle__bottom','idle__peek_left'];const panels=[];for(let row=0;row<rowClips.length;row++){const c=clips.find(c=>`${c.from}__${c.to}`===rowClips[row]);for(let col=0;col<7;col++)panels.push({input:path.join(out,c.files[Math.round(col/6*(c.files.length-1))]),left:col*320,top:row*320});}await sharp({create:{width:2240,height:1600,channels:4,background:'#a6b7c5'}}).composite(panels).png().toFile(path.join(root,'docs/transitions-preview.png'));
console.log(JSON.stringify({poses:Object.keys(poses).length,clips:clips.length,frames:37,totalMs:4390}));
