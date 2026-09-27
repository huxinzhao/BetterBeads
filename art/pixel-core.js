(function(root){
'use strict';
class PixelDocument {
 constructor(w,h,p){if(p.length!==w*h*4)throw Error('像素尺寸错误');this.width=w;this.height=h;this.pixels=new Uint8ClampedArray(p);this.saved=this.pixels.slice();this.undoStack=[];this.redoStack=[];this.pending=null;}
 get dirty(){return this.pixels.some((v,i)=>v!==this.saved[i]);}
 begin(){if(!this.pending)this.pending=this.pixels.slice();}
 end(){if(this.pending&&this.pixels.some((v,i)=>v!==this.pending[i])){this.undoStack.push(this.pending);if(this.undoStack.length>100)this.undoStack.shift();this.redoStack=[];}this.pending=null;}
 undo(){this.end();if(this.undoStack.length){this.redoStack.push(this.pixels.slice());this.pixels=this.undoStack.pop();}}
 redo(){if(this.redoStack.length){this.undoStack.push(this.pixels.slice());this.pixels=this.redoStack.pop();}}
 markSaved(){this.saved=this.pixels.slice();}
 color(x,y){return Array.from(this.pixels.slice((y*this.width+x)*4,(y*this.width+x)*4+4));}
 set(x,y,c,r){if(x>=r.x&&y>=r.y&&x<r.x+r.w&&y<r.y+r.h)this.pixels.set(c,(y*this.width+x)*4);}
 line(x,y,xx,yy,c,r){let dx=Math.abs(xx-x),dy=-Math.abs(yy-y),sx=x<xx?1:-1,sy=y<yy?1:-1,e=dx+dy;for(;;){this.set(x,y,c,r);if(x===xx&&y===yy)break;let t=2*e;if(t>=dy){e+=dy;x+=sx;}if(t<=dx){e+=dx;y+=sy;}}}
 fill(x,y,c,r){let old=this.color(x,y),same=a=>a.every((v,i)=>v===old[i]);if(same(c))return;let q=[[x,y]];while(q.length){let [xx,yy]=q.pop();if(xx<r.x||yy<r.y||xx>=r.x+r.w||yy>=r.y+r.h||!same(this.color(xx,yy)))continue;this.set(xx,yy,c,r);q.push([xx-1,yy],[xx+1,yy],[xx,yy-1],[xx,yy+1]);}}
}
async function fingerprint(bytes){return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),n=>n.toString(16).padStart(2,'0')).join('');}
async function writeFile(h,bytes){let w=await h.createWritable();try{await w.write(bytes);await w.close();}catch(e){try{await w.abort();}catch{}throw e;}}
async function savePng(h,folder,name,hash,bytes){let old=await(await h.getFile()).arrayBuffer();if(await fingerprint(old)!==hash)throw Error('原文件已被其他程序修改，请重新载入后再保存。');await writeFile(await folder.getFileHandle(name,{create:true}),old);if(await fingerprint(await(await h.getFile()).arrayBuffer())!==hash)throw Error('保存前文件发生变化，已停止写入。');await writeFile(h,bytes);return await fingerprint(bytes);}
const api={PixelDocument,fingerprint,savePng};if(typeof module!=='undefined')module.exports=api;else root.PixelCore=api;
})(globalThis);
