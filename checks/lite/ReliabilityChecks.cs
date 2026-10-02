using BetterBeads.Data;
using System.Text.Json;

internal static class ReliabilityChecks
{
    internal static void Run(Action<bool,string> check)
    {
        FurnitureTemplates.Configure(SimpleCrafting.Frames().Concat(SimpleCrafting.Ornaments()));
        var design=SimpleCrafting.Blank(SimpleCrafting.Picture16);
        design.Views["front"].Cells[0]=new(){ColorId="test",Rgba=0x123456ff,MaterialId="decoration"};
        var gift=new PendingGalleryGift{Request=Guid.NewGuid().ToString("N"),Npc="Leah",Day=10,
            Painting=new(){Design=design}};
        var outbox=GalleryDelivery.Read(DesignStorage.Serialize(new[]{gift}));
        check(outbox.Count==1&&outbox[0].Request==gift.Request&&outbox[0].Painting.Design.Views["front"].Cells[0]!.Rgba==0x123456ff,
            "gift outbox restores original request and detached painting");
        design.Views["front"].Cells[0]!.Rgba=0xabcdefFF;
        check(outbox[0].Painting.Design.Views["front"].Cells[0]!.Rgba==0x123456ff,"loaded outbox is independent of artwork editing");
        bool rejected=false;try{GalleryDelivery.Read("[{}]");}catch(InvalidDataException){rejected=true;}
        check(rejected,"invalid outbox cannot silently become an empty outbox");
        rejected=false;try{GalleryDelivery.Read(DesignStorage.Serialize(new[]{gift,gift}));}catch(InvalidDataException){rejected=true;}
        check(rejected,"duplicate request identities in outbox are rejected");
        var world=new OnlineProgress();
        check(GalleryDelivery.Receive(world,42,"Farmer",outbox[0])&&world.GiftSequence==1,"host records a native gift exactly once");
        check(!GalleryDelivery.Receive(world,42,"Farmer",outbox[0])&&world.GiftSequence==1,"retried request cannot advance gallery twice");
        var restored=JsonSerializer.Deserialize<OnlineProgress>(DesignStorage.Serialize(world))!;
        check(!GalleryDelivery.Receive(restored,42,"Farmer",outbox[0])&&restored.GiftSequence==1,"deduplication survives host save and reconnect");
        check(!OnlineRules.Advance(restored.Gallery["Leah"],10,_=>6),"same-day gift still waits until next day");
        check(OnlineRules.Advance(restored.Gallery["Leah"],11,_=>6),"delayed recorded gift displays once its original day has passed");
        var newer=new PendingGalleryGift{Request=Guid.NewGuid().ToString("N"),Npc="Leah",Day=12,
            Painting=new(){Design=SimpleCrafting.Blank(SimpleCrafting.Picture32)}};
        GalleryDelivery.Receive(restored,42,"Farmer",newer);
        var late=new PendingGalleryGift{Request=Guid.NewGuid().ToString("N"),Npc="Leah",Day=9,
            Painting=new(){Design=SimpleCrafting.Blank(SimpleCrafting.Picture16)}};
        check(GalleryDelivery.Receive(restored,42,"Farmer",late)&&restored.GiftSequence==2
            &&restored.Gallery["Leah"].Candidates[42].Painting.InstanceId==newer.Painting.InstanceId,
            "late older gift is acknowledged without replacing the newer painting");
        check(OnlineRules.ValidWorld(JsonSerializer.Deserialize<OnlineProgress>("{}")),"old online save defaults missing gallery receipts safely");
        check(OnlineRules.ValidWorld(restored),"updated online progress remains readable");
        IEnumerable<(string Source,long Count)> NeverRead(){throw new Exception("Backpack already supplies all ingredients");}
        check(MaterialSupply.NeededSources(0,NeverReadDeferred()).Count==0,"sufficient backpack avoids inspecting or locking chests");
        IEnumerable<(string Source,long Count)> NeverReadDeferred(){foreach(var v in NeverRead())yield return v;}
        var stores=new[]{("empty",0L),("A",1L),("B",2L),("C",20L)};
        check(MaterialSupply.NeededSources(3,stores).SequenceEqual(new[]{"A","B"}),"only stable-order contributing chests are selected");
        check(MaterialSupply.NeededSources(5,stores).SequenceEqual(new[]{"A","B","C"}),"larger deficits select enough chests without changing order");
        foreach(var size in new[]{(480,420),(640,480),(854,480),(1024,768),(1280,720),(1280,900),(1920,1080)})
        {
            var scale=WorkbenchScale.Calculate(size.Item1,size.Item2,32);
            var layout=LibraryShareFilesLayout.Calculate(SimpleEditorLayout.Calculate(scale.Width,scale.Height).Frame);
            var controls=layout.Rows.Concat(new[]{layout.Title,layout.Note,layout.Previous,layout.Next,layout.Back}).ToArray();
            check(controls.All(r=>layout.Dialog.Contains(r))&&layout.Rows.All(r=>r.Height>=40&&r.Bottom+8<=layout.Note.Y),
                "share file rows fit without covering feedback "+size);
            check(layout.Previous.Right+8<=layout.Next.X&&layout.Next.Right+8<=layout.Back.X
                &&layout.Note.Bottom+8<=layout.Previous.Y,"share file navigation remains separate "+size);
        }
        foreach(var use in new[]{ProductUse.Wallpaper,ProductUse.Flooring})
        {
            var snapshot=new ProductSnapshot{Design=SimpleCrafting.Blank(ProductCategories.For(use)!.TemplateFor(16))};
            snapshot.Design.Views["front"].Cells[0]=new(){ColorId="test",Rgba=0xffffffff,MaterialId="decoration"};
            var drawing=new ProductRenderData(snapshot);
            check(drawing.CacheKey("front",1)!=drawing.CacheKey("front",2),"decoration preview invalidates on art revision "+use);
        }
        check(OnlineRules.Protocol=="1.1.0-RC4-reliability1","changed online gift rules require matching versions");
    }
}
