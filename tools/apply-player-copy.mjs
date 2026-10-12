import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {commitOutputs} from '../art/export-output.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const file=path.resolve(root,process.argv[2]||'docs/RC4_PLAYER_COPY.txt');
if(!file.startsWith(root+path.sep))throw Error('Copy document must be inside the workspace');
const input=fs.readFileSync(file,'utf8');
const records=new Map();let key=null;
for(const line of input.split(/\r?\n/)){
 const header=/^\[([a-zA-Z0-9_.-]+)\]$/.exec(line.trim());
 if(header){key=header[1];if(records.has(key))throw Error('Duplicate text key: '+key);records.set(key,{});continue;}
 const value=/^(中文|英文)\s*=\s*(.*)$/.exec(line);
 if(value){if(!key)throw Error('Text without a key');const locale=value[1]==='中文'?'zh':'default';if(records.get(key)[locale]!==undefined)throw Error('Duplicate language: '+key);records.get(key)[locale]=value[2].trim();}
}
if(!records.size)throw Error('No editable text found');
const tokens=s=>[...s.matchAll(/\{(\d+)(?:[^{}]*)\}/g)].map(m=>m[1]).sort();
const output=new Map();
const catalogs={};
for(const [key,row] of records){
 if(!row.zh||!row.default||JSON.stringify(tokens(row.zh))!==JSON.stringify(tokens(row.default)))throw Error('Missing language or mismatched placeholders: '+key);
}
for(const locale of ['zh','default']){
 const filename=path.join(root,'BetterBeads/i18n',locale+'.json');
 const original=JSON.parse(fs.readFileSync(filename,'utf8').replace(/^\uFEFF/,''));
 for(const [key,row] of records){
  if(original[key]!==undefined&&JSON.stringify(tokens(original[key]))!==JSON.stringify(tokens(row[locale])))throw Error('Changed placeholder contract: '+key);
  original[key]=row[locale];
 }
 output.set(filename,Buffer.from(JSON.stringify(original,null,2)+'\n','utf8'));
 catalogs[locale]=original;
}
output.set(path.join(root,'art/text-data.js'),Buffer.from('window.BEAD_COPY_CATALOG='+JSON.stringify(catalogs.default,null,2)+';\nwindow.BEAD_COPY_CHINESE='+JSON.stringify(catalogs.zh,null,2)+';\n','utf8'));
commitOutputs(output);
console.log('Applied '+records.size+' Chinese / English text entries. No game, Mods or save files were accessed.');
