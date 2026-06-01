using System;
using System.Runtime.InteropServices;
namespace DiscordOverlay;

internal static class X11
{
    [DllImport("libX11")]
    public static extern int XSetWMHints(IntPtr display, IntPtr window, ref XWMHints hints);
    

    [DllImport ("libX11")]
    public static extern IntPtr XOpenDisplay(string? display);

    [DllImport("libX11")]
public static extern IntPtr XInternAtom(IntPtr display, string name, bool only_if_exists);

[DllImport("libX11")]
public static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, 
    IntPtr type, int format, int mode, ref IntPtr data, int nelements);

    [DllImport("libXext")]
public static extern IntPtr XShapeCombineRegion(IntPtr display, IntPtr window, int destKind, int xOff, int yOff, IntPtr region, int op);

[DllImport("libX11")]
public static extern IntPtr XCreateRegion();

[DllImport("libX11")]
public static extern IntPtr XDefaultRootWindow(IntPtr display);

[DllImport("libX11")]
public static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, 
    long event_mask, ref XClientMessageEvent event_send);

    [DllImport("libX11")]
public static extern int XRaiseWindow(IntPtr display, IntPtr window);


}



[StructLayout(LayoutKind.Sequential)]
public struct XWMHints
{
       public long flags;
    public bool input;
    public int initial_state;
    public IntPtr icon_pixmap;
    public IntPtr icon_window;
    public int icon_x;
    public int icon_y;
    public IntPtr icon_mask;
    public IntPtr window_group;
}

[StructLayout(LayoutKind.Sequential)]
public struct XClientMessageEvent
{
    public int type;
    public ulong serial;
    public bool send_event;
    public IntPtr display;
    public IntPtr window;
    public IntPtr message_type;
    public int format;
    public IntPtr ptr1;
    public IntPtr ptr2;
    public IntPtr ptr3;
    public IntPtr ptr4;
    public IntPtr ptr5;
}

