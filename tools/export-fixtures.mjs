import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const manifest=JSON.parse(await fs.readFile(path.join(root,'assets/derived/manifest.json'),'utf8'));
const lines=manifest.sway.durationsMs.map((ms,i)=>`duration.${i}=${ms}`);
for(const c of manifest.clips)lines.push(`clip.${c.from}|${c.to}=${c.durationMs};${c.files.join(',')}`);
await fs.mkdir(path.join(root,'build'),{recursive:true});
await fs.writeFile(path.join(root,'build/engine-fixtures.properties'),lines.join('\n')+'\n');
