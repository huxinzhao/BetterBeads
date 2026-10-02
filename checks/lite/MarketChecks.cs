using BetterBeads.Data;

internal static class MarketChecks
{
    public static void Run(Action<bool,string> check)
    {
        int[] bands=new int[3];
        int[] common=new int[151];
        for(long n=0;n<20000;n++)
        {
            var product=Sample();
            var price=ArtworkMarket.Roll(91387,n,product,_=>2);
            check(DesignStorage.Serialize(price)==DesignStorage.Serialize(ArtworkMarket.Roll(91387,n,product,_=>2)),"same save and sequence gives stable valuation "+n);
            check(ArtworkMarket.Valid(price)&&price.MaterialValue==2,"valid fixed material baseline "+n);
            bands[price.Percent<=150?0:price.Percent<500?1:2]++;
            if(price.Percent<=150)common[price.Percent]++;
            if(price.Collector)check(price.Percent>=500&&price.CollectorMultiplier is >=10 and <=100
                &&price.FinalPrice==ArtworkMarket.Price(2,price.Percent,price.CollectorMultiplier),"collector boost and threshold");
            else check(price.CollectorMultiplier==0,"ordinary work has no collector multiplier");
        }
        check(Math.Abs(bands[0]/20000d-.95)<.01&&Math.Abs(bands[1]/20000d-.04)<.006&&Math.Abs(bands[2]/20000d-.01)<.003,"95/4/1 pricing bands");
        check(common[80]>common[40]&&common[80]>common[120],"80 percent is modal neighborhood");
        check(ArtworkMarket.DrawPercent(0,0)==0&&ArtworkMarket.DrawPercent(95,0)==151&&ArtworkMarket.DrawPercent(99,0)==500,"band lower edges");
        check(ArtworkMarket.Price(100,80,1)==80&&ArtworkMarket.Price(100,500,10)==5000
            &&ArtworkMarket.Price(100,500,100)==50000,"ordinary and collector price boundaries");
        check(ArtworkMarket.Price(1,0,1)==0&&ArtworkMarket.Price(long.MaxValue,1000,100)==int.MaxValue,"zero and overflow clamp");
        var valued=Sample();valued.Valuation=new(){MaterialValue=2,Percent=80,FinalPrice=1};
        check(SimpleCrafting.SaleValue(valued,_=>100)==1,"frozen price ignores later material price");
        var copy=valued.Copy();copy.Valuation!.FinalPrice=0;
        check(valued.Valuation.FinalPrice==1,"valuation copy is independent");
        check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(valued),out _),"valid valuation survives serialization");
        valued.Valuation.Percent=1001;
        check(!DesignStorage.TryReadSnapshot(DesignStorage.Serialize(valued),out _),"invalid valuation is rejected");
        var legacy=Sample();
        check(SimpleCrafting.SaleValue(legacy,_=>2)==4,"legacy item keeps double-material value");
        var previous=Sample();previous.Valuation=new(){Version=2,MaterialValue=2,Percent=80,FinalPrice=1};
        check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(previous),out _),"RC3 valuations remain readable");
        var renamed=Sample();renamed.Design.Name="Different name";renamed.Design.Views["front"].Cells[0]!.Rgba=0x112233FF;
        var sameRoll=ArtworkMarket.Roll(91387,32,renamed,_=>2);
        check(DesignStorage.Serialize(sameRoll)==DesignStorage.Serialize(ArtworkMarket.Roll(91387,32,Sample(),_=>2)),
            "names and colors do not reroll a bill");
        var costly=Sample();
        for(int i=1;i<5;i++)costly.Design.Views["front"].Cells[i]=new(){ColorId="wood",Rgba=0xFFFFFFFF};
        costly.ActualMaterials["(O)388"]=2;
        check(Enumerable.Range(0,64).Any(i=>ArtworkMarket.Roll(91387,i,costly,_=>2).Percent
            !=ArtworkMarket.Roll(91387,i,Sample(),_=>2).Percent),"different material costs do not share a prize sequence");
        legacy.CreatedInCreativeMode=true;
        check(SimpleCrafting.SaleValue(legacy,_=>2)==1,"creative product keeps the RC7 one-gold sale price");
        var progress=new SaveProgress();int inventory=1;
        try{AtomicCraftCommit.Apply(()=>inventory=2,()=>inventory=1,null,progress,
            ()=>{progress.ArtworkValuationSequence++;throw new Exception("simulated progress failure");},
            ()=>progress.ArtworkValuationSequence--);}catch(InvalidOperationException){}
        catch(Exception e)when(e.Message=="simulated progress failure"){}
        check(inventory==1&&progress.ArtworkValuationSequence==0,"failed progress mutation rolls back inventory and sequence");
        var first=Sample();first.Design.Name="Forest Blade";first.Valuation=new(){MaterialValue=2,Percent=600,Collector=true,CollectorMultiplier=37,FinalPrice=444};
        check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(first),out var restored)
            &&restored?.Valuation?.CollectorMultiplier==37&&restored.Valuation?.FinalPrice==444,"new collector multiplier survives serialization");
        var old=first.Copy();old.Valuation=new(){Version=1,MaterialValue=2,Percent=600,Collector=true,FinalPrice=1200};
        check(DesignStorage.TryReadSnapshot(DesignStorage.Serialize(old),out var oldRestored)
            &&oldRestored?.Valuation?.FinalPrice==1200,"previous collector item keeps its 100 times price");
        old.Valuation!.CollectorMultiplier=37;
        check(!ArtworkMarket.Valid(old.Valuation),"old valuation cannot silently change multiplier");
        var second=first.Copy();second.InstanceId="second";second.Design.Name="Moon Blade";
        check(CollectorLedger.Record(progress,19,first)&&!CollectorLedger.Record(progress,19,first),"collector event records once per item");
        check(CollectorLedger.Record(progress,19,second)&&progress.CollectorLetters.Count==1&&progress.CollectorLetters[0].Works.Count==2,"same-day collector pieces share a letter");
        check(!CollectorLedger.Due(progress,19).Any()&&CollectorLedger.Due(progress,20).Single().Works.Count==2,"collector letter becomes due next day");
        var reloaded=System.Text.Json.JsonSerializer.Deserialize<SaveProgress>(DesignStorage.Serialize(progress))!;
        check(CollectorLedger.Due(reloaded,20).Single().Works.Count==2,"collector letter survives save and item disposal");
        CollectorLedger.Remove(progress,19,first.InstanceId);
        check(progress.CollectorLetters[0].Works.Count==1,"rollback removes only its own collector event");
        CollectorLedger.Remove(progress,19,second.InstanceId);
        check(progress.CollectorLetters.Count==0,"empty collector event rolls back cleanly");
    }
    private static ProductSnapshot Sample()
    {
        var design=SimpleCrafting.Blank(SimpleCrafting.Picture16);
        design.Views["front"].Cells[0]=new BeadCell{ColorId="wood",Rgba=0xFFFFFFFF};
        return new ProductSnapshot{Design=design,ActualMaterials=new(){{"(O)388",1}}};
    }
}
