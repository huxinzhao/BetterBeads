using BetterBeads.Data;

internal static class GeometryChecks
{
    public static Blueprint Shape(ProductUse use,int size,Func<int,int,bool> filled,SwordOrientation orientation=SwordOrientation.Vertical)
    {
        var d=SimpleCrafting.Blank(SimpleCrafting.WeaponId(use,size));d.SwordOrientation=use==ProductUse.Sword?orientation:SwordOrientation.Diagonal;
        d.SupplementaryMaterials=new(){{"iridium",0}};
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(filled(x,y))d.Views["front"].Cells[y*size+x]=new(){Rgba=0xffcc88ff,MaterialId="iridium"};
        return d;
    }
    public static void Run(Action<bool,string> check)
    {
        var straight=Shape(ProductUse.Sword,32,(x,y)=>x>=14&&x<=17&&y>=7&&y<=29);
        var curved=Shape(ProductUse.Sword,32,(x,y)=>y>=7&&y<=29&&x>=14+(int)Math.Round(4*Math.Sin((29-y)/22d*Math.PI))&&x<=17+(int)Math.Round(4*Math.Sin((29-y)/22d*Math.PI)));
        var wavy=Shape(ProductUse.Sword,32,(x,y)=>y>=3&&y<=29&&Math.Abs(x-16-(int)Math.Round(3*Math.Sin((29-y)/8d*2*Math.PI)))<=1);
        var sickle=Shape(ProductUse.Sword,32,(x,y)=>x>=15&&x<=17&&y>=15&&y<=29||y>=6&&y<=9&&x>=5&&x<=17||x>=15&&x<=17&&y>=9&&y<=15);
        var loop=Shape(ProductUse.Sword,32,(x,y)=>x>=15&&x<=17&&y>=19&&y<=29||x>=8&&x<=24&&y>=4&&y<=20&&!(x>=11&&x<=21&&y>=7&&y<=17));
        foreach(var d in new[]{straight,curved,sickle,loop})
        {
            var p=WeaponGeometry.Evaluate(d);
            check(double.IsFinite(p.Geometry.Area)&&p.Geometry.Area>0,"finite path-weighted area");
            check(p.Reach is >=.5 and <=2,"reach bounds");check(p.Stats.Speed is >=-20 and <=8,"speed bounds");
            check(p.Geometry.MainCount==p.Geometry.Count,"connected components retained");
            var repaint=d.Copy();foreach(var cell in repaint.Views["front"].Cells.Where(cell=>cell is not null))cell!.Rgba=0x112233ff;
            check(WeaponGeometry.Evaluate(repaint)==p,"colors cannot reroll geometry");
        }
        var s=WeaponGeometry.Evaluate(straight);var c=WeaponGeometry.Evaluate(curved);var sc=WeaponGeometry.Evaluate(sickle);
        check(Math.Abs(s.Stats.MaxDamage-c.Stats.MaxDamage)<15,"curving does not collapse damage");
        check(sc.Geometry.Area>25,"sickle hook is an active connected blade");
        check(sc.Geometry.Serration==0,"smooth single hook is not serration");
        var shifted=Shape(ProductUse.Sword,32,(x,y)=>x>=9&&x<=12&&y>=4&&y<=26);
        check(Math.Abs(WeaponGeometry.Evaluate(shifted).Reach-s.Reach)<.000001,"translation adds no reach");
        var detached=straight.Copy();detached.Views["front"].Cells[0]=new(){Rgba=0xffffffff,MaterialId="iridium"};
        var detachedResult=WeaponGeometry.Evaluate(detached);
        check(detachedResult.Geometry.Area==s.Geometry.Area&&detachedResult.Reach==s.Reach,"isolated ornament adds no blade or reach");
        check(detachedResult.Geometry.Count==s.Geometry.Count+1,"ornament adds weight");
        var full=Shape(ProductUse.Sword,32,(_,_)=>true);var heavy=WeaponGeometry.Evaluate(full);
        check(heavy.Stats.MaxDamage>s.Stats.MaxDamage,"large dense blade increases impact");
        check(heavy.Stats.Speed<s.Stats.Speed,"dense blade is substantially slower");
        check(heavy.Stats.Speed<=-15,"full iridium sword remains very heavy after Galaxy speed calibration");
        check(WeaponGeometry.HasShapeEffects("simple-5")&&WeaponGeometry.HasShapeEffects(WeaponGeometry.Version)
            &&!WeaponGeometry.HasShapeEffects("simple-4"),"frozen previous shape effects remain supported");
        var pointed=Shape(ProductUse.Sword,32,(x,y)=>y>=3&&y<=29&&Math.Abs(x-16)<=Math.Min(3,(y-3)/2));
        var toothed=Shape(ProductUse.Sword,32,(x,y)=>y>=4&&y<=29&&x>=13&&x<=17+(y%4==0?4:y%4==1?2:0));
        var tipResult=WeaponGeometry.Evaluate(pointed);var toothResult=WeaponGeometry.Evaluate(toothed);
        Console.WriteLine("tip "+tipResult.Geometry.Tip+"; teeth "+toothResult.Geometry.Serration);
        check(tipResult.Stats.CritChance>.02&&tipResult.Stats.CritChance<=.030001,"supported tapered tip increases sword critical chance");
        check(toothResult.Geometry.Serration>0,"repeated supported exterior teeth produce bleeding");
        check(WeaponGeometry.Evaluate(loop).Geometry.Serration==0,"enclosed holes do not count as teeth");
        var shortSickle=Shape(ProductUse.Sword,32,(x,y)=>x>=15&&x<=17&&y>=9&&y<=22||y>=6&&y<=9&&x>=5&&x<=17);
        var recurved=Shape(ProductUse.Sword,32,(x,y)=>y>=5&&y<=29&&x>=14+(int)Math.Round(5*Math.Sin((29-y)/24d*Math.PI*1.6))&&x<=17+(int)Math.Round(5*Math.Sin((29-y)/24d*Math.PI*1.6)));
        var smoothSickle=Shape(ProductUse.Sword,32,(x,y)=>x>=15&&x<=17&&y>=14&&y<=29||y<=16&&y>=2&&x>=2&&x<=17
            &&Math.Abs(Math.Sqrt((x-9d)*(x-9d)+(y-10d)*(y-10d))-7)<=1.5);
        var character=Shape(ProductUse.Sword,32,(x,y)=>y>=2&&y<=9&&x>=12&&x<=20||y>=10&&y<=22&&x>=13&&x<=19
            ||y>=23&&y<=30&&(x>=13&&x<=15||x>=17&&x<=19)||y>=11&&y<=19&&(x>=10&&x<=12||x>=20&&x<=22));
        foreach(var d in new[]{shortSickle,recurved,smoothSickle,character})
        {
            var result=WeaponGeometry.Evaluate(d);check(result.Geometry.Area>10&&result.Stats.MinDamage>0,"hook/recurve/character art remains usable");
            check(result.Reach<=2,"curved path cannot create unlimited reach");
        }
        check(WeaponGeometry.Evaluate(smoothSickle).Serration==0,"pixel-staircased smooth crescent is not serrated");
        check(WeaponGeometry.Evaluate(character).Serration==0,"head/shoulder/hand corners do not become a serrated blade");
        var waveResult=WeaponGeometry.Evaluate(wavy);
        check(waveResult.Geometry.Wave>0&&waveResult.Bleeding>0,"repeated blade-center turns produce wave bleeding");
        check(waveResult.Bleeding==Math.Max(waveResult.Serration,waveResult.Geometry.Wave*.5),"wave and teeth do not stack two damage budgets");
        foreach(var d in new[]{straight,curved,sickle,smoothSickle,character,loop,full})
            check(WeaponGeometry.Evaluate(d).Geometry.Wave==0,"straight/hook/character/plate silhouettes do not produce wave bleeding");
        var waveRepaint=wavy.Copy();foreach(var b in waveRepaint.Views["front"].Cells.Where(b=>b is not null))b!.Rgba=0x556677ff;
        check(WeaponGeometry.Evaluate(waveRepaint)==waveResult,"wave bleeding does not depend on colors");
        check(ShapeBleeding.Budget(.5,100,"Bat")==15,"wave-only bleeding capped at fifteen percent over three seconds");
        check(WeaponGeometry.HasSwingTiming("simple-6")&&WeaponGeometry.HasShapeEffects("simple-6"),"previous calibrated rules remain active");
        var oldBleed=new ProductSnapshot{WeaponRulesVersion="simple-6",FinalStats=new(){{"shapeSerration",.4},{"shapeBleeding",.8}}};
        check(WeaponGeometry.BleedingStrength(oldBleed)==.4,"old rules ignore new bleeding keys");
        oldBleed.WeaponRulesVersion=WeaponGeometry.Version;
        check(WeaponGeometry.BleedingStrength(oldBleed)==.8,"new rules use frozen combined bleeding strength");
        var cache=new WeaponGeometryCache();var cached=cache.Get(1,straight);
        long allocations=GC.GetAllocatedBytesForCurrentThread();bool reused=true;
        for(int frame=0;frame<600;frame++)reused&=ReferenceEquals(cached,cache.Get(1,straight));
        check(reused&&cache.Analyses==1&&GC.GetAllocatedBytesForCurrentThread()==allocations,"600 static frames reuse geometry without allocations");
        check(!ReferenceEquals(cached,cache.Get(2,curved))&&cache.Analyses==2,"draft change invalidates geometry once");
        var thinNeck=Shape(ProductUse.Hammer,32,(x,y)=>y>=18&&y<=29&&x==5||y>=3&&y<=17&&x>=0&&x<=13);
        var strongNeck=Shape(ProductUse.Hammer,32,(x,y)=>y>=18&&y<=29&&x>=4&&x<=6||y>=3&&y<=17&&x>=0&&x<=13);
        check(WeaponGeometry.Evaluate(strongNeck).Geometry.Support>WeaponGeometry.Evaluate(thinNeck).Geometry.Support,"support identifies a weak neck");
        var bleed=new ShapeBleeding();bleed.Add(.3);
        check(bleed.Advance(3)==0,"tiny budget has no per-tick damage floor");bleed.Add(.8);
        check(bleed.Advance(3)==1,"fractional budgets accumulate without inflation");
        bleed.Add(12);check(bleed.Advance(.5)==2,"first installment arrives at half a second");
        bleed.Add(6);check(bleed.Advance(.5)==3,"new wound does not delay an existing wound");
        check(bleed.Advance(0)==0,"pause cannot advance wounds");
        check(ShapeBleeding.Budget(1,100,"Skeleton")==0&&ShapeBleeding.Budget(1,100,"GreenSlime")==15
            &&ShapeBleeding.Budget(1,100,"Bat")==30,"living/slime/undead susceptibilities");
        var receipts=new ShapeHitReceipts();
        check(receipts.TryAccept(10,1)&&!receipts.TryAccept(10,1),"duplicate online hit does not add a second wound");
        check(!receipts.TryAccept(10,0)&&receipts.TryAccept(10,3)&&!receipts.TryAccept(10,2),"stale sequence is rejected");
        check(receipts.TryAccept(11,1),"peers have independent hit sequences");
        receipts.Forget(10);check(receipts.TryAccept(10,1),"reconnected peer starts a fresh sequence");
        foreach(var use in new[]{ProductUse.Sword,ProductUse.Dagger,ProductUse.Hammer})
        foreach(int size in new[]{16,24,32})foreach(string metal in SimpleCrafting.Metals)
        {
            var d=Shape(use,size,(x,y)=>x==size/2&&y>0);foreach(var b in d.Views["front"].Cells.Where(b=>b is not null))b!.MaterialId=metal;
            var result=WeaponGeometry.Evaluate(d);check(result.Stats.MinDamage>0&&result.Stats.MaxDamage>=result.Stats.MinDamage,"all size/type/metal stats valid");
        }
        Console.WriteLine($"straight {s.Stats}, curved {c.Stats}, sickle {sc.Stats}, full32 {heavy.Stats}");
        if(Environment.GetEnvironmentVariable("BEADS_GEOMETRY_PREVIEW") is {} output)
        {
            var entries=new[]{("直剑",straight),("等豆数弯剑",curved),("镰刀状剑",sickle),("短柄镰刀状剑",shortSickle),("回弯剑",recurved),("平滑镰刀内弧",smoothSickle),("带孔环刃",loop),("尖端剑",pointed),("锯齿剑",toothed),("蛇形波刃",wavy),("人物轮廓剑",character),("重型满格剑",full),("细柄宽头锤",thinNeck),("支撑更宽的锤",strongNeck)};
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(entries.Select(e=>new{Name=e.Item1,Size=e.Item2.Views["front"].Width,
                Use=e.Item2.Use.ToString(),Pixels=e.Item2.Views["front"].Cells.Select(b=>b?.Rgba??0),Result=WeaponGeometry.Evaluate(e.Item2)})));
        }
    }
}
