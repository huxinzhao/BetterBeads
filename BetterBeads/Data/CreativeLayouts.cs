namespace BetterBeads.Data;

internal sealed record PaletteSearchLayout(UiRect Dialog,UiRect Search,UiRect Close,UiRect[] Results,UiRect Previous,UiRect Next,UiRect PageInfo,UiRect Status)
{
    internal static PaletteSearchLayout Calculate(UiRect frame)
    {
        var parent=CreativeDialogLayout.Calculate(frame).Dialog;
        int inset=16;
        var search=new UiRect(parent.X+inset,parent.Y+64,parent.Width-inset*2,44);
        int columns=parent.Width<600?3:5;
        int rows=parent.Height<440?3:4;
        int gap=8;
        int cellWidth=(parent.Width-inset*2-gap*(columns-1))/columns;
        int top=search.Bottom+16;
        int bottom=parent.Bottom-88;
        int cellHeight=Math.Max(48,Math.Min(64,(bottom-top-gap*(rows-1))/rows));
        var cells=Enumerable.Range(0,columns*rows).Select(i=>new UiRect(parent.X+inset+i%columns*(cellWidth+gap),top+i/columns*(cellHeight+gap),cellWidth,cellHeight)).ToArray();
        int footer=parent.Bottom-56;
        return new(parent,search,new(parent.Right-56,parent.Y+16,40,40),cells,
            new(parent.X+inset,footer,48,40),new(parent.Right-inset-48,footer,48,40),
            new(parent.X+inset+56,footer,parent.Width-inset*2-112,40),
            new(parent.X+inset,footer-32,parent.Width-inset*2,24));
    }
}

internal sealed record CreativeDialogLayout(UiRect Dialog,UiRect CommonTab,UiRect WorkTab,UiRect Close,UiRect Body,UiRect Left,UiRect Right,
    UiRect Back,UiRect Next,UiRect Apply,UiRect[] Cells,ColorPickerLayout Picker,UiRect BeforeCard,UiRect AfterCard,
    UiRect Preview,UiRect Info,UiRect PreviousPage,UiRect NextPage,UiRect PageInfo,bool Compact)
{
    internal static CreativeDialogLayout Calculate(UiRect frame)
    {
        int width=Math.Min(800,frame.Width-16);bool compact=width<680;
        int height=Math.Min(compact?432:456,frame.Height-16);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        var body=new UiRect(box.X+16,box.Y+64,width-32,height-(compact?176:128));
        int gridWidth=compact?176:184,gap=compact?4:8,cell=compact?28:40;
        var left=new UiRect(body.X,body.Y,gridWidth,body.Height);
        int pickerX=left.Right+16,pickerWidth=compact?body.Right-pickerX:Math.Min(296,body.Width-gridWidth-16-188);
        int size=Math.Min(pickerWidth-32,body.Height-28);
        var sv=new UiRect(pickerX,body.Y,size,size);var hue=new UiRect(sv.Right+8,sv.Y,24,size);
        var right=compact?new UiRect(pickerX,body.Y,pickerWidth,body.Height):new UiRect(hue.Right+16,body.Y,body.Right-hue.Right-16,body.Height);
        int gx=left.X+(left.Width-cell*4-gap*3)/2;
        var cells=Enumerable.Range(0,20).Select(i=>new UiRect(gx+i%4*(cell+gap),left.Y+i/4*(cell+gap),cell,cell)).ToArray();
        int arrow=compact?32:40;var prev=new UiRect(left.X,left.Bottom-arrow,arrow,arrow);var next=new UiRect(left.Right-arrow,prev.Y,arrow,arrow);
        var page=new UiRect(prev.Right+4,prev.Y,next.X-prev.Right-8,arrow);
        var info=new UiRect(sv.X,sv.Bottom+8,hue.Right-sv.X,20);
        int half=(body.Width-8)/2;
        var before=compact?new UiRect(body.X,body.Bottom+8,half,40):new UiRect(right.X,right.Y,right.Width,44);
        var after=compact?new UiRect(before.Right+8,before.Y,half,40):new UiRect(right.X,before.Bottom+8,right.Width,44);
        var preview=compact?new UiRect(after.Right-40,after.Y,40,40):new UiRect(right.X,after.Bottom+8,right.Width,right.Bottom-after.Bottom-8);
        int bw=(body.Width-16)/3,by=box.Bottom-56;
        var back=new UiRect(body.X,by,bw,40);var reset=new UiRect(back.Right+8,by,bw,40);var apply=new UiRect(reset.Right+8,by,body.Right-reset.Right-8,40);
        var close=new UiRect(box.Right-56,box.Y+16,40,40);
        return new(box,new(box.X+16,box.Y+16,128,40),new(box.X+152,box.Y+16,128,40),close,body,left,right,back,reset,apply,cells,
            new(box,sv,hue,right,left,back,reset,apply,close),before,after,preview,info,prev,next,page,compact);
    }
}

internal sealed record ScenePreviewLayout(UiRect Dialog,UiRect Close,UiRect Stage,UiRect Info,UiRect Note,UiRect Toggle)
{
    internal static ScenePreviewLayout Calculate(UiRect frame)
    {
        int width=Math.Min(760,frame.Width-16),height=Math.Min(600,frame.Height-16);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        return new(box,new(box.Right-56,box.Y+16,40,40),new(box.X+16,box.Y+64,width-32,height-200),
            new(box.X+16,box.Bottom-128,width-32,24),new(box.X+16,box.Bottom-96,width-32,24),new(box.X+16,box.Bottom-60,width-32,44));
    }
}

internal sealed record LiteLibraryLayout(UiRect Search,UiRect Filter,UiRect Import,UiRect Rows,int Count,UiRect Previous,UiRect Next,UiRect Status,UiRect[] Actions)
{
    internal static LiteLibraryLayout Calculate(UiRect area)
    {
        // Keep search readable at the minimum viewport; filtering and importing share their own row.
        var search=new UiRect(area.X,area.Y,area.Width,40);
        int half=(area.Width-8)/2;
        int count=Math.Max(1,(area.Height-192)/64);
        return new(search,new(area.X,area.Y+48,half,40),new(area.X+half+8,area.Y+48,half,40),
            new(area.X,area.Y+96,area.Width,count*64),count,
            new(area.X,area.Bottom-96,48,40),new(area.Right-48,area.Bottom-96,48,40),new(area.X+56,area.Bottom-96,area.Width-112,40),
            Enumerable.Range(0,4).Select(i=>new UiRect(area.X+i*((area.Width-24)/4+8),area.Bottom-48,(area.Width-24)/4,44)).ToArray());
    }
    internal UiRect Row(int index)=>new(Rows.X,Rows.Y+index*64,Rows.Width,56);
    internal UiRect Star(int index)=>new(Rows.Right-48,Rows.Y+index*64+8,40,40);
    internal UiRect Preview(int index)=>new(Rows.X+4,Rows.Y+index*64+6,44,44);
}

internal sealed record LibraryShareLayout(UiRect Dialog,UiRect Close,UiRect Copy,UiRect Chart,UiRect Paste,UiRect ImportFile,UiRect Note)
{
    internal static LibraryShareLayout Calculate(UiRect frame)
    {
        int width=Math.Min(520,frame.Width-24),height=Math.Min(320,frame.Height-24);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        int buttonHeight=Math.Max(40,Math.Min(44,(height-108)/3-8));
        int first=box.Y+64;
        return new(box,new(box.Right-56,box.Y+12,40,40),
            new(box.X+16,first,width-32,buttonHeight),
            new(box.X+16,first+buttonHeight+8,width-32,buttonHeight),
            new(box.X+16,first+(buttonHeight+8)*2,width-32,buttonHeight),
            new(box.X+16,first+(buttonHeight+8)*3,width-32,buttonHeight),
            new(box.X+16,box.Bottom-34,width-32,24));
    }
}

internal sealed record LibraryShareFilesLayout(UiRect Dialog,UiRect Title,UiRect[] Rows,UiRect Previous,UiRect Next,UiRect Back,UiRect Note)
{
    internal static LibraryShareFilesLayout Calculate(UiRect frame)
    {
        int width=Math.Min(520,frame.Width-24),height=Math.Min(376,frame.Height-24);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        int count=Math.Clamp((height-160)/48,1,4);
        var rows=Enumerable.Range(0,count).Select(i=>new UiRect(box.X+16,box.Y+64+i*48,width-32,42)).ToArray();
        return new(box,new(box.X+16,box.Y+12,width-32,40),rows,
            new(box.X+16,box.Bottom-56,72,40),new(box.X+96,box.Bottom-56,72,40),
            new(box.Right-128,box.Bottom-56,112,40),new(box.X+16,box.Bottom-96,width-32,24));
    }
}

internal sealed record LibraryFilterLayout(UiRect Dialog,UiRect[] Categories,UiRect Favorites,UiRect Sort,UiRect Done,UiRect Previous,UiRect Next)
{
    internal static LibraryFilterLayout Calculate(UiRect frame)
    {
        int width=Math.Min(580,frame.Width-16),height=Math.Min(408,frame.Height-16);
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        bool paged=height<392;int columns=paged?2:3,count=paged?5:10,cell=(width-32-(columns-1)*8)/columns;
        return new(box,Enumerable.Range(0,count).Select(i=>new UiRect(box.X+16+i%columns*(cell+8),box.Y+56+i/columns*44,cell,40)).ToArray(),
            new(box.X+16,box.Bottom-152,width-32,40),new(box.X+16,box.Bottom-104,width-32,40),new(box.X+16,box.Bottom-56,width-32,40),
            new(box.Right-112,box.Y+8,40,40),new(box.Right-64,box.Y+8,40,40));
    }
}

internal sealed record LibraryOpenLayout(UiRect Dialog,UiRect Edit,UiRect Copy,UiRect Cancel)
{
    internal static LibraryOpenLayout Calculate(UiRect frame)
    {
        int width=Math.Min(440,frame.Width-32),height=248;
        var box=new UiRect(frame.X+(frame.Width-width)/2,frame.Y+(frame.Height-height)/2,width,height);
        return new(box,new(box.X+16,box.Y+88,width-32,40),new(box.X+16,box.Y+136,width-32,40),new(box.X+16,box.Bottom-56,width-32,40));
    }
}
