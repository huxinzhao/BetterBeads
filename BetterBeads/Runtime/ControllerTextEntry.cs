#if BEADS_LITE
using BetterBeads.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace BetterBeads.Runtime;

internal sealed class ControllerTextEntry
{
    private readonly TextBox target;
    private readonly string original;
    private ControllerKeyboardLayout layout;
    private UiRect frame;
    private int page;
    internal int Page=>page;
    internal bool Active {get;private set;}=true;
    internal IReadOnlyList<UiRect> Targets=>layout.Targets(page);
    internal ControllerTextEntry(TextBox target,UiRect frame)
    {this.target=target;this.frame=frame;layout=ControllerKeyboardLayout.Calculate(frame);original=target.Text;target.Selected=false;
        if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,target))Game1.keyboardDispatcher.Subscriber=null;}
    internal void Relayout(UiRect next){frame=next;layout=ControllerKeyboardLayout.Calculate(next);}
    internal void Select(UiRect rect)
    {
        int tab=Array.IndexOf(layout.Pages,rect);
        if(tab>=0){page=tab;return;}
        int key=Array.IndexOf(layout.Keys,rect);
        string alphabet=page==0?ControllerKeyboardLayout.FirstPage:ControllerKeyboardLayout.SecondPage;
        if(key>=0&&key<alphabet.Length){target.Text+=alphabet[key];return;}
        int action=Array.IndexOf(layout.Actions,rect);
        if(action==0)target.Text+=" ";
        else if(action==1&&target.Text.Length>0)target.Text=target.Text[..^1];
        else if(action==2)Close(false);
        else if(action==3)Close(true);
    }
    internal void Close(bool cancel)
    {
        if(cancel)target.Text=original;
        target.Selected=false;
        if(ReferenceEquals(Game1.keyboardDispatcher.Subscriber,target))Game1.keyboardDispatcher.Subscriber=null;
        Active=false;
    }
    internal void Draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect,ArtResources.Rect(frame),Color.Black*.7f);
        var box=layout.Box;
        ArtResources.Panel(b,box);
        ArtResources.TextLine(b,ContentText.Get("input.keyboard","输入文字"),new(box.X+16,box.Y+12,box.Width-32,28),ArtResources.Ink);
        ArtResources.TextLine(b,target.Text,new(box.X+16,box.Y+40,box.Width-32,32),ArtResources.Ink);
        ArtResources.ButtonText(b,layout.Pages[0],"A–X",selected:page==0);
        ArtResources.ButtonText(b,layout.Pages[1],"Y–Z / 0–9",selected:page==1);
        string alphabet=page==0?ControllerKeyboardLayout.FirstPage:ControllerKeyboardLayout.SecondPage;
        for(int i=0;i<alphabet.Length;i++)ArtResources.ButtonText(b,layout.Keys[i],alphabet[i].ToString());
        string[] actions={ContentText.Get("input.space","空格"),"⌫",ContentText.Get("input.done","完成"),ContentText.Get("input.cancel","取消")};
        for(int i=0;i<actions.Length;i++)ArtResources.ButtonText(b,layout.Actions[i],actions[i]);
    }
}
#endif
