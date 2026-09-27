namespace BetterBeads.Data;

public sealed record ColorPickerLayout(UiRect Dialog,UiRect SaturationValue,UiRect Hue,UiRect Swatch,UiRect Slots,
    UiRect Previous,UiRect Next,UiRect Apply,UiRect Close,bool Compact=false)
{
    public UiRect ColorTab=>new(Dialog.Right-248,Dialog.Y+16,88,40);
    public UiRect DyeTab=>new(Dialog.Right-152,Dialog.Y+16,80,40);
    public UiRect Info=>new(Swatch.X,Swatch.Bottom-24,Swatch.Width,24);
    public UiRect SlotInfo=>new(Swatch.X,Swatch.Bottom-48,Swatch.Width,24);
    public int PreviewBeadSize=>Math.Min(Swatch.Height-(Compact?56:32),(Swatch.Width-32)/2);
    public UiRect ReferenceCaption=>new(Swatch.X,Swatch.Y,Swatch.Width,22);
    public UiRect BeforeCard=>Compact?new(Swatch.X,Swatch.Y+26,Swatch.Width,78):new(Swatch.X,Swatch.Y+24,(Swatch.Width-8)/2,96);
    public UiRect AfterCard=>Compact?new(Swatch.X,Swatch.Y+110,Swatch.Width,78):new(BeforeCard.Right+8,Swatch.Y+24,(Swatch.Width-8)/2,96);
    public UiRect CardTitle(UiRect card)=>new(card.X+2,card.Y+2,card.Width-4,20);
    public UiRect CardBead(UiRect card)
    {
        int size=Compact?30:42;
        return new(card.X+(card.Width-size)/2,card.Y+24,size,size);
    }
    public UiRect CardCode(UiRect card)=>new(card.X+2,card.Bottom-24,card.Width-4,22);
    public UiRect Hint=>new(Dialog.X+24,SaturationValue.Bottom+16,Dialog.Width-48,24);
    public bool CanPick(bool dyePage,int x,int y)=> (!Compact || dyePage) && (SaturationValue.Contains(x,y)||Hue.Contains(x,y));
    public static ColorPickerLayout Calculate(UiRect frame)
    {
        bool compact=frame.Width<684 || frame.Height<572;
        int width=compact?Math.Min(520,frame.Width-16):660,height=compact?Math.Min(548,frame.Height-16):548;
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        int pad=24,top=box.Y+(compact?72:88),size=compact?Math.Min(height-184,(width-84)*2/3):320;
        var sv=new UiRect(box.X+pad,top,size,size);
        var hue=new UiRect(sv.Right+12,top,24,size);
        int rightX=hue.Right+(compact?16:24),rightWidth=box.Right-pad-rightX;
#if BEADS_LITE
        var swatch=new UiRect(rightX,top,rightWidth,compact?200:120);
        var slots=compact?new UiRect(sv.X,top,size,size):new UiRect(rightX,top+136,rightWidth,184);
#else
        var swatch=new UiRect(rightX,top,rightWidth,compact?104:72);
        var slots=compact?new UiRect(sv.X,top,size,size):new UiRect(rightX,top+88,232,232);
#endif
        if(!compact)return new(box,sv,hue,swatch,slots,
            new(box.X+24,box.Bottom-72,136,48),new(box.X+168,box.Bottom-72,168,48),
            new(box.Right-156,box.Bottom-72,132,48),new(box.Right-64,box.Y+16,40,40),false);
        int half=Math.Min(168,(width-pad*2-8)/2),left=box.X+(width-half*2-8)/2;
        return new(box,sv,hue,swatch,slots,new(left,box.Bottom-104,half,40),new(left+half+8,box.Bottom-104,half,40),
            new(left,box.Bottom-56,half*2+8,40),new(box.Right-64,box.Y+16,40,40),true);
    }
    public static IReadOnlyList<UiRect> FavoriteCells(UiRect area,bool caption=true)
    {
        int top=caption?area.Y+32:area.Y;
        int bottom=caption?FavoriteAction(area).Y-8:area.Bottom;
        int rows=BeadPalette.Defaults.Length/4;
        int cell=Math.Min(50,Math.Min((area.Width-24)/4,(bottom-top-(rows-1)*8)/rows));
        cell=Math.Max(4,cell);
        int gridWidth=cell*4+24,gridHeight=cell*rows+(rows-1)*8,left=area.X+(area.Width-gridWidth)/2;
        if(!caption)top=area.Y+(area.Height-gridHeight)/2;
        return Enumerable.Range(0,BeadPalette.Defaults.Length).Select(i=>new UiRect(left+i%4*(cell+8),top+i/4*(cell+8),cell,cell)).ToArray();
    }
    public static UiRect FavoriteCaption(UiRect area)=>new(area.Right-112,area.Y,104,24);
    public static UiRect FavoriteAction(UiRect area)
    {
        int height=area.Height<244?40:44;
        return new(area.X+(area.Width-136)/2,area.Bottom-height-8,136,height);
    }
}
