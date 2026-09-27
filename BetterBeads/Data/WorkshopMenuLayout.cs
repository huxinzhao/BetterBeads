namespace BetterBeads.Data;
public sealed record WorkshopMenuLayout(UiRect Frame,UiRect Title,UiRect Description,UiRect Rows,UiRect Previous,UiRect Next,UiRect Back,int Count)
{
    public static WorkshopMenuLayout Calculate(int width,int height,int descriptionLines,int choiceCount=8)
    {
        int descriptionHeight=Math.Clamp(descriptionLines,1,5)*24;
        int desiredHeight=descriptionHeight+152+Math.Clamp(choiceCount,1,8)*52;
        int w=Math.Min(720,width-32),h=Math.Min(desiredHeight,height-32),x=(width-w)/2,y=(height-h)/2;
        int count=Math.Min(Math.Max(1,choiceCount),Math.Max(1,(h-descriptionHeight-152)/52));
        return new(new(x,y,w,h),new(x+24,y+20,w-48,32),new(x+24,y+60,w-48,descriptionHeight),
            new(x+24,y+72+descriptionHeight,w-48,count*52),new(x+24,y+h-64,80,40),
            new(x+112,y+h-64,80,40),new(x+w-136,y+h-64,112,40),count);
    }
}
