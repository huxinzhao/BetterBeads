using BetterBeads.Data;

int passed=0;
void Check(bool value,string description){if(!value)throw new Exception(description);passed++;}
Blueprint Make(string template=SimpleCrafting.Sword)
{
    var d=SimpleCrafting.Blank(template);d.Name="first";
    d.Views["front"].Cells[0]=new(){ColorId="source-a",Rgba=0xAA2244FF,MaterialId=template==SimpleCrafting.Sword?"copper":"decoration"};
    return d;
}
var progress=new SaveProgress();var repository=new BlueprintRepository(progress);
var original=Make();Check(repository.Save(original),"initial save");
var copy=original.Copy(true);copy.Name="another";copy.Reference=new(){Width=1,Height=1,Pixels=new uint[]{0xFFFFFFFF}};
copy.Views["front"].Cells[0]!.ColorId="source-b";
Check(BlueprintContent.Find(progress,copy)?.Id==original.Id,"same art with another identity, name, color label, and reference");
Check(BlueprintManualSave.Prepare(progress,copy).Kind==BlueprintSaveKind.Reused,"manual save reuses existing design");
Check(BlueprintManualSave.Prepare(progress,original).Kind==BlueprintSaveKind.Unchanged,"unchanged save skips revision");
var blankName=Make();blankName.Name="";
Check(BlueprintManualSave.Prepare(new SaveProgress(),blankName).Kind==BlueprintSaveKind.NeedsName,"new unique design requires naming");
var doc=new EditorDocument(copy,new ProcessingCatalog(),"front");
doc.AdoptSaved(original);Check(!doc.IsDirty&&doc.Snapshot().Id==original.Id&&!doc.CanUndo,"adopt clears draft identity/history");
Check(progress.BlueprintRecords.Count==1,"adopting does not create a record");
var changed=copy.Copy();changed.Views["front"].Cells[0]!.Rgba=0xAA2245FF;
Check(BlueprintContent.Find(progress,changed) is null,"exact RGBA matters");
Check(BlueprintManualSave.Prepare(progress,changed).Kind==BlueprintSaveKind.Created,"distinct art can create a record");
changed=copy.Copy();changed.Views["front"].Cells[0]!.MaterialId="iron";
Check(BlueprintContent.Find(progress,changed) is null,"material matters");
changed=copy.Copy();changed.SwordOrientation=SwordOrientation.Vertical;
Check(BlueprintContent.Find(progress,changed) is null,"swing mode matters");
changed=copy.Copy();changed.Views["front"].Cells[1]=changed.Views["front"].Cells[0]!.Copy();
Check(BlueprintContent.Find(progress,changed) is null,"bead positions matter");
changed=Make(SimpleCrafting.Sword24);Check(BlueprintContent.Find(progress,changed) is null,"canvas size matters");
changed=Make(SimpleCrafting.Dagger);Check(BlueprintContent.Find(progress,changed) is null,"product type matters");
var first=original.Copy(true);first.Name="older";Check(repository.Save(first),"legacy duplicate fixture");
Check(BlueprintContent.Find(progress,copy)?.Id==new[]{original.Id,first.Id}.OrderBy(id=>id,StringComparer.Ordinal).First(),"stable duplicate selection");
Check(BlueprintContent.Find(progress,original)?.Id==original.Id,"current record wins among legacy duplicates");
var renamed=original.Copy();renamed.Name="new title";
Check(BlueprintManualSave.Prepare(progress,renamed).Kind==BlueprintSaveKind.Updated,"rename updates existing record");
var stale=original.Copy();stale.Revision--;
Check(BlueprintManualSave.Prepare(progress,stale).Kind==BlueprintSaveKind.Conflict,"stale revision never reuses another record");
var before=progress.BlueprintRecords.ToDictionary(p=>p.Key,p=>p.Value);
int deliveries=0;
AtomicCraftCommit.Apply(()=>deliveries++,()=>deliveries--,null,progress);
Check(deliveries==1&&before.OrderBy(p=>p.Key).SequenceEqual(progress.BlueprintRecords.OrderBy(p=>p.Key)),"craft without blueprint is independent");
try{AtomicCraftCommit.Apply(()=>deliveries++,()=>deliveries--,null,progress,()=>throw new Exception("late failure"));}
catch(Exception ex)when(ex.Message=="late failure"){}
Check(deliveries==1&&before.OrderBy(p=>p.Key).SequenceEqual(progress.BlueprintRecords.OrderBy(p=>p.Key)),"craft rollback leaves blueprints untouched");
Console.WriteLine($"Blueprint save checks passed: {passed}");
