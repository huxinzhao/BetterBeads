namespace BetterBeads.Data;

/// <summary>Runtime-only analysis. Geometry uses occupancy, never color or canvas size.</summary>
public sealed record WeaponGeometryProfile(int Count,double Area,double Length,double Inertia,double Support,double Tip,double Serration,double Head,
    int RootX,int RootY,int MainCount,double Wave=0);

public sealed record ShapedWeapon(WeaponStatValues Stats,double Reach,double Serration,WeaponGeometryProfile Geometry,double SwingTimeScale=.95)
{
    public double Bleeding=>Math.Max(Serration,Geometry.Wave*.5);
}

internal sealed class WeaponGeometryCache
{
    private long version=long.MinValue;private ShapedWeapon? value;
    internal int Analyses{get;private set;}
    public ShapedWeapon Get(long changeVersion,Blueprint design)
    {
        if(value is null||version!=changeVersion){value=WeaponGeometry.Evaluate(design);version=changeVersion;Analyses++;}
        return value;
    }
}

public static class WeaponGeometry
{
    public const string Version="simple-7";
    public static bool HasShapeEffects(string? version)=>version is "simple-5" or "simple-6" or Version;
    public static bool HasSwingTiming(string? version)=>version is "simple-6" or Version;
    public static double BleedingStrength(ProductSnapshot snapshot)=>snapshot.WeaponRulesVersion==Version
        ?snapshot.FinalStats.GetValueOrDefault("shapeBleeding",snapshot.FinalStats.GetValueOrDefault("shapeSerration"))
        :snapshot.FinalStats.GetValueOrDefault("shapeSerration");
    private const double Diagonal=1.4142135623730951;
    private static readonly (int X,int Y)[] Neighbors={(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)};
    internal static HashSet<int> MainComponent(BeadGrid grid,ProductUse use,SwordOrientation orientation)
    {
        int w=grid.Width,h=grid.Height;
        double gripX=orientation==SwordOrientation.Vertical&&use==ProductUse.Sword?w/2d:0;
        double Grip(int i)=>Square(i%w-gripX)+Square(i/w-(h-1));
        var unseen=Enumerable.Range(0,grid.Cells.Count).Where(i=>grid.Cells[i] is not null).ToHashSet();
        var parts=new List<HashSet<int>>();
        while(unseen.Count>0)
        {
            int start=unseen.Min();var part=new HashSet<int>{start};var queue=new Queue<int>();queue.Enqueue(start);unseen.Remove(start);
            while(queue.TryDequeue(out int p))foreach(int n in Adjacent(p,w,h))if(unseen.Remove(n)){part.Add(n);queue.Enqueue(n);}
            parts.Add(part);
        }
        return parts.OrderByDescending(p=>p.Count).ThenBy(p=>p.Min(Grip)).ThenBy(p=>p.Min()).FirstOrDefault()??new();
    }
    // Frozen numerical profiles measured from unmodified vanilla Galaxy sprites (96/74/119 pixels).
    // No game texture is embedded, and texture replacement mods cannot change these baselines.
    public static WeaponGeometryProfile Reference(ProductUse use)=>use switch
    {
        ProductUse.Dagger=>new(74,59.54421909606165,14.712670403551895,.25308651917510877,.012086701153828943,0,0,.04810234892828502,1,12,74),
        ProductUse.Hammer=>new(119,88.3268505066532,20.29898987322333,.42024047654824465,.0028147915529029032,0,0,1,0,15,119),
        _=>new(96,83.12686175263613,20.349433241279208,.2624671546861841,.003276706521717486,.1359785334211191,0,.09383589934904286,0,13,96)
    };

    public static WeaponGeometryProfile Analyze(BeadGrid grid,ProductUse use,SwordOrientation orientation)
    {
        int w=grid.Width,h=grid.Height;
        var occupied=Enumerable.Range(0,grid.Cells.Count).Where(i=>grid.Cells[i] is not null).ToHashSet();
        if(occupied.Count==0)return new(0,0,1,1,1,0,0,0,0,0,0);
        double gripX=orientation==SwordOrientation.Vertical&&use==ProductUse.Sword?w/2d:0,gripY=h-1;
        double Grip(int i)=>Square(i%w-gripX)+Square(i/w-gripY);
        var main=MainComponent(grid,use,orientation);
        var skeleton=Thin(main,w,h);
        var ends=skeleton.Where(i=>Adjacent(i,w,h).Count(skeleton.Contains)<=1).ToArray();
        int gripBead=main.OrderBy(Grip).ThenBy(i=>i).First();
        int coreRoot=skeleton.OrderBy(p=>DistanceSquared(p,gripBead,w)).ThenBy(Grip).ThenBy(i=>i).First();
        // Thinning can collapse a solid plate to a point. Connect its grip-facing edge
        // back to the centerline, rather than treating the entire plate as a handle.
        var rootNeighbors=Adjacent(coreRoot,w,h).Where(skeleton.Contains).ToArray();
        double directionX=orientation==SwordOrientation.Vertical&&use==ProductUse.Sword?0:-1,directionY=1;
        if(rootNeighbors.Length==1){directionX=coreRoot%w-rootNeighbors[0]%w;directionY=coreRoot/w-rootNeighbors[0]/w;}
        double norm=Math.Max(1,Math.Sqrt(Square(directionX)+Square(directionY)));int root=coreRoot;
        for(double step=.5;step<=Math.Max(w,h);step+=.5)
        {
            int x=(int)Math.Round(coreRoot%w+directionX/norm*step),y=(int)Math.Round(coreRoot/w+directionY/norm*step);
            if(x<0||y<0||x>=w||y>=h||!main.Contains(y*w+x))break;
            root=y*w+x;
        }
        var toRoot=Distances(main,root,w,h);int cursor=coreRoot;
        skeleton.Add(cursor);
        while(cursor!=root)
        {
            int next=Adjacent(cursor,w,h).Where(main.Contains).OrderBy(p=>toRoot[p]+Step(cursor,p,w)).ThenBy(p=>toRoot[p]).ThenBy(p=>p).First();
            if(toRoot[next]>=toRoot[cursor])break;
            skeleton.Add(next);cursor=next;
        }
        var distances=Distances(skeleton,root,w,h);
        // End spurs shorter than two cells remain in the topology, but cannot become the principal tip.
        bool Principal(int p)
        {
            int previous=-1,current=p;double length=0;
            while(length<2)
            {
                var next=Adjacent(current,w,h).Where(skeleton.Contains).Where(n=>n!=previous).ToArray();
                if(next.Length!=1)return next.Length==0||length>=2;
                int n=next[0];length+=Step(current,n,w);previous=current;current=n;
            }
            return true;
        }
        ends=skeleton.Where(i=>Adjacent(i,w,h).Count(skeleton.Contains)<=1).ToArray();
        var candidates=ends.Where(p=>p!=root&&Principal(p)).ToArray();
        int tip=(candidates.Length>0?candidates:skeleton.ToArray()).OrderByDescending(p=>distances.GetValueOrDefault(p)).ThenBy(p=>p).First();
        double path=Math.Max(1,distances.GetValueOrDefault(tip));
        var nearest=new Dictionary<int,int>();
        foreach(int p in main)nearest[p]=skeleton.OrderBy(s=>DistanceSquared(p,s,w)).ThenBy(s=>distances.GetValueOrDefault(s)).ThenBy(s=>s).First();
        double t0=use==ProductUse.Hammer?.5:.1,t1=use==ProductUse.Hammer?.7:.3;
        double area=main.Sum(p=>Smooth((distances.GetValueOrDefault(nearest[p])/path-t0)/(t1-t0)));
        double length=Math.Max(1,main.Max(p=>Math.Sqrt(DistanceSquared(root,p,w)))+.5);
        double inertia=occupied.Average(p=>DistanceSquared(root,p,w))/(length*length);
        // Distance layers follow curves. Parallel branches contribute their combined supporting widths.
        var radii=skeleton.ToDictionary(p=>p,p=>Radius(p,main,w,h));
        double support=double.PositiveInfinity;
        for(int layer=4;layer<=17;layer++)
        {
            double at=path*layer/20;
            var section=skeleton.Where(p=>Math.Abs(distances.GetValueOrDefault(p)-at)<=.8).ToArray();
            if(section.Length==0)continue;
            // Count a short path-length band, so diagonal raster staircases don't look like
            // repeated one-pixel necks. Looped/parallel support is included in the same band.
            double widths=Square(main.Count(p=>Math.Abs(distances.GetValueOrDefault(nearest[p])-at)<=1.5)/3d);
            double load=main.Where(p=>distances.GetValueOrDefault(nearest[p])>at)
                .Sum(p=>Math.Max(.5,Math.Sqrt(DistanceSquared(root,p,w))));
            if(load>0)support=Math.Min(support,widths/load);
        }
        if(!double.IsFinite(support))support=1;
        double tipScore=TipScore(main,skeleton,distances,radii,tip,path,w);
        var principal=new HashSet<int>{tip};cursor=tip;
        while(cursor!=root)
        {
            int next=Adjacent(cursor,w,h).Where(skeleton.Contains).OrderBy(p=>distances[p]+Step(cursor,p,w)).ThenBy(p=>distances[p]).ThenBy(p=>p).First();
            if(distances[next]>=distances[cursor])break;principal.Add(next);cursor=next;
        }
        var bladeProjection=main.ToDictionary(p=>p,p=>principal.OrderBy(s=>DistanceSquared(p,s,w)).ThenBy(s=>distances[s]).ThenBy(s=>s).First());
        double serration=use==ProductUse.Hammer?0:Serration(main,bladeProjection,distances,path,t1,w,h);
        double wave=use==ProductUse.Hammer?0:WaveBlade(main,principal,bladeProjection,distances,path,t1,w);
        double neck=skeleton.Where(p=>distances[p]/path is >.2 and <.6).Select(p=>radii[p]*2).DefaultIfEmpty(1).Average();
        double head=main.Where(p=>distances[nearest[p]]/path>.7).Select(p=>radii[nearest[p]]*2).DefaultIfEmpty(1).Max();
        return new(occupied.Count,Math.Max(.05,area),length,Math.Max(.001,inertia),Math.Max(.000001,support),tipScore,serration,
            Math.Clamp((head/Math.Max(1,neck)-1.5)/2.5,0,1),root%w,root/w,main.Count,wave);
    }

    public static ShapedWeapon Evaluate(Blueprint design)
    {
        var g=Analyze(design.Views["front"],design.Use,design.SwordOrientation);var r=Reference(design.Use);
        double support=Math.Clamp(g.Support/r.Support,0,1),ratio=g.Area/r.Area;
        double alpha=design.Use switch{ProductUse.Dagger=>ratio<1?.35:.10,ProductUse.Hammer=>ratio<1?.35:.25,_=>ratio<1?.30:.15};
        double damage=Math.Pow(ratio,alpha)*(.8+.2*support)*(1-.1*Math.Max(g.Serration,g.Wave*.5));
        var baseline=SimpleCrafting.Stats(design.Use,SimpleCrafting.Metal(design));
        var factors=design.Use switch{ProductUse.Dagger=>(2d,6d,.5),ProductUse.Hammer=>(2.5,4d,1.5),_=>(2d,4d,1d)};
        double burden=factors.Item1*Math.Log2(Math.Max(1,g.Count)/ (double)r.Count)
            +factors.Item2*Math.Log2(g.Length/r.Length)+factors.Item3*Math.Log2(Math.Clamp(g.Inertia/r.Inertia,.25,4));
        // Keep the reference sprite nimble, but dense large artwork progressively feels heavy.
        // This depends on placed beads, not the canvas size, and remains zero through 256 beads.
        burden+=16*Square(Math.Clamp((g.Count-256)/768d,0,1));
        int shift=(int)Math.Round(burden<0?burden/2:burden,MidpointRounding.AwayFromZero);
        shift=Math.Clamp(shift,design.Use==ProductUse.Hammer?-3:-4,26);
        int Round(int n)=>Math.Max(1,(int)Math.Round(n*damage,MidpointRounding.AwayFromZero));
        // Native animation timing is 400 - speed*40. Values >=10 can become non-positive.
        // Use a small frozen time multiplier for the reference bonus instead of +1 integer speed.
        var stats=baseline with{MinDamage=Round(baseline.MinDamage),MaxDamage=Round(baseline.MaxDamage),Speed=Math.Clamp(baseline.Speed-shift,-20,8),
            CritChance=baseline.CritChance+(float)(g.Tip*(design.Use==ProductUse.Dagger?.02:design.Use==ProductUse.Sword?.01:0)),
            Knockback=baseline.Knockback*(float)(design.Use==ProductUse.Hammer?1+.35*g.Head*support:1)};
        return new(stats,Math.Clamp(g.Length/r.Length,.5,2),g.Serration,g);
    }

    private static double WaveBlade(HashSet<int> main,HashSet<int> principal,Dictionary<int,int> projection,
        Dictionary<int,double> distance,double path,double activeStart,int w)
    {
        var blade=principal.Where(p=>distance[p]/path>activeStart).OrderBy(p=>distance[p]).ToArray();
        if(blade.Length<7)return 0;
        int start=blade[0],end=blade[^1];double dx=end%w-start%w,dy=end/w-start/w;
        double span=Math.Sqrt(dx*dx+dy*dy);if(span<6)return 0;dx/=span;dy/=span;
        var points=main.Where(p=>distance[projection[p]]/path>activeStart).Select(p=>
        {
            double x=p%w-start%w,y=p/w-start/w;
            return(Along:x*dx+y*dy,Across:-x*dy+y*dx);
        }).ToArray();
        var samples=new List<(double Along,double Across,double Width)>();
        for(double at=1;at<span;at+=1)
        {
            var band=points.Where(p=>Math.Abs(p.Along-at)<=1.1).ToArray();if(band.Length<2)continue;
            samples.Add((at,band.Average(p=>p.Across),band.Max(p=>p.Across)-band.Min(p=>p.Across)+1));
        }
        if(samples.Count<6||span/samples.Average(p=>p.Width)<2.2)return 0;
        // Follow repeated centerline turns, not jagged exterior edges. Two humps can
        // lie on the same side of the chord (the vanilla Wicked Kris does this).
        // A single crescent/hook has only one turn and must not acquire bleeding.
        var first=samples[0];var last=samples[^1];var offsets=samples.Select(sample=>sample.Across
            -(first.Across+(last.Across-first.Across)*(sample.Along-first.Along)/(last.Along-first.Along))).ToArray();
        double extreme=offsets[0],lastTurn=extreme,minimumProminence=double.PositiveInfinity;
        int direction=0,turns=0;
        foreach(double value in offsets.Skip(1))
        {
            if(direction==0){if(Math.Abs(value-extreme)>=.55)direction=Math.Sign(value-extreme);else continue;}
            if((value-extreme)*direction>=0){extreme=value;continue;}
            if(Math.Abs(value-extreme)<.55)continue;
            minimumProminence=Math.Min(minimumProminence,Math.Abs(extreme-lastTurn));
            lastTurn=extreme;turns++;direction=-direction;extreme=value;
        }
        return turns>=3?Math.Clamp((minimumProminence-.4)/.6,0,1):0;
    }

    private static double TipScore(HashSet<int> main,HashSet<int> skeleton,Dictionary<int,double> distances,Dictionary<int,double> radii,int tip,double path,int w)
    {
        if(path<4)return 0;
        var distal=skeleton.Where(p=>distances[p]>path-4&&distances[p]<=path).OrderBy(p=>distances[p]).ToArray();
        if(distal.Length<3)return 0;
        double broad=distal.Take(Math.Max(1,distal.Length/2)).Average(p=>radii[p]);
        double taper=Math.Clamp((broad-radii[tip]) /Math.Max(1,broad),0,1);
        // Tip must be terminal and backed by a connected taper, not an isolated decorative bead.
        return Adjacent(tip,w,main.Max()/w+2).Count(skeleton.Contains)<=1?taper:0;
    }
    private static double Serration(HashSet<int> main,Dictionary<int,int> nearest,Dictionary<int,double> distance,double path,double activeStart,int w,int h)
    {
        // Trace oriented exposed edges. Negative-area contours are holes and are excluded.
        var edges=new HashSet<(int X,int Y,int DX,int DY)>();
        foreach(int p in main)
        {
            int x=p%w,y=p/w;
            if(y==0||!main.Contains(p-w))edges.Add((x,y,1,0));
            if(x==w-1||!main.Contains(p+1))edges.Add((x+1,y,0,1));
            if(y==h-1||!main.Contains(p+w))edges.Add((x+1,y+1,-1,0));
            if(x==0||!main.Contains(p-1))edges.Add((x,y+1,0,-1));
        }
        int teeth=0;double toothCoverage=0,activePerimeter=0;
        while(edges.Count>0)
        {
            var first=edges.OrderBy(e=>e.Y).ThenBy(e=>e.X).ThenBy(e=>e.DX).First();var e=first;
            var contour=new List<(double X,double Y)>();
            do
            {
                contour.Add((e.X,e.Y));edges.Remove(e);
                var options=edges.Where(n=>n.X==e.X+e.DX&&n.Y==e.Y+e.DY).ToArray();
                if(options.Length==0)break;
                e=options.OrderByDescending(n=>e.DX*n.DY-e.DY*n.DX).ThenBy(n=>n.DX).First();
            }while(e!=first&&contour.Count<4096);
            int count=contour.Count;if(count<12)continue;
            double signed=Enumerable.Range(0,count).Sum(i=>contour[i].X*contour[(i+1)%count].Y-contour[(i+1)%count].X*contour[i].Y);
            if(signed<=0)continue;
            var peaks=new List<int>();var stations=new List<double>();double covered=0;
            bool Concave(int at)
            {
                var before=contour[(at+count-1)%count];var current=contour[at%count];var after=contour[(at+1)%count];
                return (current.X-before.X)*(after.Y-current.Y)-(current.Y-before.Y)*(after.X-current.X)<0;
            }
            for(int i=0;i<count;i++)
            {
                var c=contour[i];int bead=main.OrderBy(p=>Square(p%w+.5-c.X)+Square(p/w+.5-c.Y)).First();
                if(distance[nearest[bead]]/path<=activeStart)continue;
                activePerimeter++;
                var a=contour[(i+count-3)%count];var b=contour[(i+3)%count];
                double chord=Math.Sqrt(Square(b.X-a.X)+Square(b.Y-a.Y));if(chord<2)continue;
                double protrusion=((b.X-a.X)*(a.Y-c.Y)-(b.Y-a.Y)*(a.X-c.X))/chord;
                // A tooth needs two nearby re-entrant valleys. Smooth curvature, raster
                // stairs and the lone outer corner of a sickle cannot supply those valleys.
                int left=Enumerable.Range(2,10).FirstOrDefault(n=>Concave((i+count-n)%count));
                int right=Enumerable.Range(2,10).FirstOrDefault(n=>Concave((i+n)%count));
                double station=distance[nearest[bead]];
                if(protrusion>=1.25&&left>0&&right>0&&!peaks.Any(j=>Math.Min(Math.Abs(j-i),count-Math.Abs(j-i))<5)
                    &&!stations.Any(d=>Math.Abs(d-station)<3))
                {peaks.Add(i);stations.Add(station);covered+=Math.Min(10,left+right);}
            }
            // Repeated teeth must also form a coherent run along the blade. Separate
            // shoulders, sleeves or a guard's corners aren't one serrated cutting edge.
            var ordered=stations.OrderBy(d=>d).ToArray();int repeated=0;
            for(int start=0;start+2<ordered.Length;start++)
            {
                double smallest=double.PositiveInfinity,largest=0;
                for(int end=start+1;end<ordered.Length;end++)
                {
                    double pitch=ordered[end]-ordered[end-1];smallest=Math.Min(smallest,pitch);largest=Math.Max(largest,pitch);
                    // A 45-degree raster stair alternating straight/diagonal runs has a
                    // sqrt(2) pitch ratio; don't mistake a smooth crescent for repeated teeth.
                    if(largest>8||largest>smallest*1.3)break;
                    if(end-start>=2)repeated=Math.Max(repeated,end-start+1);
                }
            }
            teeth+=repeated;toothCoverage+=peaks.Count==0?0:covered*repeated/peaks.Count;
        }
        double coverage=toothCoverage/Math.Max(1,activePerimeter);
        if(teeth<3||coverage<.25)return 0;
        return Math.Min(1,Math.Min(teeth/6d,coverage/.5));
    }
    private static HashSet<int> Thin(HashSet<int> main,int w,int h)
    {
        var pixels=new HashSet<int>(main);bool changed;
        do
        {
            changed=false;
            for(int pass=0;pass<2;pass++)
            {
                var remove=new List<int>();
                foreach(int p in pixels)
                {
                    int x=p%w,y=p/w;var n=Neighbors.Select(o=>x+o.X>=0&&x+o.X<w&&y+o.Y>=0&&y+o.Y<h&&pixels.Contains((y+o.Y)*w+x+o.X)).ToArray();
                    int count=n.Count(v=>v),transitions=Enumerable.Range(0,8).Count(i=>!n[i]&&n[(i+1)%8]);
                    if(count is <2 or >6||transitions!=1)continue;
                    if(pass==0?!(n[0]&&n[2]&&n[4])&&!(n[2]&&n[4]&&n[6]):!(n[0]&&n[2]&&n[6])&&!(n[0]&&n[4]&&n[6]))remove.Add(p);
                }
                if(remove.Count==pixels.Count&&pixels.Count>0)remove.Remove(pixels.Min());
                foreach(int p in remove)pixels.Remove(p);changed|=remove.Count>0;
            }
        }while(changed);
        return pixels;
    }
    private static Dictionary<int,double> Distances(HashSet<int> pixels,int root,int w,int h)
    {
        var result=new Dictionary<int,double>{{root,0}};var queue=new PriorityQueue<int,double>();queue.Enqueue(root,0);
        while(queue.TryDequeue(out int p,out double d))
        {
            if(d>result[p])continue;
            foreach(int n in Adjacent(p,w,h).Where(pixels.Contains))
            {double next=d+Step(p,n,w);if(next<result.GetValueOrDefault(n,double.PositiveInfinity)){result[n]=next;queue.Enqueue(n,next);}}
        }
        return result;
    }
    private static double Radius(int p,HashSet<int> main,int w,int h)
    {
        double result=Math.Min(Math.Min(p%w+.5,w-p%w-.5),Math.Min(p/w+.5,h-p/w-.5));
        for(int y=Math.Max(0,p/w-8);y<=Math.Min(h-1,p/w+8);y++)for(int x=Math.Max(0,p%w-8);x<=Math.Min(w-1,p%w+8);x++)
            if(!main.Contains(y*w+x))result=Math.Min(result,Math.Max(.5,Math.Sqrt(Square(x-p%w)+Square(y-p/w))-.5));
        return Math.Max(.5,result);
    }
    private static IEnumerable<int> Adjacent(int p,int w,int h)
    {int x=p%w,y=p/w;foreach(var n in Neighbors)if(x+n.X>=0&&x+n.X<w&&y+n.Y>=0&&y+n.Y<h)yield return (y+n.Y)*w+x+n.X;}
    private static double Step(int a,int b,int w)=>a%w!=b%w&&a/w!=b/w?Diagonal:1;
    private static double DistanceSquared(int a,int b,int w)=>Square(a%w-b%w)+Square(a/w-b/w);
    private static double Square(double n)=>n*n;
    private static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
}
