'use strict';
const $=id=>document.getElementById(id),{PixelDocument,fingerprint,savePng}=PixelCore;
const assets=window.BEAD_EDITOR_ASSETS,canvas=$('paint'),ctx=canvas.getContext('2d');
let directory=null,model=null,current=null,fileHandle=null,loadedHash='',busy=false,tool='pencil',drawing=false,last=null,strokeColor=null;
let color=[57,125,89,255],region={x:0,y:0,w:1,h:1},mapping={},files=new Map(),urls=new Map();
let rawCanvas=null,rawModel=null,rawRevision=-1,drawPending=false;
function message(text,error=false){$('status').textContent=text;$('status').classList.toggle('error',error);}
function update(){
 const dirty=!!model?.dirty;
 $('save').disabled=busy||!dirty;$('reload').disabled=busy||!model;$('undo').disabled=busy||!model?.undoStack.length;$('redo').disabled=busy||!model?.redoStack.length;
 $('connect').disabled=$('welcomeOpen').disabled=busy;$('region').disabled=busy||!model;
 $('dirty').textContent=!model?'未打开图片':dirty?'有未保存修改':'已保存';$('dirty').classList.toggle('changed',dirty);
 document.title=(dirty?'● ':'')+(current?current.name+' · ':'')+'拼豆像素工坊';document.body.classList.toggle('busy',busy);
}
function hex(c){return '#'+c.slice(0,3).map(n=>n.toString(16).padStart(2,'0')).join('').toUpperCase();}
function setColor(c){if(!c[3]){chooseTool('erase');return;}color=[...c.slice(0,3),255];$('color').value=hex(color);$('hex').value=hex(color);chooseTool('pencil');palette();}
function chooseTool(t){tool=t;document.querySelectorAll('[data-tool]').forEach(b=>{b.classList.toggle('active',b.dataset.tool===t);b.setAttribute('aria-pressed',String(b.dataset.tool===t));});}
function list(){let search=$('search').value.toLowerCase(),filter=$('category').value;let shown=assets.filter(a=>(filter==='all'||(filter==='patterns')===a.path.startsWith('patterns/'))&&(a.name+' '+a.path).toLowerCase().includes(search));
 $('count').textContent=shown.length+' 张';$('assets').replaceChildren(...shown.map(a=>{let b=document.createElement('button');b.className='asset'+(current===a?' active':'');b.disabled=!!directory&&!files.has(a.path);b.title=a.path+(b.disabled?'（此文件夹内缺少）':'');let img=document.createElement('img');img.src=urls.get(a.path)||'../BetterBeads/assets/'+a.path;img.alt='';let d=document.createElement('div'),name=document.createElement('strong'),size=document.createElement('small');name.textContent=a.name;size.textContent=a.width+' × '+a.height+(a.tile?' · 可单格编辑':'');d.append(name,size);b.append(img,d);b.onclick=()=>{if(busy)return;directory?switchFile(a):connect(a);};return b;}));}
async function getFile(dir,path){let parts=path.split('/'),d=dir;for(let p of parts.slice(0,-1))d=await d.getDirectoryHandle(p);return d.getFileHandle(parts.at(-1));}
async function locate(root){for(let parts of [['BetterBeads','assets'],['assets'],[]]){try{let d=root;for(let p of parts)d=await d.getDirectoryHandle(p);await d.getFileHandle('art.json');return d;}catch{}}throw Error('没有找到素材目录。请选择项目根目录、BetterBeads目录或assets目录。');}
async function canLeave(){if(!model?.dirty)return true;return new Promise(resolve=>{let dialog=$('discardDialog');function finish(v){dialog.close();resolve(v);}dialog.oncancel=e=>{e.preventDefault();finish(false);};$('cancelDiscard').onclick=()=>finish(false);$('discardChanges').onclick=()=>finish(true);$('saveChanges').onclick=async()=>{dialog.close();resolve(await save());};dialog.showModal();});}
async function connect(preferred){
 if(busy)return;if(!window.showDirectoryPicker){message('此浏览器不支持直接写入。请用桌面版Edge或Chrome打开本页。',true);return;}
 if(!await canLeave())return;
 if(busy)return;endStroke();busy=true;update();const nextUrls=new Map();
 try{const picked=await window.showDirectoryPicker({id:'betterbeads-pixel-editor',mode:'readwrite'});const next=await locate(picked);const nextFiles=new Map();
 for(let a of assets){try{nextFiles.set(a.path,await getFile(next,a.path));}catch{}}
 if(!nextFiles.size)throw Error('这个目录内没有可编辑的PNG。');
 for(let [path,h]of nextFiles){try{nextUrls.set(path,URL.createObjectURL(await h.getFile()));}catch{}}
 let nextMapping={};try{nextMapping=JSON.parse(await(await(await next.getFileHandle('art.json')).getFile()).text())||{};}catch{}
 const first=(preferred&&nextFiles.has(preferred.path)?preferred:null)||assets.find(a=>a.path==='patterns/starter-sprout.png'&&nextFiles.has(a.path))||assets.find(a=>nextFiles.has(a.path));
 const prepared=await prepare(first,nextFiles,nextMapping);
 for(let u of urls.values())URL.revokeObjectURL(u);
 directory=next;files=nextFiles;mapping=nextMapping;urls=nextUrls;
 applyPrepared(prepared);$('connection').textContent='已连接：'+picked.name+' → assets · '+files.size+' 张图片';message('已连接。修改后点击保存，会直接写回所选目录的PNG。');
 }catch(e){if(urls!==nextUrls)for(let u of nextUrls.values())URL.revokeObjectURL(u);if(e.name!=='AbortError')message(e.message||'打开目录失败',true);}finally{busy=false;update();}
}
async function prepare(a,nextFiles=files,nextMapping=mapping){const handle=nextFiles.get(a.path),file=await handle.getFile(),bytes=await file.arrayBuffer(),bitmap=await createImageBitmap(new Blob([bytes]));let raw=document.createElement('canvas');
 try{if(bitmap.width!==a.width||bitmap.height!==a.height)throw Error('图片尺寸必须是 '+a.width+'×'+a.height+'，当前文件不符合接口。');raw.width=bitmap.width;raw.height=bitmap.height;raw.getContext('2d').drawImage(bitmap,0,0);}finally{bitmap.close();}
 const hash=await fingerprint(bytes),doc=new PixelDocument(a.width,a.height,raw.getContext('2d').getImageData(0,0,a.width,a.height).data);
 let opts=[['all','整张图片']];if(a.tile){let cols=a.width/a.tile,rows=a.height/a.tile,map=nextMapping[a.map]||{};for(let i=0;i<cols*rows;i++){let names=Object.keys(map).filter(k=>map[k]===i);opts.push([String(i),(a.path==='Products/Hat.png'?['正面','右侧','左侧','背面'][i]:names.join(' / '))||'第 '+(i+1)+' 格']);}}
 return {a,handle,hash,doc,opts};
}
function applyPrepared({a,handle,hash,doc,opts}){
 model=doc;current=a;fileHandle=handle;loadedHash=hash;drawing=false;last=null;$('welcome').hidden=true;canvas.hidden=false;
 $('title').textContent=a.name;$('filePath').textContent='assets/'+a.path;$('dimensions').textContent=a.width+' × '+a.height+' 像素 · 保持原生尺寸';
 $('region').replaceChildren(...opts.map(([v,t])=>{let o=document.createElement('option');o.value=v;o.textContent=t;return o;}));$('region').value=a.tile?'0':'all';changeRegion();palette();list();update();
}
async function open(a){applyPrepared(await prepare(a));}
async function switchFile(a){if(busy||a===current)return;if(!await canLeave()||busy)return;endStroke();busy=true;update();try{await open(a);message('正在编辑 '+a.name);}catch(e){message(e.message,true);}finally{busy=false;update();}}
function changeRegion(){if(!model)return;let v=$('region').value,t=current.tile;if(v==='all'||!t)region={x:0,y:0,w:model.width,h:model.height};else{let i=+v,cols=model.width/t;region={x:i%cols*t,y:Math.floor(i/cols)*t,w:t,h:t};}fit();palette();}
function raster(){if(!rawCanvas)rawCanvas=document.createElement('canvas');if(rawModel!==model||rawRevision!==model.revision){rawCanvas.width=model.width;rawCanvas.height=model.height;rawCanvas.getContext('2d').putImageData(new ImageData(model.pixels,model.width,model.height),0,0);rawModel=model;rawRevision=model.revision;}return rawCanvas;}
function requestDraw(){if(drawPending)return;drawPending=true;requestAnimationFrame(()=>{drawPending=false;draw();});}
function draw(){if(!model)return;let z=+$('zoom').value,raw=raster();if(canvas.width!==region.w*z)canvas.width=region.w*z;if(canvas.height!==region.h*z)canvas.height=region.h*z;ctx.clearRect(0,0,canvas.width,canvas.height);ctx.imageSmoothingEnabled=false;ctx.drawImage(raw,region.x,region.y,region.w,region.h,0,0,canvas.width,canvas.height);
 if($('grid').checked&&z>=4){ctx.strokeStyle='#3e39322d';ctx.lineWidth=1;ctx.beginPath();for(let x=0;x<=region.w;x++){ctx.moveTo(x*z+.5,0);ctx.lineTo(x*z+.5,canvas.height);}for(let y=0;y<=region.h;y++){ctx.moveTo(0,y*z+.5);ctx.lineTo(canvas.width,y*z+.5);}ctx.stroke();}
 let p=$('preview'),scale=Math.max(1,Math.min(4,Math.floor(170/region.w)));if(p.width!==region.w*scale)p.width=region.w*scale;if(p.height!==region.h*scale)p.height=region.h*scale;let g=p.getContext('2d');g.clearRect(0,0,p.width,p.height);g.imageSmoothingEnabled=false;g.drawImage(raw,region.x,region.y,region.w,region.h,0,0,p.width,p.height);update();}
function fit(){if(!model)return;let stage=$('stage'),max=Math.min((stage.clientWidth-56)/region.w,(stage.clientHeight-56)/region.h);$('zoom').value=String([2,4,8,12,16,24,32].filter(n=>n<=max).at(-1)||2);draw();}
function palette(){if(!model)return;let counts=new Map();for(let y=region.y;y<region.y+region.h;y++)for(let x=region.x;x<region.x+region.w;x++){let c=model.color(x,y);if(c[3]){let k=hex(c);counts.set(k,(counts.get(k)||0)+1);}}
 $('palette').replaceChildren(...[...counts].sort((a,b)=>b[1]-a[1]).slice(0,64).map(([h])=>{let b=document.createElement('button');b.className='swatch'+(h===hex(color)?' active':'');b.style.background=h;b.title=h;b.setAttribute('aria-label','选择颜色 '+h);b.onclick=()=>setColor([parseInt(h.slice(1,3),16),parseInt(h.slice(3,5),16),parseInt(h.slice(5,7),16),255]);return b;}));}
function point(e){let r=canvas.getBoundingClientRect();return [region.x+Math.max(0,Math.min(region.w-1,Math.floor((e.clientX-r.left)/r.width*region.w))),region.y+Math.max(0,Math.min(region.h-1,Math.floor((e.clientY-r.top)/r.height*region.h)))];}
function endStroke(){if(drawing){drawing=false;model.end();last=null;palette();update();}}
canvas.oncontextmenu=e=>e.preventDefault();canvas.onpointerdown=e=>{if(busy||!model||e.button>2||drawing)return;e.preventDefault();let [x,y]=point(e);if(tool==='pick'&&e.button!==2){setColor(model.color(x,y));return;}model.begin();strokeColor=tool==='erase'||e.button===2?[0,0,0,0]:color.slice();if(tool==='fill'&&e.button!==2){let changed=model.fill(x,y,strokeColor,region);model.end();if(changed)requestDraw();palette();return;}drawing=true;last=[x,y];canvas.setPointerCapture(e.pointerId);if(model.set(x,y,strokeColor,region))requestDraw();};
canvas.onpointermove=e=>{if(!model||busy)return;let [x,y]=point(e);$('position').textContent='像素 '+(x+1)+', '+(y+1)+' · 左键绘制 / 右键擦除';if(drawing&&(x!==last[0]||y!==last[1])){let changed=model.line(...last,x,y,strokeColor,region);last=[x,y];if(changed)requestDraw();}};
canvas.onpointerup=canvas.onpointercancel=canvas.onlostpointercapture=endStroke;
async function save(){if(busy)return false;if(!model?.dirty)return true;endStroke();busy=true;update();try{
 if(current.path.startsWith('patterns/')){let visible=false;for(let i=3;i<model.pixels.length;i+=4){if(model.pixels[i]!==0&&model.pixels[i]!==255)throw Error('参考图纸不能包含半透明像素，请改为不透明颜色或擦除。');visible ||= model.pixels[i]===255;}if(!visible)throw Error('参考图纸至少需要保留一颗豆，不能保存全透明图。');}
 const blob=await new Promise(resolve=>raster().toBlob(resolve,'image/png'));if(!blob)throw Error('PNG编码失败，未写入文件。');let bytes=await blob.arrayBuffer();let backup=await directory.getDirectoryHandle('.pixel-backups',{create:true});
 const name=current.path.replaceAll('/','__')+'.'+new Date().toISOString().replaceAll(':','-')+'.'+crypto.randomUUID().slice(0,8)+'.bak';
 loadedHash=await savePng(fileHandle,backup,name,loadedHash,bytes);model.markSaved();if(urls.has(current.path))URL.revokeObjectURL(urls.get(current.path));urls.set(current.path,URL.createObjectURL(blob));list();$('backupInfo').textContent='上次备份：.pixel-backups/'+name;
 message('已保存到 assets/'+current.path+'；旧文件已备份。');return true;
 }catch(e){message('保存未完成：'+e.message,true);return false;}finally{busy=false;update();}}
$('connect').onclick=$('welcomeOpen').onclick=()=>connect();$('save').onclick=save;$('search').oninput=$('category').onchange=list;
$('region').onchange=()=>{if(!busy){endStroke();changeRegion();}};$('zoom').onchange=$('grid').onchange=draw;$('fit').onclick=fit;
$('undo').onclick=()=>{if(!busy&&model){endStroke();model.undo();draw();palette();}};$('redo').onclick=()=>{if(!busy&&model){model.redo();draw();palette();}};
document.querySelectorAll('[data-tool]').forEach(b=>b.onclick=()=>chooseTool(b.dataset.tool));
$('color').oninput=()=>{let v=$('color').value;setColor([parseInt(v.slice(1,3),16),parseInt(v.slice(3,5),16),parseInt(v.slice(5,7),16),255]);};
$('hex').onchange=()=>{let v=$('hex').value;if(!/^#[\da-f]{6}$/i.test(v)){$('hex').value=hex(color);message('颜色格式为 #RRGGBB，例如 #397D59。',true);return;}$('color').value=v;$('color').oninput();};
$('reload').onclick=async()=>{if(busy||!current||!await canLeave())return;busy=true;update();try{await open(current);message('已重新载入磁盘文件。');}catch(e){message(e.message,true);}finally{busy=false;update();}};
window.addEventListener('beforeunload',e=>{if(model?.dirty||busy){e.preventDefault();e.returnValue='';}});
window.addEventListener('keydown',e=>{let key=e.key.toLowerCase();if((e.ctrlKey||e.metaKey)&&key==='s'){e.preventDefault();if(!$('discardDialog').open&&!busy)save();return;}if($('discardDialog').open||busy||['INPUT','SELECT','TEXTAREA'].includes(e.target.tagName))return;if(e.ctrlKey||e.metaKey){if(key==='z'){e.preventDefault();$(e.shiftKey?'redo':'undo').click();}if(key==='y'){e.preventDefault();$('redo').click();}return;}let t={b:'pencil',e:'erase',g:'fill',i:'pick'}[key];if(t)chooseTool(t);});
if(!window.showDirectoryPicker)$('support').textContent='当前浏览器不支持直接保存，请使用 Edge / Chrome。';
chooseTool(tool);list();update();
