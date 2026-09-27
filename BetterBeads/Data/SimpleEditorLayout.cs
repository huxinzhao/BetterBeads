namespace BetterBeads.Data;

/// <summary>Shared Lite geometry for rendering, hit testing and offline previews.</summary>
public static class SimpleEditorLayout
{
    public static EditorLayout Calculate(int width,int height)
    {
        var v=EditorLayout.Calculate(width,height);if(v.TooSmall)return v;
        bool compact=v.UsesDrawers;
        int fw=compact?v.Frame.Width:Math.Max(728,v.Frame.Width),fh=v.Frame.Height;
        var frame=new UiRect((width-fw)/2,v.Frame.Y,fw,fh);
        // Ten logical pixels render as sixteen screen pixels at the standard 1920x1080 UI scale.
        int contentLift=compact?0:10;
        var header=new UiRect(frame.X+24,frame.Y+(compact?12:16)-contentLift,fw-48,40);
        int footerHeight=compact?96:72;
        var footer=new UiRect(header.X,frame.Bottom-(compact?16:24)-footerHeight-contentLift,header.Width,footerHeight);
        var body=new UiRect(header.X,header.Bottom+(compact?4:8),header.Width,footer.Y-header.Bottom-(compact?12:16));
        int left=compact?body.Width:body.Width-280;
        int leftLift=compact?0:16;
        var toolbar=new UiRect(body.X,body.Y-leftLift,left,96);
        // The board viewport shares both toolbar edges; the square bead grid fits inside it.
        var canvas=new UiRect(body.X+8,body.Y+(compact?104:112)-leftLift,left-16,body.Height-(compact?112:120));
        int actionY=compact?footer.Y+56:footer.Bottom-48;
        var make=new UiRect(footer.Right-112,actionY,compact?112:96,compact?40:48);
        var save=new UiRect(make.X-128,actionY,120,make.Height);
        int sideTop=compact?body.Y:body.Y-leftLift+48;
        var side=new UiRect(body.Right-256,sideTop,256,body.Bottom-sideTop);
        return v with{Frame=frame,Header=header,Body=body,Footer=footer,CanvasToolbar=toolbar,Canvas=canvas,
            Tools=side,Materials=side,SaveButton=save,MakeButton=make,TabY=compact?null:body.Y-leftLift,
            Drawer=new(frame.Right-288,frame.Y+8,280,frame.Height-16)};
    }
    private static int LeftLift(EditorLayout l)=>l.UsesDrawers?0:16;
    public static UiRect Category(EditorLayout l)=>new(l.Board.X,l.Body.Y-LeftLift(l),184,l.UsesDrawers?40:44);
    public static UiRect Size(EditorLayout l)=>new(Category(l).Right+8,l.Body.Y-LeftLift(l),l.Board.Right-Category(l).Right-8,l.UsesDrawers?40:44);
    public static UiRect CostBox(EditorLayout l)=>new(l.Board.X,l.Footer.Y,l.UsesDrawers?l.Footer.Width:l.Board.Width,l.UsesDrawers?48:72);
    public static UiRect SupplyButton(EditorLayout l)=>new(l.Header.X,l.Header.Y+4,144,40);
    public static UiRect[] ToolButtons(EditorLayout l)
    {
        int extra=l.Board.Width-384,part=extra/3,x=l.Board.X;
        int[] widths={84+part,84+part,40,40,104+extra-2*part};
        return widths.Select(w=>{var r=new UiRect(x,l.Body.Y+(l.UsesDrawers?48:52)-LeftLift(l),w,l.UsesDrawers?40:44);x+=w+8;return r;}).ToArray();
    }
    // Reserve the lower part of a permanent sidebar for one native inventory slot.
    // Compact drawers retain the existing minimum color size and may hide the slot.
    public static int PaletteCell(UiRect area,bool sword=false)=>Math.Clamp((area.Height-(sword?268:220))/5,24,50);
    public static (UiRect Picker,UiRect Dye) ColorActions(UiRect area,bool sword)
    {
        int y=area.Y+8+5*(PaletteCell(area,sword)+8);
        int width=Math.Min(232,area.Width-24),left=area.X+(area.Width-width)/2;
        return (new(left,y,44,44),new(left+52,y,width-52,44));
    }
    public static UiRect ProductPreview(UiRect area,bool sword)
    {
        int cell=PaletteCell(area,sword);
        int top=area.Y+8+5*(cell+8)+52+(sword?48:0)+8;
        int side=Math.Min(Math.Min(160,area.Width-32),area.Bottom-top-12);
        if(side<56)return default;
        return new UiRect(area.X+(area.Width-side)/2,top,side,side);
    }
}
