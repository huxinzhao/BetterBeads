// Original native-resolution pixel sources. Existing source.json is never regenerated.
// Run: node art/export.mjs. Edit PNGs directly OR edit source.json and export again.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';
import sourceCore from './source-core.js';
import {commitOutputs} from './export-output.mjs';
const here=path.dirname(fileURLToPath(import.meta.url)), out=path.resolve(here,'../BetterBeads/assets');
const P={ink:'392D32',dark:'654538',wood:'A56842',oak:'CE9257',light:'E9BE7E',cream:'F7E8C8',paper:'FFF5DF',teal:'39776C',mint:'80B7A0',white:'FFFBEF',red:'BC5265',blue:'74C4CB',gold:'E8BC55',purple:'9B73CC',stone:'8B929E'};
const hex=c=>P[c]??c;
class Pixel {
 constructor(w,h){this.w=w;this.h=h;this.data=Array(w*h).fill('00000000');}
 p(x,y,c){if(x>=0&&x<this.w&&y>=0&&y<this.h)this.data[y*this.w+x]=hex(c);return this;}
 r(x,y,w,h,c){for(let j=y;j<y+h;j++)for(let i=x;i<x+w;i++)this.p(i,j,c);return this;}
 line(x,y,xx,yy,c){const n=Math.max(Math.abs(xx-x),Math.abs(yy-y));for(let t=0;t<=n;t++)this.p(Math.round(x+(xx-x)*t/(n||1)),Math.round(y+(yy-y)*t/(n||1)),c);return this;}
 paste(a,x,y){for(let j=0;j<a.h;j++)for(let i=0;i<a.w;i++)if(a.data[j*a.w+i]!=='00000000')this.p(x+i,y+j,a.data[j*a.w+i]);return this;}
}
const sprites={}; const add=(name,p)=>sprites[name]=p;
// One-tile workbench: pegboard, iron, storage drawers and firmly grounded legs.
const bench=new Pixel(16,32);
bench.r(2,5,12,11,'dark').r(3,6,10,9,'oak').r(4,7,8,7,'cream');
for(let y=8;y<14;y+=2)for(let x=5;x<12;x+=2)bench.p(x,y,'wood');
bench.p(6,10,'red').p(8,10,'blue').p(10,12,'teal');
bench.r(10,12,4,2,'ink').r(11,11,2,1,'dark').r(10,14,4,3,'teal').r(11,14,2,1,'mint').r(9,17,6,1,'stone');
bench.r(0,17,16,6,'dark').r(1,18,14,3,'oak').line(1,18,14,18,'light');
bench.r(2,18,6,2,'cream').p(3,18,'red').p(5,18,'teal').p(7,19,'gold');
bench.r(1,22,14,5,'wood').r(2,22,5,4,'dark').r(3,23,3,2,'oak').p(4,23,'gold');
bench.r(8,22,5,4,'dark').r(9,23,3,2,'oak').p(10,23,'gold');
bench.r(2,27,3,4,'dark').r(3,27,1,3,'oak').r(11,27,3,4,'dark').r(12,27,1,3,'oak');
bench.line(5,28,10,28,'wood'); add('Workbench',bench);
const materials=[['wood','A87149','E2B17B','704B3C'],['hardwood','7C4F48','B58364','4F353B'],['stone','9397A2','CED0CB','535A6D'],['wool','E5D7B7','FFFAE7','9E8975'],['decoration','D97C97','FFC4C5','864E77'],['copper','CC804C','FFD2A1','794B3E'],['iron','A2BAC7','ECF5E9','54697F'],['gold','E4B745','FFF4AF','947033'],['iridium','9166C4','D5AFF4','53437B'],['emerald','33966C','92D998','245658'],['aquamarine','69BFCA','C7F4DD','3B718C'],['ruby','CE4B63','FFABA0','7C355A'],['amethyst','A170CA','E4B5E9','5F467F'],['topaz','DF943C','FFD690','87563D'],['jade','82B388','C4E2A6','446F68'],['diamond','BCDCE0','FFFEF0','658FAD']];
function bead(m,refined=false){
 const [id,base,hi,shadow]=m,p=new Pixel(16,16),gem=materials.findIndex(x=>x[0]===id)>=9;
 // Metals have a continuous rim, gems a clipped octagonal rim; each has an open hole.
 for(let y=2;y<14;y++)for(let x=2;x<14;x++){
  const dx=x-7.5,dy=y-7.5,inside=gem?Math.abs(dx)+Math.abs(dy)<=8 && Math.max(Math.abs(dx),Math.abs(dy))<=5.5:dx*dx+dy*dy<=34;
  if(!inside)continue;
  const edge=gem?Math.abs(dx)+Math.abs(dy)>=7||Math.max(Math.abs(dx),Math.abs(dy))>=5:dx*dx+dy*dy>=23;
  p.p(x,y,edge?shadow:dy<-1?hi:dy>2?shadow:base);
 }
 p.r(6,5,4,1,shadow).r(5,6,1,4,shadow).r(6,10,4,1,hi).r(10,6,1,4,hi).r(6,6,4,4,'00000000');
 if(id==='wood'||id==='hardwood')p.line(3,9,4,11,hi).p(10,3,shadow).p(12,8,hi);
 if(id==='wool')p.p(3,5,hi).p(2,8,hi).p(5,12,hi).p(12,10,hi).p(10,2,hi);
 if(id==='decoration')p.p(3,6,'blue').p(5,12,'gold').p(11,4,'mint');
 if(refined){p.line(4,13,10,13,'gold').p(3,12,'gold').p(11,12,'gold');p.line(12,0,12,4,'white').line(10,2,14,2,'white').p(12,2,'gold');}
 return p;
}
for(const m of materials)add('bead/'+m[0],bead(m));
for(const m of materials.slice(5))add('bead/refined-'+m[0],bead(m,true));
add('bead/unknown',bead(['unknown','94869D','D5C9D3','51495E']));
// Template art is only the neutral fallback; a player's saved pattern remains their texture.
const picture=new Pixel(16,16).r(1,1,14,14,'dark').r(2,2,12,12,'oak').r(3,3,10,10,'cream');
picture.r(6,5,4,7,'teal').r(4,7,8,3,'teal').r(7,4,2,8,'mint').r(7,11,2,2,'wood');add('product/Picture',picture);
const tree=new Pixel(16,16).r(6,9,4,4,'wood').r(7,9,1,4,'light').r(2,13,12,2,'dark').r(3,13,10,1,'oak');
tree.r(5,2,6,2,'dark').r(3,4,10,5,'dark').r(4,4,8,4,'teal').r(5,3,6,3,'mint').r(5,8,6,2,'teal');add('product/WoodOrnament',tree);
const statue=new Pixel(16,16).r(3,12,10,3,'ink').r(4,12,8,2,'stone').r(5,5,6,7,'ink').r(6,4,4,8,'stone');
statue.r(5,3,2,3,'stone').r(9,3,2,3,'stone').r(6,6,4,3,'BAC1C6').p(6,7,'ink').p(9,7,'ink').r(7,9,2,2,'teal');add('product/StoneStatue',statue);
function sword(short=false){const p=new Pixel(16,16);if(short){p.line(4,12,9,6,'ink').line(5,13,11,7,'ink').line(5,11,9,6,'white').line(6,11,10,7,'blue');}else p.line(3,13,12,4,'ink').line(4,14,14,4,'ink').line(5,12,13,2,'blue').line(5,11,12,2,'white');p.line(3,9,7,13,'dark').line(3,8,8,13,'gold').line(2,13,4,11,'wood').p(2,14,'dark');return p;}
add('product/Sword',sword());add('product/Dagger',sword(true));
const hammer=new Pixel(16,16).line(4,13,11,6,'ink').line(5,13,12,6,'dark').line(5,12,11,6,'oak');
hammer.r(7,2,7,6,'ink').r(8,3,5,4,'purple').line(8,3,12,3,'cream').r(7,3,2,3,'stone').r(12,3,2,4,'stone');add('product/Hammer',hammer);
const hat=new Pixel(20,80);for(let view=0;view<4;view++){
 const y=view*20;hat.r(6,y+5,8,2,'dark').r(4,y+7,12,5,'dark').r(5,y+7,10,4,'oak').r(6,y+6,8,2,'light').r(4,y+10,12,2,'teal').r(2,y+12,16,2,'dark').r(3,y+12,14,1,'light');
 if(view!==3)hat.p(view===2?5:13,y+10,'red').p(view===2?6:12,y+9,'cream');
}add('product/Hat',hat);
const panels=[['panel','cream','dark','light'],['inset','EADABD','A28A6E','paper'],['accent','E5EFDA','teal','mint'],['button-normal','paper','9B795D','white'],['button-hover','FFF0C2','teal','white'],['button-pressed','D9E5C7','teal','mint'],['button-disabled','D8CFC0','A89985','E6DDCE'],['tab-normal','E8D4B3','9B795D','paper'],['tab-selected','D9E5C7','teal','mint'],['background','cream','dark','light']];
for(const [id,base,border,hi]of panels){let p=new Pixel(24,24).r(2,0,20,24,border).r(0,2,24,20,border).r(2,2,20,20,base).line(3,2,20,2,hi).line(2,3,2,20,hi).line(3,21,20,21,border);if(id.includes('selected')||id==='button-pressed')p.line(5,20,18,20,'teal');add('ui/'+id,p);}
function icon(name){const p=new Pixel(16,16),O='ink',T='teal',H='mint',G='gold',C='cream';
 switch(name){
 case'paint':p.line(5,10,12,3,O).line(6,10,13,3,'oak').r(3,10,4,3,T).r(2,13,3,1,H);break;
 case'erase':p.line(3,9,9,3,O).line(4,12,12,4,O).line(3,9,6,12,O).line(9,3,12,6,O).r(6,6,4,4,'red').r(5,9,3,3,C);break;
 case'pick':p.line(3,12,11,4,O).line(4,12,12,4,'blue').line(8,3,12,7,T).p(3,13,'blue');break;
 case'fill':p.line(3,7,7,3,O).line(7,3,12,8,O).line(12,8,7,13,O).line(7,13,3,7,O).r(5,7,5,3,'blue').line(10,3,12,5,'oak').r(12,11,2,3,T);break;
 case'undo':case'redo':p.line(4,6,11,6,T).line(11,6,12,10,T).line(11,11,8,11,T).line(3,6,6,3,T).line(3,6,6,9,T);if(name==='redo')p.data=p.data.flatMap((_,i,a)=>[a[Math.floor(i/16)*16+15-i%16]]);break;
 case'mirror-h':p.line(7,2,7,13,O).line(2,8,5,5,T).line(2,8,5,11,T).line(12,8,9,5,T).line(12,8,9,11,T);break;
 case'mirror-v':p.line(2,7,13,7,O).line(8,2,5,5,T).line(8,2,11,5,T).line(8,12,5,9,T).line(8,12,11,9,T);break;
 case'center':p.r(3,3,10,10,T).r(4,4,8,8,'00000000').line(7,1,7,14,O).line(1,7,14,7,O).r(6,6,3,3,G);break;
 case'import':p.r(3,2,10,12,O).r(4,3,8,10,C).line(7,6,7,11,T).line(5,9,7,11,T).line(7,11,9,9,T);break;
 case'save':p.r(3,2,10,12,O).r(4,3,8,10,T).r(5,3,5,4,C).r(6,9,4,4,C).r(8,3,1,3,'oak');break;
 case'iron':p.r(5,3,6,2,O).r(4,4,2,3,O).r(10,4,2,3,O).r(4,7,8,5,T).r(6,7,6,2,H).r(2,11,12,2,O).r(3,11,10,1,'stone');break;
 case'library':p.r(2,3,12,11,O).r(3,4,5,8,C).r(9,4,4,8,'light').line(8,3,8,13,'wood').line(4,6,6,6,T).line(4,8,6,8,T);break;
 case'processing':p.r(3,3,9,7,O).r(4,4,7,4,'oak').r(6,10,3,3,T).r(5,13,5,1,O).p(6,5,G).p(9,6,'red');break;
 case'grid':p.r(2,2,12,12,O).r(3,3,10,10,C);for(let i=4;i<13;i+=3)p.line(i,3,i,12,'oak').line(3,i,12,i,'oak');break;
 case'reference':p.r(2,3,12,10,O).r(3,4,10,8,C).line(4,10,7,7,T).line(7,7,10,10,T).p(10,5,G);break;
 case'close':p.line(4,4,11,11,O).line(4,11,11,4,O);break;
 case'plus':p.line(3,7,12,7,O).line(7,3,7,12,O);break;
 case'minus':p.line(3,7,12,7,O);break;
 case'creative':p.line(3,12,11,4,T).line(9,2,9,6,G).line(7,4,11,4,G).p(13,8,G).p(5,2,G);break;
 case'lock':p.r(4,7,8,6,O).r(5,8,6,4,G).r(5,3,6,5,O).r(6,4,4,3,C).p(7,10,O);break;
 case'warning':p.line(7,2,1,13,O).line(7,2,14,13,O).line(1,13,14,13,O).line(7,5,7,9,'red').p(7,11,'red');break;
 case'soul':p.r(5,3,6,9,O).r(6,2,4,12,'purple').r(7,4,2,5,'white').r(6,11,4,1,'blue');break;
 default:p.paste(bead(materials[5]),0,0);break;
 }return p;}
const iconIds=['paint','erase','pick','fill','material','undo','redo','mirror-h','mirror-v','center','import','save','iron','library','processing','grid','reference','close','plus','minus','creative','lock','warning','soul'];
for(const id of iconIds)add('icon/'+id,icon(id));
const statusIds=['lock','missing-color','missing-material','unassigned','invalid-material','unsaved','success','refined'];
for(const id of statusIds){const p=new Pixel(12,12);if(id==='lock')p.r(3,1,6,6,'dark').r(4,2,4,4,'00000000').r(2,5,8,6,'dark').r(3,6,6,4,'gold').p(5,8,'dark');
 else if(id==='refined')p.line(6,1,6,10,'gold').line(2,5,10,5,'gold').r(5,4,3,3,'white');
 else if(id==='success')p.line(2,6,5,9,'teal').line(5,9,10,3,'teal');
 else if(id==='unsaved')p.r(2,2,8,8,'dark').r(3,3,6,6,'gold').r(7,2,3,3,'cream');
 else {p.r(2,2,8,8,id==='missing-color'?'purple':id==='unassigned'?'stone':'red').r(5,3,2,4,'white').p(5,8,'white');if(id==='invalid-material')p.line(1,10,10,1,'ink');}add('status/'+id,p);}
const mask=new Pixel(8,8);for(let y=0;y<8;y++)for(let x=0;x<8;x++)if((x-3.5)**2+(y-3.5)**2<=15 && !(x>=3&&x<=4&&y>=3&&y<=4))mask.p(x,y,'FFFFFF');add('BeadMask',mask);
add('ScrollTrack',new Pixel(8,8).r(2,0,4,8,'BBA88C').r(3,0,2,8,'D6C6A9'));
add('ScrollThumb',new Pixel(8,16).r(1,1,6,14,'dark').r(2,2,4,12,'oak').line(2,2,5,2,'light').line(3,6,4,6,'cream').line(3,9,4,9,'cream'));
const board=new Pixel(24,24).r(2,0,20,24,'dark').r(0,2,24,20,'dark').r(2,2,20,20,'oak').r(3,3,18,18,'light').r(5,5,14,14,'dark').r(6,6,12,12,'cream');
for(const [x,y]of [[3,3],[19,3],[3,19],[19,19]])board.r(x,y,2,2,'teal').p(x,y,'mint');
add('Board',board);
function encode(p){const colors=[...new Set(p.data)],symbols='.'+'0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!$%&()*+,-/:;<=>?@[]^_{|}~';if(colors.length>symbols.length)throw Error('Palette too large');return{width:p.w,height:p.h,palette:Object.fromEntries(colors.map((c,i)=>[symbols[i],c])),pixels:Array.from({length:p.h},(_,y)=>p.data.slice(y*p.w,(y+1)*p.w).map(c=>symbols[colors.indexOf(c)]).join(''))};}
const sourcePath=path.join(here,'source.json');
const sourceExists=fs.existsSync(sourcePath),outputs=new Map();
const source=sourceExists?JSON.parse(fs.readFileSync(sourcePath,'utf8')):{schema:1,sprites:Object.fromEntries(Object.entries(sprites).map(([n,p])=>[n,encode(p)]))};
sourceCore.validateSource(source);
const resets=process.argv.slice(2).filter(a=>a.startsWith('--reset-sprite='));
for(const option of resets){const id=option.slice('--reset-sprite='.length);if(!Object.hasOwn(sprites,id))throw Error('Unknown reset sprite: '+id);source.sprites[id]=encode(sprites[id]);}
sourceCore.validateSource(source,Object.fromEntries(Object.entries(sprites).map(([n,p])=>[n,[p.w,p.h]])));
if(!sourceExists||resets.length)outputs.set(sourcePath,JSON.stringify(source,null,2)+'\n');
outputs.set(path.join(here,'source-data.js'),'window.BEAD_ART_SOURCE='+JSON.stringify(source)+';\n');
function decode(s){const p=new Pixel(s.width,s.height);if(s.pixels.length!==s.height||s.pixels.some(r=>r.length!==s.width))throw Error('Invalid pixel rows');p.data=s.pixels.join('').split('').map(c=>{let v=s.palette[c];if(!/^[0-9a-f]{6}([0-9a-f]{2})?$/i.test(v??''))throw Error('Invalid palette');return v;});return p;}
const edited=Object.fromEntries(Object.entries(source.sprites).map(([n,s])=>[n,decode(s)]));
function crc32(b){let c=0xffffffff;for(const v of b){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return(c^0xffffffff)>>>0;}
function chunk(id,buf){let t=Buffer.from(id),len=Buffer.alloc(4),crc=Buffer.alloc(4);len.writeUInt32BE(buf.length);crc.writeUInt32BE(crc32(Buffer.concat([t,buf])));return Buffer.concat([len,t,buf,crc]);}
function png(p){const head=Buffer.alloc(13);head.writeUInt32BE(p.w);head.writeUInt32BE(p.h,4);head[8]=8;head[9]=6;let raw=Buffer.alloc((p.w*4+1)*p.h);for(let y=0;y<p.h;y++)for(let x=0;x<p.w;x++){let c=p.data[y*p.w+x];if(c.length===6)c+='FF';for(let k=0;k<4;k++)raw[y*(p.w*4+1)+1+x*4+k]=parseInt(c.slice(k*2,k*2+2),16);}return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',head),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]);}
function save(name,p){outputs.set(path.join(out,name+'.png'),png(p));}
for(const name of ['Workbench','BeadMask','ScrollTrack','ScrollThumb','Board'])save(name,edited[name]);
for(const id of ['Picture','WoodOrnament','StoneStatue','Sword','Dagger','Hammer','Hat'])save('Products/'+id,edited['product/'+id]);
function atlas(prefix,ids,cols,size,file){const p=new Pixel(cols*size,Math.ceil(ids.length/cols)*size);ids.forEach((id,i)=>p.paste(edited[prefix+id],i%cols*size,Math.floor(i/cols)*size));save(file,p);return Object.fromEntries(ids.map((id,i)=>[id,i]));}
const beadIds=Object.keys(edited).filter(n=>n.startsWith('bead/')).map(n=>n.slice(5));
const beadMap=atlas('bead/',beadIds,8,16,'Beads');
const panelsMap=atlas('ui/',panels.map(p=>p[0]),10,24,'Ui');
const iconsMap=atlas('icon/',iconIds,12,16,'Icons');
const statusMap=atlas('status/',statusIds,8,12,'Status');
const defaults={Schema:1,BeadColumns:8,Beads:beadMap,Ui:panelsMap,Icons:iconsMap,Status:statusMap,Ink:'392D32',MutedInk:'55493D',Canvas:'4D5158',CheckerLight:'E5DFD0',CheckerDark:'CFC8B8',ShowBeadHoles:true,ShowIcons:true,UseNativeUi:true};
const config=path.join(out,'art.json');if(!fs.existsSync(config))outputs.set(config,JSON.stringify(defaults,null,2)+'\n');
outputs.set(path.join(here,'palette.gpl'),'GIMP Palette\nName: Better Beads Workshop\nColumns: 8\n# Original palette\n'+Object.entries(P).map(([n,c])=>[0,2,4].map(i=>parseInt(c.slice(i,i+2),16)).join(' ')+' '+n).join('\n')+'\n');
// Contact sheet uses only the exact export pixels, enlarged by an integer scale.
const preview=new Pixel(960,640).r(0,0,960,640,'cream');
function enlarged(p,x,y,scale){for(let j=0;j<p.h;j++)for(let i=0;i<p.w;i++)if(p.data[j*p.w+i]!=='00000000')preview.r(x+i*scale,y+j*scale,scale,scale,p.data[j*p.w+i]);}
enlarged(edited.Workbench,36,26,7);
beadIds.forEach((id,i)=>enlarged(edited['bead/'+id],190+(i%7)*104,26+Math.floor(i/7)*78,4));
['Picture','WoodOrnament','StoneStatue','Sword','Dagger','Hammer'].forEach((id,i)=>enlarged(edited['product/'+id],38+i*104,366,5));
for(let view=0;view<4;view++){const p=new Pixel(20,20);p.data=edited['product/Hat'].data.slice(view*400,(view+1)*400);enlarged(p,710+(view%2)*110,346+Math.floor(view/2)*64,3);}
iconIds.forEach((id,i)=>enlarged(edited['icon/'+id],36+(i%12)*76,480+Math.floor(i/12)*72,3));
outputs.set(path.join(here,'contact-sheet.png'),png(preview));
const uiPreview=new Pixel(960,440).r(0,0,960,440,'cream');
panels.forEach(([id],i)=>{let p=edited['ui/'+id],x=24+i%5*186,y=24+Math.floor(i/5)*196;for(let yy=0;yy<164;yy++)for(let xx=0;xx<164;xx++){let sx=xx<8?xx:xx>=156?16+xx-156:8+xx%8,sy=yy<8?yy:yy>=156?16+yy-156:8+yy%8;uiPreview.p(x+xx,y+yy,p.data[sy*24+sx]);}});
outputs.set(path.join(here,'ui-contact-sheet.png'),png(uiPreview));
commitOutputs(outputs);
console.log(`Exported ${Object.keys(edited).length} editable sprites; atlas mapping at assets/art.json.`);
