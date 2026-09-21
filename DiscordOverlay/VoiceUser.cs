using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace DiscordOverlay;

public class VoiceUser : INotifyPropertyChanged
{
    private string name = "";
    private Bitmap? avatar;
    private bool isSpeaking;
    private bool isMuted;
    private bool isDeafened;

    public VoiceUser(string id)
    {
        Id = id;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string? AvatarHash { get; set; }

    public bool AvatarRequested { get; set; }

    public string Name
    {
        get => name;
        set => SetField(ref name, value);
    }

    public Bitmap? Avatar
    {
        get => avatar;
        set => SetField(ref avatar, value);
    }

    public bool IsSpeaking
    {
        get => isSpeaking;
        set => SetField(ref isSpeaking, value);
    }

    public bool IsMuted
    {
        get => isMuted;
        set
        {
            if (SetField(ref isMuted, value))
            {
                OnPropertyChanged(nameof(ShowMuted));
            }
        }
    }

    public bool IsDeafened
    {
        get => isDeafened;
        set
        {
            if (SetField(ref isDeafened, value))
            {
                OnPropertyChanged(nameof(ShowMuted));
            }
        }
    }

    public bool ShowMuted => IsMuted && !IsDeafened;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
