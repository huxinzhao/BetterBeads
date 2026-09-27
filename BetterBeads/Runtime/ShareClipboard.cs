#if BEADS_LITE
using System.Runtime.InteropServices;

namespace BetterBeads.Runtime;

internal static class ShareClipboard
{
    [DllImport("SDL2",EntryPoint="SDL_SetClipboardText",CallingConvention=CallingConvention.Cdecl)]
    private static extern int SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
    [DllImport("SDL2",EntryPoint="SDL_GetClipboardText",CallingConvention=CallingConvention.Cdecl)]
    private static extern IntPtr GetClipboardText();
    [DllImport("SDL2",EntryPoint="SDL_free",CallingConvention=CallingConvention.Cdecl)]
    private static extern void Free(IntPtr value);
    internal static bool TryCopy(string code)
    {
        try{return SetClipboardText(code)==0;}
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return false;}
    }
    internal static string? TryPaste()
    {
        try
        {
            IntPtr pointer=GetClipboardText();if(pointer==IntPtr.Zero)return null;
            try{return Marshal.PtrToStringUTF8(pointer);}finally{Free(pointer);}
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return null;}
    }
}
#endif
