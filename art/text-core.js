(function(root){
'use strict';
function tokens(key,text){
 const result=[];
 if(!key.startsWith('copy.')){
  text=text.replace(/\{\{([^{}]+)\}\}/g,(whole,name)=>{result.push('{{'+name.trim()+'}}');return '';});
  if(text.includes('{{')||text.includes('}}'))throw Error('命名占位符需要完整的双括号，例如 {{name}}。');
 }
  for(let i=0;i<text.length;i++){
   if(text[i]==='{'){
    if(text[i+1]==='{'){i++;continue;}
    let end=text.indexOf('}',i+1),body=text.slice(i+1,end);
    if(end<0||!/^\d+(?:,\s*-?\d+)?(?::[^{}]+)?$/.test(body))throw Error('数字占位符格式错误，请保留 {0} 或 {0:0.##} 等完整格式。');
    result.push('{'+body+'}');i=end;
   }else if(text[i]==='}'){
    if(text[i+1]==='}')i++;else throw Error('存在未配对的右花括号。');
   }
  }
 return result.sort();
}
function validate(key,before,after){
 if(typeof after!=='string')return '文案必须是文字。';
 if(before.trim()&&!after.trim())return '文案不能为空；如需隐藏内容请另行调整显示逻辑。';
 try{if(JSON.stringify(tokens(key,before))!==JSON.stringify(tokens(key,after)))return '请保留全部原占位符及格式，可调整它们的位置。';}catch(e){return e.message;}
 for(let marker of ['^^','[#]'])if(before.split(marker).length!==after.split(marker).length)return '请保留游戏控制标记 '+marker+'。';
 if(before.startsWith('Basic/')){const a=before.split('/'),b=after.split('/');if(b[0]!=='Basic'||a.length!==b.length||a.slice(4).join('/')!==b.slice(4).join('/'))return '请保留任务定义的 / 分隔符、Basic 和末尾控制字段。';}
 return '';
}
class CopyDocument{
 constructor(values,contracts=values){if(!values||Array.isArray(values)||typeof values!=='object'||Object.values(values).some(v=>typeof v!=='string')||!Object.keys(values).length)throw Error('需要非空的“文案键: 文字”JSON对象。');this.values=Object.assign(Object.create(null),values);this.saved=Object.assign(Object.create(null),values);this.contracts=Object.assign(Object.create(null),values,contracts);}
 get changed(){return Object.keys(this.values).filter(k=>this.values[k]!==this.saved[k]);}
 get dirty(){return this.changed.length>0;}
 set(key,value){if(!Object.hasOwn(this.values,key))throw Error('不能修改或新增内部文案键。');this.values[key]=value;}
 reset(key){this.set(key,this.saved[key]);}
 issues(){return this.changed.map(key=>({key,error:validate(key,this.contracts[key],this.values[key])})).filter(v=>v.error);}
 serialize(){const issues=this.issues();if(issues.length)throw Error(issues[0].key+'：'+issues[0].error);return JSON.stringify(this.values,null,2)+'\n';}
 markSaved(){this.saved=Object.assign(Object.create(null),this.values);}
}
const api={CopyDocument,tokens,validate};if(typeof module!=='undefined')module.exports=api;else root.CopyCore=api;
})(globalThis);
