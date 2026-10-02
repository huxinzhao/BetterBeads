(function(root){
'use strict';
class PixelDocument {
 constructor(w,h,p){if(!Number.isInteger(w)||!Number.isInteger(h)||w<1||h<1||p.length!==w*h*4)throw Error('像素尺寸错误');this.width=w;this.height=h;this.pixels=new Uint8ClampedArray(p);this.saved=this.pixels.slice();this.undoStack=[];this.redoStack=[];this.pending=null;this.revision=0;this.changedBytes=0;}
 get dirty(){return this.changedBytes>0;}
 begin(){if(!this.pending)this.pending=this.pixels.slice();}
 end(){if(this.pending&&this.pixels.some((v,i)=>v!==this.pending[i])){this.undoStack.push(this.pending);if(this.undoStack.length>100)this.undoStack.shift();this.redoStack=[];}this.pending=null;}
 replacePixels(p){this.pixels=p;this.changedBytes=0;for(let i=0;i<p.length;i++)if(p[i]!==this.saved[i])this.changedBytes++;this.revision++;}
 undo(){this.end();if(this.undoStack.length){this.redoStack.push(this.pixels.slice());this.replacePixels(this.undoStack.pop());}}
 redo(){this.end();if(this.redoStack.length){this.undoStack.push(this.pixels.slice());this.replacePixels(this.redoStack.pop());}}
 markSaved(){this.saved=this.pixels.slice();this.changedBytes=0;}
 color(x,y){let i=(y*this.width+x)*4;return [this.pixels[i],this.pixels[i+1],this.pixels[i+2],this.pixels[i+3]];}
 set(x,y,c,r){
  if(x<r.x||y<r.y||x>=r.x+r.w||y>=r.y+r.h||x<0||y<0||x>=this.width||y>=this.height)return false;
  let offset=(y*this.width+x)*4,changed=false;
  for(let k=0;k<4;k++){let i=offset+k;if(this.pixels[i]===c[k])continue;this.changedBytes+=(c[k]!==this.saved[i]?1:0)-(this.pixels[i]!==this.saved[i]?1:0);this.pixels[i]=c[k];changed=true;}
  if(changed)this.revision++;return changed;
 }
 line(x,y,xx,yy,c,r){let changed=false,dx=Math.abs(xx-x),dy=-Math.abs(yy-y),sx=x<xx?1:-1,sy=y<yy?1:-1,e=dx+dy;for(;;){changed=this.set(x,y,c,r)||changed;if(x===xx&&y===yy)break;let t=2*e;if(t>=dy){e+=dy;x+=sx;}if(t<=dx){e+=dx;y+=sy;}}return changed;}
 fill(x,y,c,r){if(x<r.x||y<r.y||x>=r.x+r.w||y>=r.y+r.h)return false;let old=this.color(x,y),same=a=>a.every((v,i)=>v===old[i]);if(same(c))return false;let q=[[x,y]],changed=false;while(q.length){let [xx,yy]=q.pop();if(xx<r.x||yy<r.y||xx>=r.x+r.w||yy>=r.y+r.h||!same(this.color(xx,yy)))continue;changed=this.set(xx,yy,c,r)||changed;q.push([xx-1,yy],[xx+1,yy],[xx,yy-1],[xx,yy+1]);}return changed;}
}
async function fingerprint(bytes){return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),n=>n.toString(16).padStart(2,'0')).join('');}
async function writeFile(h,bytes){let w=await h.createWritable();try{await w.write(bytes);await w.close();}catch(e){try{await w.abort();}catch{}throw e;}}
async function savePng(h,folder,name,hash,bytes){let nextHash=await fingerprint(bytes),old=await(await h.getFile()).arrayBuffer();if(await fingerprint(old)!==hash)throw Error('原文件已被其他程序修改，请重新载入后再保存。');await writeFile(await folder.getFileHandle(name,{create:true}),old);if(await fingerprint(await(await h.getFile()).arrayBuffer())!==hash)throw Error('保存前文件发生变化，已停止写入。');await writeFile(h,bytes);return nextHash;}
const api={PixelDocument,fingerprint,savePng};if(typeof module!=='undefined')module.exports=api;else root.PixelCore=api;
})(globalThis);
