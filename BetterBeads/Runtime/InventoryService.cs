using System.Runtime.CompilerServices;
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Objects;

namespace BetterBeads.Runtime;

internal sealed record InventoryBinding(IInventory Items,int Index)
{
    public Item? Read()=>Index<Items.Count?Items[Index]:null;
    public void Write(Item? value)
    {
        while(Items.Count<=Index)Items.Add(null!);
        Items[Index]=value!;
    }
}
internal sealed record InventorySnapshot(string Scope,int BackpackSlots,int ChestCount,IReadOnlyList<InventoryBinding> Bindings,
    InventorySlot?[] Slots);

internal sealed class InventoryService
{
    public ProcessingCatalog Catalog {get;}
    private ConditionalWeakTable<Item,Identity> identities=new();
    private sealed class Identity {public string Value=Guid.NewGuid().ToString("N");}
    private StardewValley.Object? bench;
    private GameLocation? location;
    private Vector2 origin;
    private int radius=5;
    private Farmer? owner;
    public Farmer Owner=>owner??Game1.player;
    public HashSet<Chest>? LockedChests {get;set;}
    public InventoryService(ProcessingCatalog catalog)=>Catalog=catalog;
    private string Identify(Item item)=>identities.GetValue(item,_=>new()).Value;
    public void Bind(StardewValley.Object workbench,int range)
    {Bind(workbench,range,Game1.player);}
    public void Bind(StardewValley.Object workbench,int range,Farmer farmer)
    {owner=farmer;bench=workbench;location=farmer.currentLocation;origin=workbench.TileLocation;radius=Math.Clamp(range,0,20);}
    public int Radius=>radius;
    public bool IsAvailable=>Context.IsWorldReady && bench is not null && location is not null
        && ReferenceEquals(Owner.currentLocation,location) && location.Objects.TryGetValue(origin,out var current) && ReferenceEquals(current,bench);
    public IReadOnlyList<Chest> AvailableChests()=>!IsAvailable?Array.Empty<Chest>():location!.Objects.Pairs
        .Where(p=>MaterialSupply.WithinRange(p.Key.X,p.Key.Y,origin.X,origin.Y,radius))
        .OrderBy(p=>p.Key.Y).ThenBy(p=>p.Key.X).Select(p=>p.Value).OfType<Chest>()
        .Where(c=>c.GetType()==typeof(Chest)&&c.playerChest.Value&&!c.fridge.Value&&!c.giftbox.Value
            &&string.IsNullOrEmpty(c.globalInventoryId.Value)
            &&c.specialChestType.Value is Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest
            &&!c.GetMutex().IsLocked()).ToArray();
#if BEADS_LITE
    public IReadOnlyList<Chest> RequiredChests(Blueprint design,bool creative)
    {
        if(creative)return Array.Empty<Chest>();
        var cost=SimpleCrafting.Cost(design);long needed=cost.Count;
        int Count(IEnumerable<Item?> items)=>items.Select(Describe).Where(s=>s?.ItemId==cost.Item&&s.RawMaterial is not null)
            .Aggregate(0,(n,s)=>(int)Math.Min(int.MaxValue,(long)n+s!.Count));
        needed-=Count(Owner.Items.Take(Owner.MaxItems));var seen=new HashSet<IInventory>{Owner.Items};
        IEnumerable<(Chest Source,long Count)> Sources()
        {
            foreach(var chest in AvailableChests())
            {
                var items=chest.GetItemsForPlayer(Owner.UniqueMultiplayerID);if(!seen.Add(items))continue;
                yield return (chest,Count(items));
            }
        }
        return MaterialSupply.NeededSources(needed,Sources());
    }
#endif
    private bool IsBead(Item item)=>Catalog.Materials.Any(m=>item.QualifiedItemId==BeadItems.BeadId(m.Id));
    internal static bool IsPlainObject(Item item)=>item.GetType()==typeof(StardewValley.Object) && item is StardewValley.Object obj && !obj.bigCraftable.Value && !obj.IsRecipe
        && !obj.questItem.Value && string.IsNullOrEmpty(obj.questId.Value) && !obj.modData.Any();
    public InventorySlot? Describe(Item? item)
    {
        if(item is null)return null;
        bool plain=IsPlainObject(item);
        var source=plain && !IsBead(item)?Catalog.Match(item.QualifiedItemId,item.Category):null;
        if(source is not null && GameplayBalance.ProtectedSource(item.QualifiedItemId,item.Category,PlayMode.AllowValuableSources))source=null;
        string? material=source is null?null:Catalog.Materials.FirstOrDefault(m=>m.Id==source.MaterialId) is {} m?MaterialRules.Canonical(m):null;
        return new(Identify(item),item.QualifiedItemId,item.Stack,item.maximumStackSize(),plain && item.Quality==0 && IsBead(item),material,source?.Yield??0,item.Quality);
    }
    public InventorySnapshot Capture()
    {
        var bindings=new List<InventoryBinding>();var scopes=new List<string>();int chests=0;
        void Add(IInventory items,int capacity,string id)
        {
            scopes.Add(id+":"+capacity+":"+items.Count);
            for(int i=0;i<capacity;i++)bindings.Add(new(items,i));
        }
        int bag=Owner.MaxItems;Add(Owner.Items,bag,"backpack");
        if(IsAvailable)
        {
            scopes.Add("bench:"+Identify(bench!)+":"+origin.X+":"+origin.Y+":"+radius);
            foreach(var pair in location!.Objects.Pairs.Where(p=>MaterialSupply.WithinRange(p.Key.X,p.Key.Y,origin.X,origin.Y,radius))
                .OrderBy(p=>p.Key.Y).ThenBy(p=>p.Key.X))
            {
                if(pair.Value is not Chest chest || chest.GetType()!=typeof(Chest) || !chest.playerChest.Value
                    || chest.fridge.Value || chest.giftbox.Value || !string.IsNullOrEmpty(chest.globalInventoryId.Value)
                    || chest.specialChestType.Value is not (Chest.SpecialChestTypes.None or Chest.SpecialChestTypes.BigChest)
                    || (LockedChests is not null?!LockedChests.Contains(chest):chest.GetMutex().IsLocked()))continue;
                var items=chest.GetItemsForPlayer(Owner.UniqueMultiplayerID);
                if(bindings.Any(b=>ReferenceEquals(b.Items,items)))continue;
                Add(items,Math.Max(items.Count,chest.GetActualCapacity()),Identify(chest)+":"+pair.Key.X+":"+pair.Key.Y);chests++;
            }
        }
        else scopes.Add("unavailable");
        return new(string.Join("|",scopes),bag,chests,bindings,bindings.Select(b=>Describe(b.Read())).ToArray());
    }
    public void Clear(){identities=new();bench=null;location=null;owner=null;LockedChests=null;}
}
