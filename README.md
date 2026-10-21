# Xonotic LAN Kit

One-click setup for a [Xonotic](https://xonotic.org) LAN party on Windows laptops. Xonotic is a free, open-source arena shooter, and this kit needs no accounts, no internet during play and no admin rights.

- **Install Game.exe** installs the game for a player.
- **Install and Start Server.exe** installs the game on one laptop and runs the server there.

## Get the kit ready (once)

1. Download **Install Game.exe** and **Install and Start Server.exe** from this repo's [Releases](../../releases) page.
2. Download **xonotic-0.8.6.zip** (about 1.2 GB) from [xonotic.org/download](https://xonotic.org/download).
3. Put all three files in one folder, then copy that folder to a USB stick or a shared drive.

```
Xonotic LAN Kit\
  Install Game.exe
  Install and Start Server.exe
  xonotic-0.8.6.zip
```

## Players

1. Double-click **Install Game.exe**. It takes under a minute.
2. Double-click **Play Xonotic** on your desktop.
3. Go to **Multiplayer → Join** and pick **Work LAN**.

Not in the list? Press the `` ` `` key (above Tab) and type `connect <server IP>`. The server laptop shows its IP when the server starts.

## Server laptop (just one)

1. Double-click **Install and Start Server.exe**. The first time, it installs the game and then starts the server. After that, run it again (or use the **Start Xonotic Server** desktop shortcut) whenever you want to start the server.
2. Keep the server window open while people play. Close it to stop the server.
3. If Windows Firewall asks about `xonotic-dedicated`, click **Allow**.

You can play on the server laptop too: use **Play Xonotic** on its desktop.

## What the setup does

- Checks that the zip is the official, undamaged release (SHA-512), then installs the game to `C:\Games\Xonotic`.
- Makes the server list show only games on your own network.
- Runs the game fullscreen at the screen's real resolution. On scaled screens (125%, 150% and up) it sets Windows' "Override high DPI scaling → Application" option for Xonotic, for the current user only.
- Adds desktop shortcuts.
- Server only: writes `C:\Games\Xonotic\data\server.cfg` and `start-server.bat`, starts the server, and checks that it answers.

Running either setup again is safe: it skips installing if the game is already there, and keeps any changes you made to `server.cfg`.

## Change the game mode, maps or bots

Edit `C:\Games\Xonotic\data\server.cfg` on the server laptop, then restart the server. The comments in the file explain each setting. For example, change `gametype tdm` to `ctf` for Capture the Flag.

## Troubleshooting

| Problem | Fix |
|---|---|
| "Windows protected your PC" | The programs aren't signed. Click **More info → Run anyway**, or ask IT to allow them. |
| "Couldn't find xonotic-0.8.6.zip" | Put the zip in the same folder as the setup program. |
| "doesn't match the official release" | The zip is damaged or still copying. Copy it again. |
| Server not in the Join list | Check that everyone is on the same network, and that the server laptop allowed `xonotic-dedicated` through the firewall. Try `connect <server IP>`. |
| Game stutters | In the game: **Settings → Effects → Low**. |

## Uninstall

Delete `C:\Games\Xonotic` and the desktop shortcuts.

## Build from source

Run `build.bat`. It uses the C# compiler that comes with Windows, so there's nothing to install. Both programs are built from `source/Setup.cs`; the server version is compiled with `/define:SERVER`.
