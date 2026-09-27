import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {createRequire} from 'node:module';
const {PixelDocument,fingerprint,savePng}=createRequire(import.meta.url)('../art/pixel-core.js');
const vm=await import('node:vm');
let count=0;function check(name,fn){fn();console.log('PASS '+(++count)+': '+name);}
const html=await fs.readFile('art/editor.html','utf8'),js=await fs.readFile('art/pixel-editor.js','utf8');
check('页面脚本可解析且所有固定控件引用存在',()=>{new vm.Script(js);let ids=new Set([...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]));for(let m of js.matchAll(/\$\('([^']+)'\)/g))assert.ok(ids.has(m[1]),m[1]);});
const index=JSON.parse((await fs.readFile('art/pixel-assets.js','utf8')).split('=').slice(1).join('=').trim().replace(/;$/,''));
check('编辑器覆盖全部42张运行PNG',()=>{assert.equal(index.length,42);assert.equal(index.filter(a=>a.path.startsWith('patterns/')).length,26);});
for(const a of index){const b=await fs.readFile('BetterBeads/assets/'+a.path);assert.equal(b.readUInt32BE(16),a.width);assert.equal(b.readUInt32BE(20),a.height);}
const d=new PixelDocument(8,4,new Uint8Array(8*4*4)),left={x:0,y:0,w:4,h:4},red=[255,0,0,255];
d.begin();d.line(0,0,3,3,red,left);d.end();
check('连续画笔补齐像素且一笔只记一次撤销',()=>{assert.equal(d.undoStack.length,1);for(let i=0;i<4;i++)assert.deepEqual(d.color(i,i),red);});
check('编辑后标记未保存',()=>assert.ok(d.dirty));
d.undo();check('撤销恢复干净状态',()=>assert.equal(d.dirty,false));
d.redo();check('重做恢复整笔像素',()=>assert.deepEqual(d.color(2,2),red));
d.markSaved();d.begin();d.fill(0,3,[0,128,0,255],left);d.end();
check('填色不穿过边界，也不影响相邻图块',()=>{assert.deepEqual(d.color(0,3),[0,128,0,255]);assert.deepEqual(d.color(2,2),red);assert.deepEqual(d.color(4,3),[0,0,0,0]);});
d.undo();check('撤销到已保存状态不误报修改',()=>assert.equal(d.dirty,false));
d.begin();d.set(0,0,[0,0,0,0],left);d.end();check('新笔画清除旧重做记录，橡皮恢复透明',()=>{assert.equal(d.redoStack.length,0);assert.deepEqual(d.color(0,0),[0,0,0,0]);});
let before=d.undoStack.length;d.begin();d.set(7,3,red,left);d.end();check('图集裁切外不写入，也不产生空撤销',()=>assert.equal(d.undoStack.length,before));
const tmp=await fs.mkdtemp(path.resolve('.tools/pixel-editor-check-'));const target=path.join(tmp,'test.png'),backups=path.join(tmp,'backups');await fs.mkdir(backups);
const old=await fs.readFile('BetterBeads/assets/patterns/starter-sprout.png'),next=await fs.readFile('BetterBeads/assets/patterns/robin-house.png');await fs.writeFile(target,old);
function handle(file,options={}){return {async getFile(){return {async arrayBuffer(){let b=await fs.readFile(file);return b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength);}};},async createWritable(){if(options.failOpen)throw Error('denied');let data;return {async write(bytes){data=Buffer.from(bytes);},async close(){if(options.failClose)throw Error('disk failed');await fs.writeFile(file,data);await options.afterClose?.();},async abort(){}};}};}
const folder={async getFileHandle(name){return handle(path.join(backups,name));}};
let hash=await fingerprint(old);let saved=await savePng(handle(target),folder,'first.png.bak',hash,next);
const expectedNew=await fingerprint(next);
check('保存返回新PNG指纹',()=>{assert.equal(saved,expectedNew);assert.notEqual(saved,hash);});
assert.deepEqual(await fs.readFile(target),next);assert.deepEqual(await fs.readFile(path.join(backups,'first.png.bak')),old);console.log('PASS '+(++count)+': 备份与旧PNG逐字节一致，原文件与新PNG逐字节一致');
await assert.rejects(savePng(handle(target),folder,'conflict.bak',hash,old),/其他程序/);assert.deepEqual(await fs.readFile(target),next);console.log('PASS '+(++count)+': 外部文件变化时拒绝覆盖');
await assert.rejects(savePng(handle(target),{async getFileHandle(){return {async createWritable(){throw Error('backup denied');}};}},'denied.bak',saved,old));assert.deepEqual(await fs.readFile(target),next);console.log('PASS '+(++count)+': 备份失败时不碰原文件');
await assert.rejects(savePng(handle(target,{failClose:true}),folder,'failed-write.bak',saved,old),/disk failed/);assert.deepEqual(await fs.readFile(target),next);console.log('PASS '+(++count)+': 写入失败不返回保存成功');
const racing={async getFileHandle(name){return handle(path.join(backups,name),{afterClose:()=>fs.writeFile(target,old)});}};
await assert.rejects(savePng(handle(target),racing,'race.bak',saved,next),/发生变化/);assert.deepEqual(await fs.readFile(target),old);console.log('PASS '+(++count)+': 备份期间发生外部修改，二次复核阻止覆盖');
console.log(`${count} targeted editor checks passed. Test files only: ${tmp}`);
