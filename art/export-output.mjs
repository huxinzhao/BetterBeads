import fs from 'node:fs';
import path from 'node:path';
import {randomUUID} from 'node:crypto';

// Stage the complete export on the same volumes before replacing any existing file.
export function commitOutputs(outputs,io=fs){
 const pending=[];const token=randomUUID();
 try{
  for(const [file,data]of outputs){
   if(io.existsSync(file)&&!io.statSync(file).isFile())throw Error('导出目标不是普通文件：'+file);
   io.mkdirSync(path.dirname(file),{recursive:true});
   const item={file,temp:file+'.'+token+'.tmp',backup:file+'.'+token+'.bak',moved:false,installed:false};pending.push(item);
   io.writeFileSync(item.temp,data,{flag:'wx'});
  }
  for(const item of pending){
   if(io.existsSync(item.file)){io.renameSync(item.file,item.backup);item.moved=true;}
   io.renameSync(item.temp,item.file);item.installed=true;
  }
 }catch(error){
  const failures=[];
  for(const item of [...pending].reverse())try{
   if(item.installed)io.unlinkSync(item.file);
   if(item.moved){io.renameSync(item.backup,item.file);item.moved=false;}
  }catch(e){failures.push(Error('恢复失败，请保留备份 '+item.backup+': '+e.message));}
  if(failures.length)throw new AggregateError([error,...failures],'导出失败，部分文件需从保留的备份恢复');
  throw error;
 }finally{
  for(const item of pending)try{if(io.existsSync(item.temp))io.unlinkSync(item.temp);}catch{}
 }
 // Cleanup must not turn a successful commit into a reported export failure.
 for(const item of pending)if(item.moved)try{io.unlinkSync(item.backup);}catch(e){console.warn('导出已完成，未能清理备份 '+item.backup+': '+e.message);}
}
