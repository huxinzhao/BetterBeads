using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace BetterBeads.Runtime;

internal sealed class WeaponPanel
{
    private readonly Blueprint candidate;
    private readonly ProcessingCatalog catalog;
    private readonly SaveProgress progress;
    private readonly ManufacturingService? manufacturing;
    private readonly ITranslationHelper text;
    private readonly Action<Blueprint?> finish;
    private readonly string[] choices;
    private readonly List<(UiRect Rect,Action Action)> controls=new();
    private int selection,page,mode,effectIndex;
    public WeaponPanel(Blueprint source,ProcessingCatalog catalog,SaveProgress progress,ManufacturingService? manufacturing,
        ITranslationHelper text,Action<Blueprint?> finish)
    {
        candidate=source.Copy();this.catalog=catalog;this.progress=progress;this.manufacturing=manufacturing;this.text=text;this.finish=finish;
        choices=catalog.Materials.Where(m=>WeaponMaterials.IsWeapon(source.Use) && MaterialRules.CanUse(m,source.Use)).Select(m=>m.Id)
            .Concat(source.SupplementaryMaterials.Keys).Distinct(StringComparer.Ordinal).ToArray();
    }
    public void InvalidateLayout()=>controls.Clear();
    public void Cancel(){controls.Clear();finish(null);}
    public void Click(int x,int y)
    {
        foreach(var control in controls.AsEnumerable().Reverse())if(control.Rect.Contains(x,y))
        {controls.Clear();control.Action();return;}
    }
    public void Draw(SpriteBatch b,UiRect frame)
    {
        controls.Clear();var layout=WeaponPanelLayout.Calculate(frame);
        Fill(b,frame,Color.Black*0.7f);ArtResources.Panel(b,layout.Dialog);
        Line(b,text.Get("weapon.panel").ToString(),layout.Title);
        string[] tabs={"weapon.materials","weapon.stats","weapon.effects"};int tabWidth=(layout.Tabs.Width-16)/3;
        for(int i=0;i<tabs.Length;i++)
        {int next=i;Button(b,new(layout.Tabs.X+i*(tabWidth+8),layout.Tabs.Y,tabWidth,layout.Tabs.Height),text.Get(tabs[i]).ToString(),()=>{mode=next;page=0;});}
        var stock=DraftStatus.ReadStock(catalog,manufacturing);
        var recipe=manufacturing?.FindRecipe(candidate.TemplateId);
        var preview=WeaponDraftPreview.Evaluate(candidate,recipe,catalog,progress.UnlockedColors,stock,PlayMode.Creative);
        if(mode==2)DrawEffects(b,layout.Body,preview);
        else if(mode==1)DrawStats(b,layout.Body,preview,recipe);
        else DrawMaterials(b,layout.Body,preview,stock);
        Button(b,Half(layout.Footer,false),text.Get("editor.cancel").ToString(),Cancel);
        Button(b,Half(layout.Footer,true),text.Get("editor.apply").ToString(),()=>finish(candidate.Copy()));
    }
    private void DrawMaterials(SpriteBatch b,UiRect area,WeaponDraftPreview preview,IReadOnlyDictionary<string,int> stock)
    {
        if(choices.Length==0){Line(b,text.Get("weapon.no-materials").ToString(),area);return;}
        string id=choices[selection];int amount=candidate.SupplementaryMaterials.GetValueOrDefault(id);
        bool legal=WeaponMaterials.IsWeapon(candidate.Use) && catalog.Materials.Any(m=>m.Id==id && MaterialRules.CanUse(m,candidate.Use));
        string name=catalog.Materials.Any(m=>m.Id==id)?text.Get("material."+id).ToString():id;
        Button(b,new(area.X,area.Y,area.Width,36),name+" →",()=>selection=(selection+1)%choices.Length);
        Line(b,text.Get("weapon.quantity",new{count=amount}).ToString(),new(area.X,area.Y+44,area.Width,28));
        int needed=preview.Report.Materials.FirstOrDefault(m=>m.Id==id)?.Needed??0;
        Line(b,text.Get(PlayMode.Creative?"creative.material-plan":"weapon.stock",new{needed,owned=stock.GetValueOrDefault(id)}).ToString(),new(area.X,area.Y+76,area.Width,28));
        int width=(area.Width-18)/4;var steps=new[]{-10,-1,1,10};
        for(int i=0;i<steps.Length;i++)
        {
            int step=steps[i];
            Button(b,new(area.X+i*(width+6),area.Y+112,width,36),step>0?"+"+step:step.ToString(),()=>
            {
                int next=(int)Math.Clamp((long)amount+step,0,int.MaxValue);
                if(next==0)candidate.SupplementaryMaterials.Remove(id);else candidate.SupplementaryMaterials[id]=next;
            },step<0?amount>0:legal && amount<int.MaxValue);
        }
        var clearing=new UiRect(area.X,area.Y+156,area.Width,36);
        Button(b,Half(clearing,false),text.Get("weapon.clear-one").ToString(),()=>candidate.SupplementaryMaterials.Remove(id),amount>0);
        Button(b,Half(clearing,true),text.Get("weapon.clear-all").ToString(),()=>candidate.SupplementaryMaterials.Clear(),candidate.SupplementaryMaterials.Count>0);
    }
    private void DrawEffects(SpriteBatch b,UiRect area,WeaponDraftPreview preview)
    {
        var ids=new[]{"ruby","jade","amethyst"}.Concat(candidate.SelectedEffects).Distinct(StringComparer.Ordinal).ToArray();
        effectIndex=Math.Clamp(effectIndex,0,ids.Length-1);string id=ids[effectIndex];
        bool selected=candidate.SelectedEffects.Contains(id);
        var ratio=preview.Effects?.Ratios.FirstOrDefault(r=>r.Id==id);
        Button(b,new(area.X,area.Y,area.Width,36),text.Get("effect.v2."+id)+" →",()=>effectIndex=(effectIndex+1)%ids.Length);
        Line(b,text.Get("effect.ratio",new{ratio=ratio?.Percent.ToString("0.##")??"—",
            state=text.Get("effect.state."+(ratio?.State.ToString()??"Unavailable")),count=candidate.SelectedEffects.Count}).ToString(),new(area.X,area.Y+44,area.Width,28));
        string key=ratio?.State switch
        {
            SpecialEffectState.Weak=>"effect."+id+".weak",SpecialEffectState.Overloaded=>"effect."+id+".overloaded",
            SpecialEffectState.Ready=>"effect.ready",SpecialEffectState.Absent=>"effect.absent",_=>"effect.unavailable"
        };
        string warning=Game1.parseText(ContentText.Format("copy.WeaponPanel.04ea6c801c",$"宝石占比达到20%时收益封顶，超过后不惩罚。当前可选{progress.Workshop?.EffectSlots(PlayMode.Creative)??2}项；高级能力由工坊任务开放。"),Game1.smallFont,area.Width);
        b.DrawString(Game1.smallFont,warning,new Vector2(area.X,area.Y+76),ArtResources.Ink);
        var buttons=new UiRect(area.X,area.Bottom-36,area.Width,36);
        Button(b,Half(buttons,false),text.Get(selected?"effect.deselect":"effect.select").ToString(),()=>
        {
            if(selected)candidate.SelectedEffects.RemoveAll(e=>e==id);else candidate.SelectedEffects.Add(id);
        },selected || (ratio?.State==SpecialEffectState.Ready && candidate.SelectedEffects.Count<(progress.Workshop?.EffectSlots(PlayMode.Creative)??2)));
        Button(b,Half(buttons,true),text.Get("effect.clear").ToString(),()=>candidate.SelectedEffects.Clear(),candidate.SelectedEffects.Count>0);
    }
    private void DrawStats(SpriteBatch b,UiRect area,WeaponDraftPreview preview,ManufacturingRecipe? recipe)
    {
        var lines=new List<string>();
        long total=preview.Report.Materials.Sum(m=>(long)m.Needed),supplements=candidate.SupplementaryMaterials.Values.Sum(n=>(long)n);
        lines.Add(recipe is null?text.Get("weapon.total-unconfigured",new{supplements}).ToString()
            :text.Get("weapon.total",new{total,supplements,minimum=recipe.Template.MinimumMaterials}).ToString());
        if(preview.Stats?.Values is {} stats && preview.Stats.Materials is {} materials)
        {
            lines.Add(text.Get("weapon.averages",new{hardness=materials.AverageHardness.ToString("0.##"),weight=materials.AverageWeight.ToString("0.##")}).ToString());
            lines.Add(text.Get("weapon.damage",new{min=stats.MinDamage,max=stats.MaxDamage}).ToString());
            lines.Add(text.Get("weapon.speed",new{speed=stats.Speed,knockback=stats.Knockback.ToString("0.##")}).ToString());
            lines.Add(text.Get("weapon.critical",new{chance=(stats.CritChance*100).ToString("0.##"),multiplier=stats.CritMultiplier.ToString("0.##")}).ToString());
        }
        else lines.Add(text.Get(recipe?.WeaponRules is null?"editor.weapon-pending":"weapon.fix-materials").ToString());
        if(preview.Effects is {} effects)
        {
            foreach(string effect in effects.Active)lines.Add(SpecialEffects.Summary(effect,effects));
            if(effects.DamagePenaltyPercent>0)lines.Add(text.Get("effect.penalty",new{penalty=effects.DamagePenaltyPercent}).ToString());
        }
        lines.Add(text.Get("weapon.preview-note").ToString());
        foreach(var issue in preview.Report.Issues.Select(i=>i.Code).Distinct())lines.Add(text.Get("issue."+issue).ToString());
        int rows=Math.Max(1,(area.Height-44)/28);page=Math.Clamp(page,0,Math.Max(0,(lines.Count-1)/rows));
        for(int i=0;i<rows && page*rows+i<lines.Count;i++)Line(b,lines[page*rows+i],new(area.X,area.Y+i*28,area.Width,28));
        var nav=new UiRect(area.X,area.Bottom-36,area.Width,36);
        Button(b,Half(nav,false),text.Get("processing.previous").ToString(),()=>page--,page>0);
        Button(b,Half(nav,true),text.Get("processing.next").ToString(),()=>page++,(page+1)*rows<lines.Count);
    }
    private static UiRect Half(UiRect r,bool right)=>new(r.X+(right?(r.Width-8)/2+8:0),r.Y,(r.Width-8)/2,r.Height);
    private void Button(SpriteBatch b,UiRect r,string label,Action action,bool enabled=true)
    {
        ArtResources.ButtonText(b,r,label,enabled);
        if(enabled)controls.Add((r,action));
    }
    private static void Line(SpriteBatch b,string label,UiRect r)
    {
        if(Game1.smallFont.MeasureString(label).X>r.Width)
        {while(label.Length>0 && Game1.smallFont.MeasureString(label+"…").X>r.Width)label=label[..^1];label+="…";}
        b.DrawString(Game1.smallFont,label,new Vector2(r.X,r.Y),ArtResources.Ink);
    }
    private static void Fill(SpriteBatch b,UiRect r,Color color)=>b.Draw(Game1.staminaRect,new Rectangle(r.X,r.Y,r.Width,r.Height),color);
}
