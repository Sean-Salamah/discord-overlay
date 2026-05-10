using System;
using System.Runtime.InteropServices;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;

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


public partial class MainWindow : Window
{
    
    public MainWindow()
    {
        
        
        
        InitializeComponent();

    
        

        

        

        
 
    
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        var platformHandle = this.TryGetPlatformHandle();
        
        if (platformHandle !=null)
        {

            // getting window id for X11
            IntPtr display = X11.XOpenDisplay(null);
            IntPtr nativeHandle=platformHandle.Handle;
            keyboardPassThrough(display, nativeHandle);

            //Making the window always on top
            IntPtr wmWindowType = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
            IntPtr wmWindowTypeNotification = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE_NOTIFICATION", false);
            IntPtr wmState = X11.XInternAtom(display, "_NET_WM_STATE", false);
            IntPtr skipTaskbar = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_TASKBAR", false);
            IntPtr skipPager = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_PAGER", false);
            IntPtr XA_ATOM = new IntPtr(4);

            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 0 , ref skipTaskbar, 1);
            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 1, ref skipPager, 1);

            X11.XChangeProperty(display, nativeHandle, wmWindowType, XA_ATOM, 32, 0, ref wmWindowTypeNotification, 1);
           
            
        }
    }

    public void keyboardPassThrough(IntPtr display, IntPtr handle)
    {
               // this makes our keyboard inputs pass through the window
        var hints = new XWMHints();
        hints.flags = 2;
        hints.input = false;
        X11.XSetWMHints(display,  handle, ref hints);
        
    }
    

     
    
    
}