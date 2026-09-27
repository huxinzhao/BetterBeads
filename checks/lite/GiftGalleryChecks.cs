using BetterBeads.Data;
using System.Text.Json;

internal static class GiftGalleryChecks
{
    public static void Run(Action<bool,string> check)
    {
        FurnitureTemplates.Configure(SimpleCrafting.Frames().Concat(SimpleCrafting.Ornaments()));
        var painting=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Picture16)};
        painting.Design.Name="Forest";
        check(GiftGallery.Eligible(painting),"framed 16 painting can be gifted");
        var large=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Picture32)};
        check(GiftGallery.Eligible(large),"framed 32 painting can be gifted");
        var sword=new ProductSnapshot{Design=SimpleCrafting.Blank(SimpleCrafting.Sword)};
        check(!GiftGallery.Eligible(sword),"weapon cannot enter painting gift flow");
        var progress=new SaveProgress();
        GiftGallery.Receive(progress,"Leah",painting,"Farmer",10);
        painting.Design.Name="Changed in inventory";
        var record=progress.GiftGalleryRecords["Leah"];
        check(record.Latest!.Design.Name=="Forest","gift snapshot detaches from item and design");
        check(!GiftGallery.Advance(record,5,11)&&record.Displayed is null,"five hearts keep painting pending");
        check(!GiftGallery.Advance(record,6,10)&&record.Displayed is null,"same-day display is deferred");
        check(GiftGallery.Advance(record,6,11)&&record.Displayed?.Design.Name=="Forest","six hearts display next day");
        large.Design.Name="New picture";
        GiftGallery.Receive(progress,"Leah",large,"Farmer",12);
        check(record.Displayed!.Design.Name=="Forest"&&!GiftGallery.Advance(record,6,12),"old picture stays during pending replacement");
        check(GiftGallery.Advance(record,6,13)&&record.Displayed?.Design.Name=="New picture","latest gifted painting replaces old one");
        check(!GiftGallery.Advance(record,0,14)&&record.Displayed?.Design.Name=="New picture","relationship drop does not remove display");
        var restored=JsonSerializer.Deserialize<SaveProgress>(DesignStorage.Serialize(progress))!;
        check(restored.GiftGalleryRecords["Leah"].Displayed!.Design.Name=="New picture","gift display survives save and reload");
        restored.BlueprintRecords.Clear();
        check(restored.GiftGalleryRecords["Leah"].Displayed!.Design.Name=="New picture","deleting designs does not delete gifts");
        var settings=JsonSerializer.Deserialize<GiftGallerySettings>(File.ReadAllText("BetterBeads/assets/gift-gallery.json"))!;
        check(settings.IsValid&&settings.Homes.Any(h=>h.Location=="ScienceHouse"&&h.Villagers.Contains("Robin"))
            &&settings.Homes.Any(h=>h.Location=="HarveyRoom")&&settings.Homes.Any(h=>h.Location=="WizardHouse"),
            "editable home placement registry covers shared homes, clinic, and wizard tower");
    }
}
