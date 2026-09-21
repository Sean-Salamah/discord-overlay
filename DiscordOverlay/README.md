# discord-overlay

This is supposed to be a discord overlay that will work on linux like it does on windows, allowing users to see other users in the call and have it be shown when a user is talking.

Discord's own overlay only works on Windows so I made one for Linux. It's written in C# with .NET 10 and Avalonia. It connects to the Discord desktop app through its local RPC socket and uses X11 to keep the window transparent, on top and click-through.

It shows everyone in your current voice channel with their avatar and name, puts a green ring around whoever is talking and shows a red icon if someone is muted or deafened.

**It doesn't work over fullscreen applications yet.** If a game or app is fullscreen it covers the overlay. It works fine on top of normal windows, so run games in windowed mode if you want to see it. Getting it to show over fullscreen is the main thing I still want to fix.

## What you need

- Linux on an X11 session. Mint is X11 by default. If you're on KDE or GNOME pick the X11 session at the login screen, it doesn't work properly on Wayland.
- The Discord desktop app (not the browser version)
- Your own Discord application. Discord only lets the app owner (or people added as testers) use RPC, so you can't use mine.

## Setting up the Discord app

Go to https://discord.com/developers/applications, make a new application and open the OAuth2 page. Copy the Client ID, then hit Reset Secret and copy the Client Secret. Don't share the secret with anyone.

Copy `config.example.json` to `config.json` and put both in:

```json
{
    "client_id": "YOUR_CLIENT_ID",
    "client_secret": "YOUR_CLIENT_SECRET"
}
```

## Installing

### From a release

If there's a build on the releases page you don't need .NET. Download `DiscordOverlay` and `config.example.json` into the same folder, make your `config.json` next to it and run:

```
chmod +x DiscordOverlay
./DiscordOverlay
```

### From source

You need git and the .NET 10 SDK. On Mint/Ubuntu:

```
sudo apt install git dotnet-sdk-10.0
```

Then:

```
git clone https://github.com/Sean-Salamah/discord-overlay.git
cd discord-overlay
```

Put your `config.json` in the `DiscordOverlay` folder and run:

```
./install.sh
```

That builds it into a single executable, installs it to `~/.local/share/discord-overlay` and adds it to your app menu. You can also start it with `discord-overlay` from a terminal. If that says command not found, `~/.local/bin` isn't in your PATH:

```
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc
```

To update, `git pull`, delete the `dist` folder and run `./install.sh` again. `./uninstall.sh` removes it.

## Using it

Have Discord open, then start the overlay. The first time, Discord will pop up asking you to authorize the app. After that your login is saved in `~/.config/discord-overlay` so it won't ask again (delete that folder if you want to log in with a different account).

Join a voice channel and everyone shows up in the top left corner.

You can't click on the overlay, clicks go straight through to whatever is behind it. Use the tray icon to hide it or quit.

To have it start when you log in:

```
cp ~/.local/share/applications/discord-overlay.desktop ~/.config/autostart/
```

## Problems

If the overlay says "Waiting for Discord..." then Discord isn't open or isn't logged in yet. It'll connect by itself once it is.

If it says "Discord rejected the token request" your client secret is probably wrong, reset it and paste the new one into `config.json`.

If you can't click anything behind the overlay you're most likely on Wayland. Alt+Tab to the terminal and Ctrl+C, or run `pkill -f DiscordOverlay`, then log in with X11 instead.

If the overlay disappears when you open a game, the game is probably fullscreen. It doesn't show over fullscreen apps yet, switch the game to windowed mode.

## Development

```
cd DiscordOverlay
dotnet run
```

`./publish.sh` builds the standalone executable to `dist/` without installing it. That's what goes on the releases page.

The Discord side is in `DiscordClient.cs` and `DiscordAuth.cs`, the X11 stuff is in `MainWindow.axaml.cs` and `X11/X11Interop.cs`, and the layout is `MainWindow.axaml`.
