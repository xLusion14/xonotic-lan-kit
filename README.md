# Xonotic LAN Kit

One-click setup for a [Xonotic](https://xonotic.org) LAN party on Windows laptops. Xonotic is a free, open-source arena shooter. No accounts, no internet during play, no admin rights. The game is built into each setup file, so there's nothing else to download.

---

## Just want to play? Download ONLY this:

### ⬇️ [Xonotic-Player-Setup.exe](https://github.com/xLusion14/xonotic-lan-kit/releases/latest/download/Xonotic-Player-Setup.exe) (1.2 GB, includes the game)

1. Double-click **Xonotic-Player-Setup.exe**. It takes under a minute.
2. Double-click **Play Xonotic** on your desktop.
3. The game opens on **Games on your network**. Click **Work LAN**, then **Join!**

Set your name under **Your player**. The **⚙ gear** opens Settings. Click **Close**, or anywhere outside the window, to go back.

Not in the list? Click **Refresh**, or type the address shown in the host's **Xonotic Server** window into **Address** and click **Join!**

> ⚠️ **Players: don't download the Server Setup.** Only the one person hosting needs it.

### Playing with an Xbox controller

Plug it in (wired, or Bluetooth paired with Windows) before starting the game. Mouse and keyboard keep working too.

| Control | Does |
|---|---|
| Left stick / right stick | Move / look |
| Right trigger / left trigger | Fire / secondary fire |
| A / B | Jump / crouch |
| X / Y | Reload / last weapon |
| RB / LB | Next / previous weapon |
| Right stick click | Zoom |
| D-pad up / down | Best weapon / drop weapon |
| View (Back) | Scoreboard |
| Menu (Start) | Open the menu (use the mouse there) |

Heads-up: there's no aim assist, so players on mouse have a big advantage.

---

## Hosting the game (ONE person only)

Download **[Xonotic-Server-Setup-HOST-ONLY.exe](https://github.com/xLusion14/xonotic-lan-kit/releases/latest/download/Xonotic-Server-Setup-HOST-ONLY.exe)** (1.2 GB, includes the game).

1. Double-click it and type **Y** to confirm. It installs the game, then opens the **Xonotic Server** window, which starts the server.
2. If Windows Firewall asks about `xonotic-dedicated`, click **Allow**.
3. Keep the **Xonotic Server** window open while people play. Closing it stops the server (it asks first).
4. Next time, use the **Xonotic Server** shortcut on your desktop.

You can play on the host laptop too: use **Play Xonotic** on its desktop.

**Handing out the game:** put `Xonotic-Player-Setup.exe` on a USB stick or shared folder so players don't each have to download 1.2 GB.

### The Xonotic Server window

- **How players join**: the server's name and the address players can type in, with a **Copy** button.
- **Now playing** and **Players**: the current mode and map, and who's in the game.
- **Game mode** and **Map**: tap a mode, tap a map (or **Random**), then **Change Now**. Everyone moves to the new map together; nobody is disconnected. **Restart Match** starts the current map over.
- **Game settings**: time and score limits, max players, how many bots fill empty slots, bot skill, server name and welcome message. **Save Settings** keeps them for next time. Bots, name and message change right away; limits and max players apply from the next map.
- **Start Server**, **Restart** and **Stop** at the top, and the **Server log** at the bottom.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| "Windows protected your PC" | The programs aren't signed. Click **More info → Run anyway**, or ask IT to allow them. |
| "doesn't contain the game" or "don't match the official release" | The setup file didn't finish downloading or copying. Get it again. |
| Server not in the games list | Check that everyone is on the same network, and that the host clicked **Allow** for `xonotic-dedicated` in Windows Firewall. Type the address from the **Xonotic Server** window into **Address** and click **Join!** Some work Wi-Fi networks block laptops from seeing each other; then only the typed address works, or not at all. |
| "The Xonotic Server window is open" when running the server setup | Close the **Xonotic Server** window first. |
| Game stutters | Click the **⚙ gear → Effects** and pick **Low**. |
| Text too big or small | Click the **⚙ gear → Video → Font/UI size**. |

## What the setup does

- Checks that the built-in game files are the official, undamaged Xonotic 0.8.6 release (SHA-512), then installs the game to `C:\Games\Xonotic`.
- Makes the server list show only games on your own network.
- Runs the game fullscreen at the screen's real resolution. On scaled screens (125%, 150% and up) it sets Windows' "Override high DPI scaling → Application" option for Xonotic, for the current user only.
- Replaces the main menu with a simpler LAN screen: the list of games on your network, **Your player**, **Practice vs bots**, a **⚙ gear** for Settings, and **Quit** (`data\zzz-lan-menu.pk3`; the official game files aren't changed).
- Adds the Xbox controller preset above (`data\autoexec\lan-controller.cfg`).
- On the game's first launch only: picks a text size to suit the screen's Windows scaling, and switches to Low effects on laptops without a dedicated graphics card. Players can change both afterwards.
- Adds a **Play Xonotic** desktop shortcut.
- Server setup only: writes `server.cfg`, installs the **Xonotic Server** window and its desktop shortcut, and opens it.

Running either setup again is safe: it skips installing if the game is already there, and keeps the server settings.

**Uninstall:** delete `C:\Games\Xonotic` and the desktop shortcuts.

### Fixes in this kit for LAN play

Two things in stock Xonotic 0.8.6 make a LAN server vanish from the games list a few seconds after it appears. The kit fixes both:

- The server ignores a second status query from the same address within 1 second (flood protection), which drops the server browser's follow-up query. The kit turns that off (`net_getstatusfloodblockingtimeout 0`), which is safe on a private LAN.
- When the mouse moves onto the list, the menu refreshes it, and the engine times the LAN reply from the previous query, giving the server a ~9999 ms ping that the Laggy filter hides for good. The LAN menu does a full search there instead (`menu/qcsrc/menu/xonotic/serverlist.qc`).

## Build from source

1. Download `xonotic-0.8.6.zip` from [xonotic.org/download](https://xonotic.org/download) into this folder.
2. Run `build.bat`.

It uses the C# compiler and WPF that come with Windows, so there's nothing to install. It builds the **Xonotic Server** window from `source/ServerPanel.cs` and `source/ServerPanel.xaml`, then both setups from `source/Setup.cs` (the server version with `/define:SERVER`, with the server window embedded), then `source/pack.ps1` appends the game zip to each setup.

**The LAN main menu** (`menu/`) is a small change to Xonotic's own menu code: the changed files are in `menu/qcsrc`, alongside a fix that lets the gmqcc compiler build with Microsoft's C++ compiler (`menu/gmqcc/fold.cpp`). The built add-on, `menu/zzz-lan-menu.pk3`, is committed, so you only need to rebuild it if you change the menu. To rebuild it:

1. Install the game (it includes Xonotic's source) and Visual Studio Build Tools with the C++ workload.
2. Run `powershell -ExecutionPolicy Bypass -File menu\build-menu.ps1`.
3. Run `build.bat`.
