using System;
using System.Runtime.InteropServices;
using System.Xml;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
namespace DiscordOverlay;





public partial class MainWindow : Window
{
    
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        
        var discordClient = new DiscordClient();
        discordClient.Connect();
        discordClient.SendHandshake();
        discordClient.ReadMessage();

        var platformHandle = this.TryGetPlatformHandle();

        if (platformHandle !=null)
        {

            // getting window id
            IntPtr display = X11.XOpenDisplay(null);
            IntPtr nativeHandle=platformHandle.Handle;

        
            keyboardPassThrough(display, nativeHandle);
            mousePassThrough(display,nativeHandle);

            forceTopLayer(display, nativeHandle);

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += (sender, e) =>
                {
                    forceTopLayer(display, nativeHandle);
                    X11.XRaiseWindow(display, nativeHandle);
                };
                timer.Start();

            //Making the window always on top
            IntPtr wmWindowType = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
            IntPtr wmWindowTypeNotification = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE_DOCK", false);
            IntPtr wmState = X11.XInternAtom(display, "_NET_WM_STATE", false);
            IntPtr skipTaskbar = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_TASKBAR", false);
            IntPtr skipPager = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_PAGER", false);
            IntPtr XA_ATOM = new IntPtr(4);
            IntPtr stateAbove = X11.XInternAtom(display, "_NET_WM_STATE_ABOVE", false);
            IntPtr stateFullscreen = X11.XInternAtom(display, "_NET_WM_STATE_FULLSCREEN", false);


            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 0 , ref skipTaskbar, 1);
            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 1, ref skipPager, 1);
            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 2 , ref stateAbove, 1);
            X11.XChangeProperty(display, nativeHandle, wmWindowType, XA_ATOM, 32, 0, ref wmWindowTypeNotification, 1);
            X11.XChangeProperty(display, nativeHandle, wmState, XA_ATOM, 32, 2, ref stateFullscreen, 1);
            
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

    public void mousePassThrough(IntPtr display, IntPtr handle)
    {
        IntPtr emptyRegion = X11.XCreateRegion();
        X11.XShapeCombineRegion(display, handle, 2, 0, 0, emptyRegion, 0);
    }

    public void forceTopLayer(IntPtr display, IntPtr handle)
{
    IntPtr root = X11.XDefaultRootWindow(display);
    IntPtr wmState = X11.XInternAtom(display, "_NET_WM_STATE", false);
    IntPtr stateAbove = X11.XInternAtom(display, "_NET_WM_STATE_ABOVE", false);

    var ev = new XClientMessageEvent();
    ev.type = 33;
    ev.window = handle;
    ev.message_type = wmState;
    ev.format = 32;
    ev.ptr1 = new IntPtr(1);
    ev.ptr2 = stateAbove;

    X11.XSendEvent(display, root, false, 0x180000, ref ev);
}
    
    

     
    
    
}