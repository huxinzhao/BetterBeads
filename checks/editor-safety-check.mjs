import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {spawnSync} from 'node:child_process';
import {commitOutputs} from '../art/export-output.mjs';
const require=createRequire(import.meta.url),{PixelDocument}=require('../art/pixel-core.js'),{validateSource}=require('../art/source-core.js');
let count=0;function check(name,fn){fn();console.log('PASS '+(++count)+': '+name);}
fs.mkdirSync('.tools',{recursive:true});
const temp=fs.mkdtempSync(path.resolve('.tools/editor-safety-'));
function dom(assets=[]){
 const counts={canvases:0,uploads:0,draws:0,frames:0},nodes=new Map(),queue=[],listeners={};
 function node(){let n={value:'',checked:false,style:{},dataset:{},children:[],clientWidth:512,clientHeight:512,classList:{toggle(){}},replaceChildren(...a){this.children=a;},append(){},setAttribute(){},setPointerCapture(){},getBoundingClientRect(){return {left:0,top:0,width:256,height:256};},click(){return this.onclick?.();}};n.getContext=()=>({fillRect(){},clearRect(){},drawImage(){counts.draws++;},putImageData(){counts.uploads++;},getImageData(){return {data:new Uint8ClampedArray(n.width*n.height*4)};},beginPath(){},moveTo(){},lineTo(){},stroke(){}});return n;}
 const document={getElementById(id){if(!nodes.has(id))nodes.set(id,node());return nodes.get(id);},createElement(tag){if(tag==='canvas')counts.canvases++;return node();},querySelectorAll(){return [];},body:node()};
 document.getElementById('zoom').value='16';document.getElementById('category').value='all';
 const revoked=[],sandbox={document,window:{BEAD_EDITOR_ASSETS:assets,addEventListener(n,f){listeners[n]=f;},confirm(){return true;}},structuredClone,console,setTimeout,Blob,URL:{createObjectURL(){return 'url:'+Math.random();},revokeObjectURL(u){revoked.push(u);}},ImageData:class{constructor(data,w,h){this.data=data;this.width=w;this.height=h;}},requestAnimationFrame(f){counts.frames++;queue.push(f);},counts};
 const context=vm.createContext(sandbox);function run(code){return vm.runInContext(code,context);}
 return {context,document,counts,revoked,listeners,run,flush(){while(queue.length)queue.shift()();}};
}
const html=fs.readFileSync('art/source-editor.html','utf8'),inline=[...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)].map(m=>m[1]).find(s=>s.includes('let source='));
const valid={schema:1,sprites:{test:{width:1,height:1,palette:{a:'11223380'},pixels:['a']}}};
function legacy(source=valid){let d=dom();d.context.window.BEAD_ART_SOURCE=structuredClone(source);d.run(fs.readFileSync('art/source-core.js','utf8'));d.run(inline);return d;}
const old=legacy();old.run("sprite().palette.a='99887788';checkpoint()");const before=old.run('JSON.stringify(source)'),history=old.run('history.length');
for(const bad of [{schema:1,sprites:{}},{schema:1,sprites:[]},{schema:1,sprites:{test:null}},{schema:1,sprites:{test:{width:1,height:1,palette:{},pixels:['a']}}},{schema:1,sprites:{test:{width:1,height:1,palette:{a:'112233',b:'nope'},pixels:['a']}}},{schema:1,sprites:{test:{width:2,height:1,palette:{a:'112233'},pixels:['a']}}}]){
 old.document.getElementById('file').files=[{text:async()=>JSON.stringify(bad)}];await old.document.getElementById('file').onchange();assert.equal(old.run('JSON.stringify(source)'),before);assert.equal(old.run('history.length'),history);
}
check('空图块、空色板、损坏图块及未使用的非法颜色均拒绝载入，作品和撤销历史保留',()=>assert.match(old.document.getElementById('message').textContent,/无法载入/));
let asks=0;old.context.window.confirm=()=>{asks++;return false;};old.document.getElementById('file').files=[{text:async()=>JSON.stringify(valid)}];await old.document.getElementById('file').onchange();
check('有效载入前提示未导出修改，取消后不替换',()=>{assert.equal(asks,1);assert.equal(old.run('JSON.stringify(source)'),before);});
old.context.window.confirm=()=>true;await old.document.getElementById('file').onchange();check('确认后载入完整有效源文件',()=>assert.equal(old.run('JSON.stringify(source)'),JSON.stringify(valid)));
let release;old.document.getElementById('file').files=[{text:()=>new Promise(r=>release=r)}];const waiting=old.document.getElementById('file').onchange();
const latest=structuredClone(valid);latest.sprites.test.palette.a='abcdef80';old.document.getElementById('file').files=[{text:async()=>JSON.stringify(latest)}];await old.document.getElementById('file').onchange();release(JSON.stringify(valid));await waiting;
check('迟到的旧载入结果不能覆盖后选择的文件',()=>assert.equal(old.run('sprite().palette.a'),'abcdef80'));
const alpha=legacy();alpha.document.getElementById('color').value='#445566';alpha.document.getElementById('color').onchange();
check('修改豆色使用RGB实色，撤销仍保留原始资源',()=>{assert.equal(alpha.run('sprite().palette.a'),'445566');alpha.document.getElementById('undo').onclick();assert.equal(alpha.run('sprite().palette.a'),'11223380');});
check('载入旧半透明资源后摆豆使用实色，不批量修改原色',()=>{const solid=alpha.run('solidSymbol()');assert.equal(alpha.run('sprite().palette[symbol]'),'112233');assert.equal(alpha.run('sprite().palette.a'),'11223380');assert.notEqual(solid,'a');});
const transparent=structuredClone(valid);transparent.sprites.test.palette.a='FFFFFF00';const clear=legacy(transparent);check('只有透明颜色且没有点号色键的图块可以正常打开',()=>assert.equal(clear.run('symbol'),'a'));
alpha.run("drawing=true;strokeSymbol='a'");alpha.counts.canvases=0;alpha.counts.frames=0;for(let i=0;i<100;i++)alpha.document.getElementById('paint').onpointermove({clientX:1,clientY:1});
check('旧源图块编辑器重复绘制同一像素不排队、不新建画布',()=>{assert.equal(alpha.counts.frames,0);assert.equal(alpha.counts.canvases,0);});
const source=JSON.parse(fs.readFileSync('art/source.json','utf8'));validateSource(source);
function exportFixture(name,input,options=[]){let dir=path.join(temp,name),art=path.join(dir,'art'),out=path.join(dir,'BetterBeads/assets');fs.mkdirSync(art,{recursive:true});fs.mkdirSync(path.join(out,'Products'),{recursive:true});for(const f of ['export.mjs','export-output.mjs','source-core.js'])fs.copyFileSync('art/'+f,path.join(art,f));fs.writeFileSync(path.join(art,'source.json'),JSON.stringify(input));fs.writeFileSync(path.join(art,'source-data.js'),'old source script');for(const f of ['Workbench.png','Products/Hat.png','Icons.png'])fs.writeFileSync(path.join(out,f),'original');let result=spawnSync(process.execPath,[path.join(art,'export.mjs'),...options],{encoding:'utf8'});return {result,dir,art,out};}
const missing=structuredClone(source);delete missing.sprites['product/Hat'];const fail=exportFixture('missing',missing,['--reset-sprite=Workbench']);
check('缺少末尾必需图块时PNG、源脚本和重置源文件均未覆盖',()=>{assert.notEqual(fail.result.status,0);assert.match(fail.result.stderr,/缺少必需图块/);for(const f of ['Workbench.png','Products/Hat.png','Icons.png'])assert.equal(fs.readFileSync(path.join(fail.out,f),'utf8'),'original');assert.equal(fs.readFileSync(path.join(fail.art,'source-data.js'),'utf8'),'old source script');assert.deepEqual(JSON.parse(fs.readFileSync(path.join(fail.art,'source.json'))),missing);});
const wrong=structuredClone(source);wrong.sprites['product/Hat'].width=1;wrong.sprites['product/Hat'].pixels=Array(80).fill(Object.keys(wrong.sprites['product/Hat'].palette)[0]);const wrongExport=exportFixture('wrong-size',wrong);
check('有效但错误尺寸的必需图块提前拒绝',()=>{assert.notEqual(wrongExport.result.status,0);assert.match(wrongExport.result.stderr,/图块尺寸必须/);assert.equal(fs.readFileSync(path.join(wrongExport.out,'Workbench.png'),'utf8'),'original');});
const success=exportFixture('valid',source);check('完整源文件正常生成16张PNG及源脚本',()=>{assert.equal(success.result.status,0,success.result.stderr);assert.equal(fs.readFileSync(path.join(success.out,'Workbench.png')).readUInt32BE(16),16);assert.equal(fs.readFileSync(path.join(success.out,'Products/Hat.png')).readUInt32BE(20),80);assert.ok(fs.existsSync(path.join(success.art,'contact-sheet.png')));assert.ok(fs.readFileSync(path.join(success.art,'source-data.js'),'utf8').startsWith('window.BEAD_ART_SOURCE='));});
const tx=path.join(temp,'transactions');fs.mkdirSync(tx);const a=path.join(tx,'a'),b=path.join(tx,'b'),c=path.join(tx,'new');fs.writeFileSync(a,'old-a');fs.writeFileSync(b,'old-b');
let injected=false;const failing=new Proxy(fs,{get(obj,key){if(key==='renameSync')return (from,to)=>{if(to===b&&from.endsWith('.tmp')&&!injected){injected=true;throw Error('injected write failure');}return fs.renameSync(from,to);};return obj[key];}});
check('替换中途失败回滚已覆盖文件并移除本次新增文件',()=>{assert.throws(()=>commitOutputs(new Map([[a,'new-a'],[c,'new-c'],[b,'new-b']]),failing),/injected/);assert.equal(fs.readFileSync(a,'utf8'),'old-a');assert.equal(fs.readFileSync(b,'utf8'),'old-b');assert.equal(fs.existsSync(c),false);assert.deepEqual(fs.readdirSync(tx).sort(),['a','b']);});
const stageFail=new Proxy(fs,{get(obj,key){if(key==='writeFileSync')return (file,...args)=>{if(file.startsWith(b+'.'))throw Error('injected staging failure');return fs.writeFileSync(file,...args);};return obj[key];}});
check('暂存失败时原文件完全保留且清理临时文件',()=>{assert.throws(()=>commitOutputs(new Map([[a,'new-a'],[b,'new-b']]),stageFail),/staging/);assert.equal(fs.readFileSync(a,'utf8'),'old-a');assert.deepEqual(fs.readdirSync(tx).sort(),['a','b']);});
commitOutputs(new Map([[a,'new-a'],[b,'new-b'],[c,'new-c']]));check('完整替换成功且清理备份',()=>{assert.equal(fs.readFileSync(a,'utf8'),'new-a');assert.deepEqual(fs.readdirSync(tx).sort(),['a','b','new']);});
const perf=dom();perf.run(fs.readFileSync('art/pixel-core.js','utf8'));perf.run(fs.readFileSync('art/pixel-editor.js','utf8'));
perf.run(`model=new PixelDocument(16,16,new Uint8ClampedArray(1024));current={name:'fixture',path:'fixture.png'};region={x:0,y:0,w:16,h:16};drawing=true;strokeColor=[10,20,30,255];last=[15,15];model.set(15,15,strokeColor,region);draw();counts.copies=0;let origSlice=model.pixels.slice.bind(model.pixels);model.pixels.slice=(...a)=>{counts.copies++;return origSlice(...a)};`);
Object.assign(perf.counts,{canvases:0,uploads:0,draws:0,frames:0});
for(let i=0;i<100;i++)perf.document.getElementById('paint').onpointermove({clientX:249,clientY:249});perf.flush();
check('同像素100次移动：0画布分配、0复制、0上传、0绘制',()=>{assert.equal(perf.counts.canvases,0);assert.equal(perf.counts.copies,0);assert.equal(perf.counts.uploads,0);assert.equal(perf.counts.draws,0);assert.equal(perf.counts.frames,0);});
perf.run('last=[0,0]');for(let i=1;i<=15;i++)perf.document.getElementById('paint').onpointermove({clientX:i*16+1,clientY:1});
check('同帧连续落豆补齐线段，仅安排一次刷新',()=>assert.equal(perf.counts.frames,1));perf.flush();
check('刷新复用底层画布，无整图数组复制',()=>{assert.equal(perf.counts.canvases,0);assert.equal(perf.counts.copies,0);assert.equal(perf.counts.uploads,1);assert.equal(perf.run('model.color(7,0)[3]'),255);});
perf.run('setColor([50,60,70,128])');perf.document.getElementById('color').value='#aabbcc';perf.document.getElementById('color').oninput();
check('PNG编辑器取色和选色只使用RGB实色，不更改未绘制像素',()=>{assert.equal(perf.run('color[3]'),255);perf.run('model.set(0,0,[10,20,30,64],region);palette()');const swatch=perf.document.getElementById('palette').children.find(b=>b.title==='#0A141E');assert.ok(swatch);swatch.onclick();assert.equal(perf.run('color[3]'),255);assert.equal(perf.run('model.color(0,0)[3]'),64);});
const editorHtml=fs.readFileSync('art/editor.html','utf8');check('两个编辑页均移除不透明度控件',()=>{assert.doesNotMatch(html,/id="alpha"/);assert.doesNotMatch(editorHtml,/id="alpha"/);});
const transparentBefore=clear.run('sprite().palette.a');clear.document.getElementById('color').value='#abcdef';clear.document.getElementById('color').onchange();check('编辑空白格对应颜色只创建RGB颜色，不把已有空白填实',()=>{assert.equal(clear.run('sprite().palette.a'),transparentBefore);assert.equal(clear.run('sprite().pixels[0]'),'a');assert.equal(clear.run('sprite().palette[symbol]'),'abcdef');});
let random=99;const d=new PixelDocument(8,8,new Uint8Array(256)),r={x:0,y:0,w:8,h:8};for(let i=0;i<400;i++){random=(Math.imul(random,1664525)+1013904223)>>>0;const action=random%7;if(action<3){d.begin();d.set((random>>>5)%8,(random>>>10)%8,action===0?[0,0,0,0]:[random%256,55,66,128],r);d.end();}else if(action===3)d.undo();else if(action===4)d.redo();else if(action===5)d.markSaved();else{d.begin();d.fill(0,0,[5,6,7,255],r);d.end();}assert.equal(d.dirty,d.pixels.some((v,i)=>v!==d.saved[i]));}
check('400次编辑、撤销、重做和保存后，增量修改状态与完整像素比较一致',()=>assert.ok(d.revision>0));
// A failed directory connection must not replace the current unsaved document.
const candidate={name:'new',path:'new.png',width:1,height:1},connect=dom([candidate]);connect.run(fs.readFileSync('art/pixel-core.js','utf8'));connect.run(fs.readFileSync('art/pixel-editor.js','utf8'));
connect.run("model=new PixelDocument(1,1,new Uint8ClampedArray(4));region={x:0,y:0,w:1,h:1};model.set(0,0,[1,2,3,255],region);current={name:'old',path:'old.png'};directory={name:'old'};urls.set('old.png','old-url');canLeave=async()=>true");
const handle={async getFile(){return {async arrayBuffer(){return new Uint8Array([1]).buffer;},async text(){return '{}';}};}};
const dir={name:'new',async getDirectoryHandle(){throw Error('no subdirectory');},async getFileHandle(){return handle;}};connect.context.window.showDirectoryPicker=async()=>dir;let closed=0;connect.context.createImageBitmap=async()=>({width:2,height:2,close(){closed++;}});
const oldDoc=connect.run('model');await connect.run('connect()');
check('新目录首张PNG尺寸错误时保留旧作品、目录和撤销状态，并释放临时资源',()=>{assert.equal(connect.run('model'),oldDoc);assert.equal(connect.run('directory.name'),'old');assert.equal(connect.run('model.dirty'),true);assert.equal(closed,1);assert.ok(!connect.revoked.includes('old-url'));assert.ok(connect.revoked.length>0);assert.equal(connect.run('busy'),false);});
const ids=new Set([...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]));for(const m of inline.matchAll(/\$\('([^']+)'\)/g))assert.ok(ids.has(m[1]),m[1]);
console.log(`${count} targeted safety and refresh checks passed. Temporary files only: ${temp}`);
