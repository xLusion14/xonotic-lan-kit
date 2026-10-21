# Xonotic LAN Kit

One-click setup for a [Xonotic](https://xonotic.org) LAN party on Windows laptops. Xonotic is a free, open-source arena shooter. No accounts, no internet during play, no admin rights. The game is built into each setup file, so there's nothing else to download.

---

## Just want to play? Download ONLY this:

### ⬇️ [Xonotic-Player-Setup.exe](https://github.com/xLusion14/xonotic-lan-kit/releases/latest/download/Xonotic-Player-Setup.exe) (1.2 GB, includes the game)

1. Double-click **Xonotic-Player-Setup.exe**. It takes under a minute.
2. Double-click **Play Xonotic** on your desktop.
3. Go to **Multiplayer → Join** and pick **Work LAN**.

Not in the list? Press the `` ` `` key (above Tab) and type `connect <server IP>`. Ask the host for the IP.

> ⚠️ **Players: don't download the Server Setup.** Only the one person hosting needs it.

---

## Hosting the game (ONE person only)

Download **[Xonotic-Server-Setup-HOST-ONLY.exe](https://github.com/xLusion14/xonotic-lan-kit/releases/latest/download/Xonotic-Server-Setup-HOST-ONLY.exe)** (1.2 GB, includes the game).

1. Double-click it and type **Y** to confirm. It installs the game, starts the server, and shows the IP players can connect to.
2. Keep the server window open while people play. Close it to stop the server.
3. If Windows Firewall asks about `xonotic-dedicated`, click **Allow**.
4. Next time, use the **Start Xonotic Server** shortcut on your desktop.

You can play on the host laptop too: use **Play Xonotic** on its desktop.

**Handing out the game:** put `Xonotic-Player-Setup.exe` on a USB stick or shared folder so players don't each have to download 1.2 GB.

### Change the game mode, maps or bots

Edit `C:\Games\Xonotic\data\server.cfg` on the host laptop, then restart the server. The comments in the file explain each setting. For example, change `gametype tdm` to `ctf` for Capture the Flag.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| "Windows protected your PC" | The programs aren't signed. Click **More info → Run anyway**, or ask IT to allow them. |
| "doesn't contain the game" or "don't match the official release" | The setup file didn't finish downloading or copying. Get it again. |
| Server not in the Join list | Check that everyone is on the same network, and that the host allowed `xonotic-dedicated` through the firewall. Try `connect <server IP>`. |
| Game stutters | In the game: **Settings → Effects → Low**. |

## What the setup does

- Checks that the built-in game files are the official, undamaged Xonotic 0.8.6 release (SHA-512), then installs the game to `C:\Games\Xonotic`.
- Makes the server list show only games on your own network.
- Runs the game fullscreen at the screen's real resolution. On scaled screens (125%, 150% and up) it sets Windows' "Override high DPI scaling → Application" option for Xonotic, for the current user only.
- Adds a **Play Xonotic** desktop shortcut.
- Server setup only: writes `server.cfg` and `start-server.bat`, adds a **Start Xonotic Server** shortcut, starts the server and checks it answers.

Running either setup again is safe: it skips installing if the game is already there, and keeps any changes made to `server.cfg`.

**Uninstall:** delete `C:\Games\Xonotic` and the desktop shortcuts.

## Build from source

1. Download `xonotic-0.8.6.zip` from [xonotic.org/download](https://xonotic.org/download) into this folder.
2. Run `build.bat`.

It uses the C# compiler that comes with Windows, so there's nothing to install. Both programs are built from `source/Setup.cs` (the server version with `/define:SERVER`), then `source/pack.ps1` appends the game zip to each one.
