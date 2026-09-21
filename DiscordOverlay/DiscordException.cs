using System;

namespace DiscordOverlay;

public class DiscordException : Exception
{
    public DiscordException(string message) : base(message)
    {
    }
}
