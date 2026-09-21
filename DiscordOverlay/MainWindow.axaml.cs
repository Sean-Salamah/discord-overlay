using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace DiscordOverlay;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<VoiceUser> users = new();
    private readonly CancellationTokenSource cancellation = new();

    private DiscordClient? discordClient;
    private DispatcherTimer? topLayerTimer;
    private IntPtr display;
    private IntPtr windowHandle;
    private IntPtr wmStateAtom;
    private IntPtr stateAboveAtom;

    public MainWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        UsersList.ItemsSource = users;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        CoverScreen();
        SetUpX11Window();
        StartDiscord();
    }

    protected override void OnClosed(EventArgs e)
    {
        cancellation.Cancel();
        discordClient?.Dispose();
        topLayerTimer?.Stop();

        if (display != IntPtr.Zero)
        {
            X11.XCloseDisplay(display);
            display = IntPtr.Zero;
        }

        base.OnClosed(e);
    }

    public bool OverlayVisible
    {
        get => OverlayRoot.IsVisible;
        set => OverlayRoot.IsVisible = value;
    }

    private void CoverScreen()
    {
        Screen? screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen == null)
        {
            return;
        }

        Position = screen.Bounds.Position;
        Width = screen.Bounds.Width / screen.Scaling;
        Height = screen.Bounds.Height / screen.Scaling;
    }

    private void SetUpX11Window()
    {
        IPlatformHandle? platformHandle = TryGetPlatformHandle();
        if (platformHandle == null || platformHandle.HandleDescriptor != "XID")
        {
            Console.WriteLine("Not running on X11, click-through and stay-on-top will not work.");
            return;
        }

        display = X11.XOpenDisplay(null);
        if (display == IntPtr.Zero)
        {
            return;
        }

        windowHandle = platformHandle.Handle;
        wmStateAtom = X11.XInternAtom(display, "_NET_WM_STATE", false);
        stateAboveAtom = X11.XInternAtom(display, "_NET_WM_STATE_ABOVE", false);

        KeyboardPassThrough();
        MousePassThrough();
        SetWindowProperties();
        ForceTopLayer();

        topLayerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        topLayerTimer.Tick += (sender, args) =>
        {
            ForceTopLayer();
            X11.XRaiseWindow(display, windowHandle);
            X11.XFlush(display);
        };
        topLayerTimer.Start();
    }

    private void SetWindowProperties()
    {
        IntPtr xaAtom = new IntPtr(4);
        IntPtr wmWindowType = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
        IntPtr windowTypeDock = X11.XInternAtom(display, "_NET_WM_WINDOW_TYPE_DOCK", false);
        IntPtr skipTaskbar = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_TASKBAR", false);
        IntPtr skipPager = X11.XInternAtom(display, "_NET_WM_STATE_SKIP_PAGER", false);
        IntPtr stateAbove = stateAboveAtom;

        X11.XChangeProperty(display, windowHandle, wmStateAtom, xaAtom, 32, 0, ref skipTaskbar, 1);
        X11.XChangeProperty(display, windowHandle, wmStateAtom, xaAtom, 32, 2, ref skipPager, 1);
        X11.XChangeProperty(display, windowHandle, wmStateAtom, xaAtom, 32, 2, ref stateAbove, 1);
        X11.XChangeProperty(display, windowHandle, wmWindowType, xaAtom, 32, 0, ref windowTypeDock, 1);
        X11.XFlush(display);
    }

    // this makes keyboard inputs pass through the window
    private void KeyboardPassThrough()
    {
        var hints = new XWMHints();
        hints.flags = 1;
        hints.input = false;
        X11.XSetWMHints(display, windowHandle, ref hints);
        X11.XFlush(display);
    }

    private void MousePassThrough()
    {
        IntPtr emptyRegion = X11.XCreateRegion();
        X11.XShapeCombineRegion(display, windowHandle, 2, 0, 0, emptyRegion, 0);
        X11.XDestroyRegion(emptyRegion);
        X11.XFlush(display);
    }

    private void ForceTopLayer()
    {
        IntPtr root = X11.XDefaultRootWindow(display);

        var ev = new XClientMessageEvent();
        ev.type = 33;
        ev.window = windowHandle;
        ev.message_type = wmStateAtom;
        ev.format = 32;
        ev.ptr1 = new IntPtr(1);
        ev.ptr2 = stateAboveAtom;
        ev.ptr4 = new IntPtr(1);

        X11.XSendEvent(display, root, false, 0x180000, ref ev);
        X11.XFlush(display);
    }

    private void StartDiscord()
    {
        Config config;
        try
        {
            config = Config.Load();
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
            return;
        }

        discordClient = new DiscordClient(config);
        discordClient.StatusChanged += status => Dispatcher.UIThread.Post(() => ShowStatus(status));
        discordClient.ChannelJoined += members => Dispatcher.UIThread.Post(() => ShowChannel(members));
        discordClient.ChannelLeft += () => Dispatcher.UIThread.Post(users.Clear);
        discordClient.MemberUpdated += member => Dispatcher.UIThread.Post(() => UpdateMember(member));
        discordClient.MemberLeft += userId => Dispatcher.UIThread.Post(() => RemoveMember(userId));
        discordClient.SpeakingChanged += (userId, speaking) => Dispatcher.UIThread.Post(() => SetSpeaking(userId, speaking));

        DiscordClient client = discordClient;
        CancellationToken token = cancellation.Token;
        Task.Run(() => client.RunAsync(token));
    }

    private void ShowStatus(string? status)
    {
        StatusText.Text = status;
        StatusBorder.IsVisible = !string.IsNullOrEmpty(status);
    }

    private void ShowChannel(IReadOnlyList<VoiceMember> members)
    {
        foreach (VoiceUser user in users.ToList())
        {
            if (members.All(member => member.Id != user.Id))
            {
                users.Remove(user);
            }
        }

        foreach (VoiceMember member in members)
        {
            UpdateMember(member);
        }
    }

    private void UpdateMember(VoiceMember member)
    {
        VoiceUser? user = FindUser(member.Id);
        if (user == null)
        {
            user = new VoiceUser(member.Id);
            users.Add(user);
        }

        user.Name = member.Name;
        user.IsMuted = member.Muted;
        user.IsDeafened = member.Deafened;

        if (!user.AvatarRequested || user.AvatarHash != member.AvatarHash)
        {
            LoadAvatar(user, member.AvatarHash);
        }
    }

    private static async void LoadAvatar(VoiceUser user, string? avatarHash)
    {
        user.AvatarRequested = true;
        user.AvatarHash = avatarHash;

        Bitmap? avatar = await AvatarLoader.LoadAsync(user.Id, avatarHash);

        if (user.AvatarHash == avatarHash)
        {
            user.Avatar = avatar;
        }
    }

    private void RemoveMember(string userId)
    {
        VoiceUser? user = FindUser(userId);
        if (user != null)
        {
            users.Remove(user);
        }
    }

    private void SetSpeaking(string userId, bool speaking)
    {
        VoiceUser? user = FindUser(userId);
        if (user != null)
        {
            user.IsSpeaking = speaking;
        }
    }

    private VoiceUser? FindUser(string userId)
    {
        return users.FirstOrDefault(user => user.Id == userId);
    }
}
