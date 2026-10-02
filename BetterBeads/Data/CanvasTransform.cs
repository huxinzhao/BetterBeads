namespace BetterBeads.Data;

public sealed class CanvasTransform
{
    public UiRect Viewport { get; private set; }
    public int Zoom { get; private set; } = 1;
    public int OffsetX { get; private set; }
    public int OffsetY { get; private set; }
    public void Layout(UiRect viewport) => Viewport = viewport;
    public void Relayout(UiRect viewport,int columns,int rows)
    {
        if(Viewport.Width<=0||Viewport.Height<=0){Layout(viewport);Center(columns,rows);return;}
        double cx=(Viewport.Width/2d-OffsetX)/Zoom,cy=(Viewport.Height/2d-OffsetY)/Zoom;
        double previousFit=Math.Max(1,Math.Min(Viewport.Width/columns,Viewport.Height/rows));
        double nextFit=Math.Max(1,Math.Min(viewport.Width/columns,viewport.Height/rows));
        Zoom=Math.Clamp((int)Math.Round(Zoom*nextFit/previousFit),1,128);
        Layout(viewport);
        OffsetX=(int)Math.Round(viewport.Width/2d-cx*Zoom);
        OffsetY=(int)Math.Round(viewport.Height/2d-cy*Zoom);
    }
    public void Center(int columns, int rows)
    {
        if (columns <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        Zoom = Math.Clamp(Math.Min(Viewport.Width / columns, Viewport.Height / rows), 1, 128);
        OffsetX = (Viewport.Width - columns * Zoom) / 2;
        OffsetY = (Viewport.Height - rows * Zoom) / 2;
    }
    public bool TryCell(int screenX, int screenY, int columns, int rows, out int x, out int y)
    {
        x = y = -1;
        if (!Viewport.Contains(screenX, screenY)) return false;
        int localX = screenX - Viewport.X - OffsetX, localY = screenY - Viewport.Y - OffsetY;
        if (localX < 0 || localY < 0) return false;
        x = localX / Zoom; y = localY / Zoom;
        return x < columns && y < rows;
    }
    public void Pan(int dx, int dy)
    {
        OffsetX = (int)Math.Clamp((long)OffsetX + dx, -100000, 100000);
        OffsetY = (int)Math.Clamp((long)OffsetY + dy, -100000, 100000);
    }
    public void ZoomAt(int screenX, int screenY, int delta)
        =>ZoomStepsAt(screenX,screenY,Math.Sign(delta));
    public void ZoomStepsAt(int screenX,int screenY,int steps)
    {
        if (!Viewport.Contains(screenX, screenY)) return;
        int next = Math.Clamp(Zoom + steps, 1, 128);
        double localX = (screenX - Viewport.X - OffsetX) / (double)Zoom;
        double localY = (screenY - Viewport.Y - OffsetY) / (double)Zoom;
        OffsetX = (int)Math.Round(screenX - Viewport.X - localX * next);
        OffsetY = (int)Math.Round(screenY - Viewport.Y - localY * next);
        Zoom = next;
    }
    public UiRect CellRect(int x, int y) => new(Viewport.X + OffsetX + x * Zoom, Viewport.Y + OffsetY + y * Zoom, Zoom, Zoom);
}
