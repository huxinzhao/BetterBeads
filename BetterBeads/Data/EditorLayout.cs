namespace BetterBeads.Data;

public readonly record struct UiRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;
    public bool Contains(UiRect other) => other.Width >= 0 && other.Height >= 0
        && other.X >= X && other.Y >= Y && other.Right <= Right && other.Bottom <= Bottom;
    public bool Overlaps(UiRect other) => Width > 0 && Height > 0 && other.Width > 0 && other.Height > 0
        && X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>All rectangles share UI viewport coordinates, including overlay hit targets.</summary>
public sealed record EditorLayout(UiRect Frame, UiRect Header, UiRect Body, UiRect Tools, UiRect Materials,
    UiRect CanvasToolbar, UiRect Canvas, UiRect Footer, UiRect SaveButton, UiRect MakeButton,
    UiRect Drawer, bool UsesDrawers, bool TooSmall)
{
    public int? TabY { get; init; }
    public UiRect Board => new(Canvas.X-8,Canvas.Y-8,Canvas.Width+16,Canvas.Height+16);
    public UiRect Sidebar=>Tools;
    public UiRect DrawerContent=>new(Drawer.X+8,Drawer.Y+56,Drawer.Width-16,Drawer.Height-64);
    public UiRect DrawerColors=>new(Drawer.X+8,Drawer.Y+12,88,40);
    public UiRect DrawerMaterials=>new(Drawer.X+104,Drawer.Y+12,88,40);
    public UiRect DrawerClose=>new(Drawer.Right-72,Drawer.Y+12,64,40);
    public UiRect ProductButton=>new(CanvasToolbar.X,CanvasToolbar.Y,UsesDrawers?96:Math.Min(136,CanvasToolbar.Width-256),UsesDrawers?40:44);
    public UiRect MoreButton=>new(EraseButton.Right+8,CanvasToolbar.Y,64,UsesDrawers?40:44);
    public UiRect PaintButton=>new(ProductButton.Right+8,CanvasToolbar.Y,UsesDrawers?40:CanvasToolbar.Width>=440?96:84,UsesDrawers?40:44);
    public UiRect EraseButton=>new(PaintButton.Right+8,CanvasToolbar.Y,PaintButton.Width,PaintButton.Height);
    public UiRect SidebarButton=>new(MoreButton.Right+8,CanvasToolbar.Y,128,40);
    public UiRect Status=>new(Footer.X,Footer.Y,Footer.Width,24);
    public UiRect HelpButton=>new(Footer.X,SaveButton.Y,UsesDrawers?88:104,SaveButton.Height);
    public UiRect EditorTab=>new(Header.Right-248,TabY??Header.Y+4,80,40);
    public UiRect LibraryTab=>new(Header.Right-160,TabY??Header.Y+4,112,40);
    public UiRect CloseButton=>new(Header.Right-40,TabY??Header.Y+4,40,40);
    public UiRect Title=>new(Header.X,Header.Y+4,Math.Max(1,EditorTab.X-Header.X-16),40);
    public UiRect LibraryArea=>new(Body.X,Body.Y,Body.Width,Footer.Bottom-Body.Y);
    public bool AcceptsCanvasInput(bool blocked,int x,int y)=>!TooSmall && !blocked && Canvas.Contains(x,y);
    public static EditorLayout Calculate(int viewportWidth,int viewportHeight)
    {
        if(viewportWidth<=0 || viewportHeight<=0)throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        bool compact=viewportWidth<900 || viewportHeight<640;
        int height=Math.Max(1,viewportHeight-32),bodyHeight=height-(compact?192:200);
        int sidebarWidth=viewportWidth>=1200 && viewportHeight>=800?320:256;
        int toolbarHeight=compact?40:44;
        int boardSize=Math.Max(16,bodyHeight-toolbarHeight-12);
        int width=compact?Math.Min(520,viewportWidth-32):Math.Min(viewportWidth-32,boardSize+sidebarWidth+72);
        var frame=new UiRect((viewportWidth-width)/2,(viewportHeight-height)/2,width,height);
        if(viewportWidth<480 || viewportHeight<420)
            return new(frame,frame,default,default,default,default,default,default,default,default,default,true,true);
        var header=new UiRect(frame.X+24,frame.Y+24,width-48,48);
        var body=new UiRect(header.X,header.Bottom+16,header.Width,bodyHeight);
        var footer=new UiRect(body.X,body.Bottom+16,body.Width,compact?64:72);
        int canvasWidth=compact?body.Width:body.Width-sidebarWidth-24;
        var toolbar=new UiRect(body.X,body.Y,canvasWidth,toolbarHeight);
        boardSize=Math.Min(boardSize,canvasWidth);
        var canvas=new UiRect(body.X+(canvasWidth-boardSize)/2+8,toolbar.Bottom+20,boardSize-16,boardSize-16);
        var drawer=new UiRect(frame.Right-24-280,header.Bottom-4,280,frame.Bottom-16-header.Bottom+4);
        int cell=Math.Clamp((bodyHeight-280)/4,32,50),paletteHeight=cell*4+120;
        var tools=compact?new UiRect(drawer.X+8,drawer.Y+56,drawer.Width-16,drawer.Height-64):new UiRect(body.Right-sidebarWidth,body.Y,sidebarWidth,paletteHeight);
        var materials=compact?tools:new UiRect(tools.X,tools.Bottom+16,sidebarWidth,body.Bottom-tools.Bottom-16);
        var make=new UiRect(footer.Right-112,footer.Y+24,112,compact?40:48);
        var save=new UiRect(make.X-128,make.Y,120,make.Height);
        return new(frame,header,body,tools,materials,toolbar,canvas,footer,save,make,drawer,compact,false);
    }
}
