import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url),sharp=require('sharp');
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),dir=path.join(root,'assets/derived');
const m=JSON.parse(await fs.readFile(path.join(dir,'manifest.json'),'utf8'));
let checks=0;function check(value,message){assert.ok(value,message);checks++;}
check(m.sway.frames.length===37,'37 originals');check(m.sway.durationsMs.reduce((a,b)=>a+b,0)===4390,'4390 milliseconds');
for(const [id,hash]of Object.entries(m.sourceSha256)){const input=await fs.readFile(path.join(root,'assets/source',id==='sway'?'sway.gif':`${id}.png`));check(crypto.createHash('sha256').update(input).digest('hex')===hash,`source untouched: ${id}`);}
const files=new Set(Object.values(m.poses).map(p=>p.file));for(const c of m.clips){check(c.files[0]===m.poses[c.from].file,`start endpoint ${c.from}`);check(c.files.at(-1)===m.poses[c.to].file,`end endpoint ${c.to}`);check(c.durationMs>0,`duration ${c.from}`);const reverse=m.clips.find(r=>r.from===c.to&&r.to===c.from);check(reverse!==undefined,`return path exists for ${c.from}`);for(const f of c.files)files.add(f);}
for(let y=0;y<5;y++)for(let x=0;x<5;x++)files.add(`gaze/${x}_${y}.png`);
for(const file of files){const meta=await sharp(path.join(dir,file)).metadata();check(meta.width===320&&meta.height===320&&meta.hasAlpha,`frame format ${file}`);}
const left=await sharp(path.join(dir,m.poses.peek_left.file)).flop().raw().toBuffer(),right=await sharp(path.join(dir,m.poses.peek_right.file)).raw().toBuffer();check(left.equals(right),'right edge is exact horizontal mirror');
const packed=await sharp(path.join(root,'assets/source/sway.gif'),{animated:true}).ensureAlpha().raw().toBuffer();for(let i=0;i<37;i++){const frame=await sharp(path.join(dir,m.poses[m.sway.frames[i]].file)).ensureAlpha().raw().toBuffer();const source=packed.subarray(i*320*320*4,(i+1)*320*320*4);check(frame.equals(source),`original GIF frame ${i} retained exactly`);}
const idle=await sharp(path.join(dir,m.poses.idle.file)).ensureAlpha().raw().toBuffer();for(let y=0;y<5;y++)for(let x=0;x<5;x++){const f=await sharp(path.join(dir,`gaze/${x}_${y}.png`)).ensureAlpha().raw().toBuffer();let changed=0;for(let py=0;py<320;py++)for(let px=0;px<320;px++){const p=(py*320+px)*4;let diff=false;for(let c=0;c<4;c++)if(f[p+c]!==idle[p+c])diff=true;if(diff){changed++;check(py>=42&&py<=62&&((px>=137&&px<=152)||(px>=168&&px<=183)),`gaze only changes eyes at ${px},${py}`);}}if(x!==2||y!==2)check(changed>0,'gaze has visible change');}
const report={checks,originalFrames:37,originalMs:4390,poses:Object.keys(m.poses).length,clips:m.clips.length,referencedPngs:files.size,sourceHashesUnchanged:true,mirrorExact:true,eyeOnlyChanges:true,visualAcceptance:'pending human review; mathematical checks do not prove natural anatomy'};
await fs.mkdir(path.join(root,'docs/verification'),{recursive:true});await fs.writeFile(path.join(root,'docs/verification/assets.json'),JSON.stringify(report,null,2));console.log(`PASS ${checks} asset checks; ${files.size} PNGs; exact 37-frame GIF retention`);
