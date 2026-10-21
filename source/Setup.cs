// Xonotic LAN kit installer. Builds two programs from this one file:
//   Player setup:  csc Setup.cs                 -> installs the game, LAN settings, "Play Xonotic" shortcut
//   Server setup:  csc /define:SERVER Setup.cs  -> all of the above, plus the server files and starts the server
// Both expect xonotic-0.8.6.zip in the same folder as the exe (only needed for the first install).
// Options for testing: --target <folder> installs somewhere else, --no-shortcuts skips desktop shortcuts.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

static class Setup
{
    const string ZipName = "xonotic-0.8.6.zip";
    const string ZipSha512 = "cb39879e96f19abb2877588c2d50c5d3e64dd68153bec3dd1bebedf4d765e506afa419c28381d7005aed664cb1a042571c132b5b319e4308cab67745d996c2a6";
    const string InstallingMarker = ".lan-installing";

#if SERVER
    const string Title = "Xonotic LAN - Server Setup";
#else
    const string Title = "Xonotic LAN - Game Setup";
#endif

    static string Target = @"C:\Games\Xonotic";
    static bool Shortcuts = true;

    static int Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--target" && i + 1 < args.Length) Target = args[++i];
            else if (args[i] == "--no-shortcuts") Shortcuts = false;
        }
        Target = Path.GetFullPath(Target);
        Console.Title = Title;
        Console.WriteLine(Title);
        Console.WriteLine(new string('=', Title.Length));
        Console.WriteLine();

        int code = 0;
        try { Run(); }
        catch (Exception e)
        {
            code = 1;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("Setup failed: " + e.Message);
            Console.ResetColor();
        }
        Console.WriteLine();
        Console.WriteLine("Press Enter to close.");
        Console.ReadLine();
        return code;
    }

    static void Run()
    {
        InstallGame();
        WriteFile(Path.Combine(Target, @"data\autoexec\lan.cfg"), LanCfg);
        SetDpiAware(Path.Combine(Target, "xonotic.exe"));
        if (Shortcuts) CreateShortcut("Play Xonotic", Path.Combine(Target, "xonotic.exe"));
        Step("LAN settings and screen scaling fix applied.");

#if SERVER
        string cfg = Path.Combine(Target, @"data\server.cfg");
        if (File.Exists(cfg)) Step("Keeping your existing server settings (data\\server.cfg).");
        else WriteFile(cfg, ServerCfg);
        string bat = Path.Combine(Target, "start-server.bat");
        WriteFile(bat, StartServerBat);
        if (Shortcuts) CreateShortcut("Start Xonotic Server", bat);
        Step("Server files ready.");
        Console.WriteLine();
        StartServer(bat);
#else
        Console.WriteLine();
        Done("The game is installed. Use \"Play Xonotic\" on the desktop to start it.");
        Console.WriteLine("To join: Multiplayer > Join, then pick \"Work LAN\".");
        Console.WriteLine();
        Console.Write("Start the game now? [Y/n] ");
        string answer = Console.ReadLine();
        if (answer != null && !answer.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(Path.Combine(Target, "xonotic.exe")) { WorkingDirectory = Target });
#endif
    }

    // ---- Game install ----

    static void InstallGame()
    {
        bool installed = File.Exists(Path.Combine(Target, "xonotic.exe"))
            && File.Exists(Path.Combine(Target, @"data\xonotic-20230620-data.pk3"))
            && File.Exists(Path.Combine(Target, @"data\xonotic-20230620-maps.pk3"))
            && !File.Exists(Path.Combine(Target, InstallingMarker));
        if (installed)
        {
            Step("Xonotic is already installed in " + Target + ".");
            return;
        }

        string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ZipName);
        if (!File.Exists(zipPath))
            throw new Exception("Couldn't find " + ZipName + ". Put it in the same folder as this program and run it again.");

        Console.Write("Checking the game download is genuine... ");
        string hash;
        using (var sha = SHA512.Create())
        using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        if (hash != ZipSha512)
            throw new Exception(ZipName + " doesn't match the official release. It may be damaged or incomplete - copy it again.");
        Console.WriteLine("OK");

        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, InstallingMarker), "");
        string root = Target.TrimEnd('\\') + "\\";
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            // Skip the Mac app bundle and Unix symlinks; Windows doesn't use them.
            var entries = zip.Entries.Where(e => e.FullName.StartsWith("Xonotic/")
                && !e.FullName.StartsWith("Xonotic/Xonotic.app/")
                && ((e.ExternalAttributes >> 16) & 0xF000) != 0xA000).ToList();
            long total = entries.Sum(e => e.Length), done = 0;
            int lastPct = -1;
            foreach (var e in entries)
            {
                string rel = e.FullName.Substring("Xonotic/".Length).Replace('/', '\\');
                if (rel.Length == 0) continue;
                string dest = Path.GetFullPath(Path.Combine(Target, rel));
                if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                if (e.FullName.EndsWith("/")) { Directory.CreateDirectory(dest); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                e.ExtractToFile(dest, true);
                done += e.Length;
                int pct = total == 0 ? 100 : (int)(done * 100 / total);
                if (pct != lastPct) { Console.Write("\rInstalling Xonotic to " + Target + "... " + pct + "%"); lastPct = pct; }
            }
        }
        File.Delete(Path.Combine(Target, InstallingMarker));
        Console.WriteLine();
        Step("Xonotic installed.");
    }

    static void SetDpiAware(string exe)
    {
        // Same as Properties > Compatibility > "Override high DPI scaling" > Application, for this user.
        // Without it, scaled screens get a shrunk resolution stretched to fit (blurry).
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
            key.SetValue(exe, "~ HIGHDPIAWARE");
    }

    static void CreateShortcut(string name, string target)
    {
        string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), name + ".lnk");
        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(shellType);
        object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
        Type t = sc.GetType();
        t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { target });
        t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Target });
        t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { Path.Combine(Target, "xonotic.exe") + ",0" });
        t.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
        Step("Desktop shortcut \"" + name + "\" created.");
    }

    static void WriteFile(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
    }

    static void Step(string msg) { Console.WriteLine("  - " + msg); }

    static void Done(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

#if SERVER
    // ---- Server start ----

    static void StartServer(string bat)
    {
        if (Process.GetProcessesByName("xonotic-dedicated").Length > 0)
            Step("The server is already running.");
        else
        {
            Console.WriteLine("Starting the server in its own window (keep that window open while you play)...");
            Console.WriteLine("If Windows Firewall asks about xonotic-dedicated, click Allow.");
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + bat + "\"\"") { WorkingDirectory = Target, UseShellExecute = true });
        }

        string status = null;
        for (int i = 0; i < 30 && status == null; i++) { Thread.Sleep(1000); status = QueryServer(); }
        Console.WriteLine();
        if (status == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("The server didn't answer within 30 seconds. Check its window for errors.");
            Console.ResetColor();
            return;
        }
        Done("Server is up: " + status);
        Console.WriteLine();
        Console.WriteLine("Players join from Multiplayer > Join (look for \"Work LAN\"),");
        Console.WriteLine("or open the console with the ` key and type one of:");
        foreach (string ip in LanAddresses()) Console.WriteLine("    connect " + ip);
        Console.WriteLine();
        Console.WriteLine("To play on this laptop too, use \"Play Xonotic\" on the desktop.");
        Console.WriteLine("To stop the server, close its window.");
    }

    static string QueryServer()
    {
        try
        {
            using (var udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = 1000;
                udp.Connect("127.0.0.1", 26000);
                byte[] q = new byte[] { 255, 255, 255, 255 }.Concat(Encoding.ASCII.GetBytes("getinfo lan")).ToArray();
                udp.Send(q, q.Length);
                IPEndPoint ep = null;
                string[] kv = Encoding.ASCII.GetString(udp.Receive(ref ep)).Split('\n')[1].TrimStart('\\').Split('\\');
                string host = "", map = "", mode = "";
                for (int i = 0; i + 1 < kv.Length; i += 2)
                {
                    if (kv[i] == "hostname") host = kv[i + 1];
                    else if (kv[i] == "mapname") map = kv[i + 1];
                    else if (kv[i] == "qcstatus") mode = kv[i + 1].Split(':')[0];
                }
                if (map.StartsWith("_init")) return null; // still on the boot map, the real one isn't loaded yet
                return "\"" + host + "\", " + mode + " on " + map;
            }
        }
        catch { return null; }
    }

    static string[] LanAddresses()
    {
        // Real network adapters only: ones with a default gateway (skips virtual Hyper-V/VPN switches).
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .SelectMany(p => p.UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .ToArray();
    }

    const string ServerCfg = @"// Work LAN Xonotic server

hostname ""Work LAN""
sv_public 0              // LAN only: don't advertise to the internet master server
port 26000
maxplayers 16
sv_motd ""Welcome to the work LAN. Be nice.""

// Team Deathmatch rotation (change gametype to dm, ctf or ca for other modes)
gametype tdm
g_maplist ""stormkeep implosion solarium catharsis boil afterslime""
g_maplist_shuffle 1
timelimit_override 10
fraglimit_override 50

// Bots fill empty slots so odd numbers still get even teams
minplayers 6
skill 4                  // bot difficulty 0-10

// Uncomment and set to manage the server from a client console with rcon
// rcon_password ""change-me""
";

    const string StartServerBat = @"@echo off
rem Start the Xonotic LAN server. Put this file in the Xonotic folder (next to xonotic.exe).
cd /d ""%~dp0""

set EXE=
for %%F in (xonotic-x64-dedicated.exe xonotic-x86_64-dedicated.exe xonotic-dedicated.exe) do if exist ""%%F"" if not defined EXE set EXE=%%F

if not defined EXE (
  echo Could not find the Xonotic dedicated server exe in %CD%
  pause
  exit /b 1
)

echo Starting %EXE% ... other players join via Multiplayer ^> Join, or connect to this laptop's IP.
ipconfig | findstr /c:""IPv4""
""%~dp0%EXE%"" -sessionid server +serverconfig server.cfg
pause
";
#endif

    const string LanCfg = @"// Work LAN settings, run automatically at every launch (game and server).

// LAN only: no internet master servers, so the server list only shows games on this network
set sv_master1 """"
set sv_master2 """"
set sv_master3 """"
set sv_master4 """"
set sv_masterextra1 """"
set sv_masterextra2 """"
set sv_masterextra3 """"

// Always fullscreen at the screen's own resolution, whatever laptop this runs on
vid_fullscreen 1
vid_desktopfullscreen 1
";
}
