XONOTIC WORK LAN KIT
====================

Copy this whole folder to a USB stick or shared drive. Keep xonotic-0.8.6.zip
next to the two programs - they install the game from it.


PLAYERS
-------
1. Double-click "Install Game.exe". It takes under a minute.
2. Double-click "Play Xonotic" on your desktop.
3. Multiplayer > Join, then pick "Work LAN".
   Not listed? Press the ` key (above Tab) and type:  connect <server IP>
   (the server laptop shows its IP when the server starts)


SERVER LAPTOP (one laptop only)
-------------------------------
1. Double-click "Install and Start Server.exe".
   First time: it installs the game, then starts the server.
   After that: run it again any time to start the server.
   (The "Start Xonotic Server" desktop shortcut does the same.)
2. Keep the server window open while people play. Close it to stop the server.
3. If Windows Firewall asks about xonotic-dedicated, click Allow.
4. You can play on the server laptop too: use "Play Xonotic" on the desktop.


WHAT GETS INSTALLED
-------------------
- The game goes in C:\Games\Xonotic
- Desktop shortcuts: "Play Xonotic" (and "Start Xonotic Server" on the server)
- The server list only shows games on your own network
- The game always runs fullscreen at the screen's own resolution
- Server settings: C:\Games\Xonotic\data\server.cfg (mode, maps, bots).
  Your edits are kept if you run the server setup again.

To uninstall: delete C:\Games\Xonotic and the desktop shortcuts.

Weak laptop stuttering? In the game: Settings > Effects > Low.

The setup programs are built from source\Setup.cs.
