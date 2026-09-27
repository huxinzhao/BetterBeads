using System.Text.Json;
using BetterBeads.Data;

int count=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;}
SaveProgress Personal()=>new(){Workshop=new(),FavoriteColors=BeadPalette.Normalize(null)};
FurnitureTemplates.Configure(DefaultManufacturing.Furniture().Concat(SimpleCrafting.Frames()).Concat(SimpleCrafting.Ornaments()).Concat(FurnitureFinish.Definitions()));
ProductSnapshot Painting(string name)
{
    var d=SimpleCrafting.Blank(SimpleCrafting.Picture16);d.Name=name;
    d.Views["front"].Cells[17]=new(){MaterialId="decoration",ColorId="test",Rgba=0xBB7755FF};
    var recipe=SimpleCrafting.Recipes().Single(r=>r.Template.Id==d.TemplateId);
    return SimpleCrafting.Evaluate(d,recipe,SimpleCrafting.Catalog(),new InventorySlot?[]{null},0,Guid.NewGuid().ToString(),true,true,1,"").Plan!.Product;
}
var first=Painting("First");var second=Painting("Second");var third=Painting("Third");
var host=Personal();host.ArtworkValuationSequence=39;
var remote=Personal();remote.BlueprintRecords["opaque-old-record"]="unknown version";remote.ArtworkValuationSequence=9999;
remote.HiddenLiteTemplates.Add("builtin:sebastian");remote.FavoriteBlueprintIds.Add("opaque-old-record");
remote.RecentBlueprintIds.Add("opaque-old-record");
Check(OnlineRules.ValidPersonal(remote),"preserve opaque legacy records");
OnlineRules.ApplyPersonal(host,remote);
Check(host.ArtworkValuationSequence==39,"personal sync cannot overwrite market sequence");
Check(host.BlueprintRecords["opaque-old-record"]=="unknown version","personal library copied");
Check(host.HiddenLiteTemplates.Contains("builtin:sebastian")&&host.FavoriteBlueprintIds.Contains("opaque-old-record"),"personal preferences copied");
remote.BlueprintRecords.Clear();remote.FavoriteColors[0]=0;
Check(host.BlueprintRecords.Count==1&&host.FavoriteColors[0]!=0,"personal sync detached");
remote.CollectorLetters.Add(new());OnlineRules.ApplyPersonal(host,remote);
Check(host.CollectorLetters.Count==0,"collector letters remain host-owned");
remote.FavoriteColors.Clear();Check(!OnlineRules.ValidPersonal(remote),"malformed palette rejected");
var world=new OnlineProgress();world.Players[22]=Personal();
OnlineRules.Receive(world,"Robin",11,"Host",first,10);
var slot=world.Gallery["Robin"];
Check(!OnlineRules.Advance(slot,10,_=>10),"gift waits until next day");
Check(!OnlineRules.Advance(slot,11,_=>5),"five hearts cannot display");
Check(OnlineRules.Advance(slot,11,_=>6),"six hearts displays gift");
Check(slot.Displayed!.Painting.InstanceId==first.InstanceId,"correct first gift");
Check(!OnlineRules.Advance(slot,12,_=>0)&&slot.Displayed is not null,"lost friendship keeps display");
OnlineRules.Receive(world,"Robin",22,"Guest",second,11);
Check(!OnlineRules.Advance(slot,12,id=>id==22?5:10)&&slot.Displayed!.Giver=="Host","low-heart guest cannot replace host display");
Check(OnlineRules.Advance(slot,13,_=>6)&&slot.Displayed!.Giver=="Guest","guest later qualifies");
OnlineRules.Receive(world,"Robin",11,"Host",third,13);
Check(!OnlineRules.Advance(slot,13,_=>10)&&slot.Displayed!.Giver=="Guest","previous art stays visible until tomorrow");
Check(OnlineRules.Advance(slot,14,_=>10)&&slot.Displayed!.Painting.InstanceId==third.InstanceId,"latest qualifying gift wins");
Check(!OnlineRules.Advance(slot,15,_=>10),"old qualifying candidates never replace newer art");
long sequence=world.GiftSequence;OnlineRules.Receive(world,"Robin",11,"Host",third,14);
Check(world.GiftSequence==sequence,"duplicate gift does not consume sequence");
third.Design.Name="Edited library";Check(slot.Displayed!.Painting.Design.Name=="Third","gift snapshot independent of library");
var loaded=JsonSerializer.Deserialize<OnlineProgress>(DesignStorage.Serialize(world))!;
Check(OnlineRules.ValidWorld(loaded)&&loaded.Gallery["Robin"].Displayed!.Giver=="Host","world survives save roundtrip");
Check(loaded.Players.Count==1&&loaded.Players.ContainsKey(22),"per-player persistence");
loaded.Gallery["Robin"].Candidates[11].Painting.Design.Views.Clear();
Check(!OnlineRules.ValidWorld(loaded),"corrupt gallery prevents overwriting online save");
// The transaction callback owns the global sequence while recipient letters stay personal.
var market=Personal();var recipient=Personal();int material=8,items=0;
try
{
    AtomicCraftCommit.Apply(()=>{material-=4;items++;},()=>{material=8;items=0;},null,recipient,
        ()=>{market.ArtworkValuationSequence++;throw new InvalidOperationException("simulated late failure");},
        ()=>market.ArtworkValuationSequence=0);
}
catch(InvalidOperationException){}
Check(material==8&&items==0&&market.ArtworkValuationSequence==0,"late failure restores inventory and shared valuation");
AtomicCraftCommit.Apply(()=>{material-=4;items++;},()=>{},null,recipient,()=>market.ArtworkValuationSequence++);
Check(material==4&&items==1&&market.ArtworkValuationSequence==1,"successful commit advances one global valuation");
Check(recipient.BlueprintRecords.Count==0,"craft never saves a design");
Check(FurnitureFinish.Definitions().Single(f=>f.Id==FurnitureFinish.Small).NameKey=="product.ornament","small ornament uses small name");
Console.WriteLine($"Online data and transaction checks passed: {count}");
