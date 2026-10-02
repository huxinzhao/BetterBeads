(function(root){
'use strict';
const record=v=>v!==null&&typeof v==='object'&&!Array.isArray(v);
function validateSource(value,required={}){
 if(!record(value)||value.schema!==1||!record(value.sprites))throw Error('不是支持的资源源文件');
 const entries=Object.entries(value.sprites);
 if(!entries.length||entries.length>512)throw Error('源文件必须包含1～512个图块');
 for(const [name,s]of entries){
  if(!name||!record(s)||!Number.isInteger(s.width)||!Number.isInteger(s.height)||s.width<1||s.height<1||s.width>256||s.height>256||!record(s.palette)||!Object.keys(s.palette).length||Object.entries(s.palette).some(([k,c])=>k.length!==1||k.charCodeAt(0)>127||typeof c!=='string'||!/^[0-9a-f]{6}([0-9a-f]{2})?$/i.test(c))||!Array.isArray(s.pixels)||s.pixels.length!==s.height||s.pixels.some(r=>typeof r!=='string'||r.length!==s.width||[...r].some(c=>!Object.hasOwn(s.palette,c))))throw Error('图块数据无效：'+name);
 }
 for(const [name,size]of Object.entries(required)){
  if(!Object.hasOwn(value.sprites,name))throw Error('缺少必需图块：'+name);
  const s=value.sprites[name];if(s.width!==size[0]||s.height!==size[1])throw Error('图块尺寸必须是 '+size.join('×')+'：'+name);
 }
 return value;
}
const api={validateSource};if(typeof module!=='undefined')module.exports=api;else root.ArtSourceCore=api;
})(globalThis);
