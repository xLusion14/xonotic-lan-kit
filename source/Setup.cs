// Xonotic LAN kit installer. Builds two programs from this one file (see build.bat):
//   Xonotic-Player-Setup.exe            (csc Setup.cs)                -> installs the game, LAN settings, "Play Xonotic" shortcut
//   Xonotic-Server-Setup-HOST-ONLY.exe  (csc /define:SERVER Setup.cs) -> all of the above, plus the server files and starts the server
// build.bat appends xonotic-0.8.6.zip to both exes, so each is a single self-contained installer.
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
    const string PlayerExe = "Xonotic-Player-Setup.exe";
    const string PanelExe = "Xonotic Server.exe";

#if SERVER
    const string Title = "Xonotic LAN - SERVER Setup (host only)";
#else
    const string Title = "Xonotic LAN - Player Setup";
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
#if SERVER
        // Ask first so a player who grabbed the wrong exe doesn't end up hosting. Skipped once this laptop is the server.
        bool isHost = File.Exists(Path.Combine(Target, PanelExe)) || File.Exists(Path.Combine(Target, "start-server.bat"));
        if (!isHost && !ConfirmHost()) return;
        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PanelExe)).Length > 0)
            throw new Exception("The Xonotic Server window is open. Close it first, then run this setup again.");
#endif
        InstallGame();
        WriteFile(Path.Combine(Target, @"data\autoexec\lan.cfg"), LanCfg);
        WriteFile(Path.Combine(Target, @"data\autoexec\lan-controller.cfg"), ControllerCfg);
        WriteFile(Path.Combine(Target, @"data\autoexec\lan-firstrun.cfg"), FirstRunCfg());
        WriteResource("zzz-lan-menu.pk3", Path.Combine(Target, @"data\zzz-lan-menu.pk3"));
        SetDpiAware(Path.Combine(Target, "xonotic.exe"));
        if (Shortcuts) CreateShortcut("Play Xonotic", Path.Combine(Target, "xonotic.exe"));
        Step("LAN main menu, controller preset, LAN settings and screen scaling fix applied.");

#if SERVER
        string cfg = Path.Combine(Target, @"data\server.cfg");
        if (File.Exists(cfg)) Step("Keeping your existing server settings (data\\server.cfg).");
        else WriteFile(cfg, ServerCfg);
        string panel = Path.Combine(Target, PanelExe);
        WriteResource(PanelExe, panel);
        if (Shortcuts)
        {
            // the panel replaces the old command-window shortcut
            string old = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Start Xonotic Server.lnk");
            if (File.Exists(old)) File.Delete(old);
            CreateShortcut("Xonotic Server", panel);
        }
        Step("Server control panel installed.");
        Console.WriteLine();
        Done("Opening the Xonotic Server window. It starts the server for you.");
        Console.WriteLine("Keep that window open while people play. Next time, use \"Xonotic Server\" on the desktop.");
        Console.WriteLine("If Windows Firewall asks about xonotic-dedicated, click Allow.");
        Console.WriteLine();
        Console.WriteLine("Players install with " + PlayerExe + ".");
        Console.WriteLine("To play on this laptop too, use \"Play Xonotic\" on the desktop.");
        Process.Start(new ProcessStartInfo(panel) { WorkingDirectory = Target });
#else
        Console.WriteLine();
        Done("The game is installed. Use \"Play Xonotic\" on the desktop to start it.");
        Console.WriteLine("To join: pick \"Work LAN\" from the games list, then click Join.");
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

        using (Stream payload = OpenPayload())
        {
            if (payload == null)
                throw new Exception("This setup file doesn't contain the game. It may not have finished downloading - download it again.");

            Console.Write("Checking the game files are genuine... ");
            string hash;
            using (var sha = SHA512.Create())
                hash = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
            if (hash != ZipSha512)
                throw new Exception("The game files don't match the official Xonotic release. The setup file may be damaged - download or copy it again.");
            Console.WriteLine("OK");
            payload.Position = 0;

            Directory.CreateDirectory(Target);
            File.WriteAllText(Path.Combine(Target, InstallingMarker), "");
            string root = Target.TrimEnd('\\') + "\\";
            using (var zip = new ZipArchive(payload, ZipArchiveMode.Read, true))
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
        }
        File.Delete(Path.Combine(Target, InstallingMarker));
        Console.WriteLine();
        Step("Xonotic installed.");
    }

    // The game zip is appended to the end of this exe by build.bat (source\pack.ps1), followed by a
    // 16-byte trailer: zip length (Int64, little-endian) + "XONLANK1". Falls back to the zip next to the exe.
    const string PayloadMagic = "XONLANK1";

    static Stream OpenPayload()
    {
        var self = new FileStream(Process.GetCurrentProcess().MainModule.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        if (self.Length > 16)
        {
            byte[] trailer = new byte[16];
            self.Seek(-16, SeekOrigin.End);
            int got = 0;
            while (got < 16) { int n = self.Read(trailer, got, 16 - got); if (n == 0) break; got += n; }
            long length = BitConverter.ToInt64(trailer, 0);
            if (got == 16 && Encoding.ASCII.GetString(trailer, 8, 8) == PayloadMagic && length > 0 && length <= self.Length - 16)
                return new SubStream(self, self.Length - 16 - length, length);
        }
        self.Dispose();

        string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ZipName);
        return File.Exists(zipPath) ? new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20) : null;
    }

    // Read-only, seekable window onto part of another stream (the zip inside this exe).
    sealed class SubStream : Stream
    {
        readonly Stream inner;
        readonly long start, length;
        long pos;

        public SubStream(Stream inner, long start, long length) { this.inner = inner; this.start = start; this.length = length; }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position { get { return pos; } set { pos = value; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long left = length - pos;
            if (left <= 0) return 0;
            if (count > left) count = (int)left;
            inner.Position = start + pos;
            int n = inner.Read(buffer, offset, count);
            pos += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            pos = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? pos + offset : length + offset;
            return pos;
        }

        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }

    // Defaults for this laptop, applied on the game's first launch only so players can still change them:
    // menu text size from the Windows display scaling, and Low effects when there's no dedicated graphics card.
    static string FirstRunCfg()
    {
        int dpi = 96;
        using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics"))
            if (key != null && key.GetValue("AppliedDPI") is int) dpi = (int)key.GetValue("AppliedDPI");
        string scale = dpi <= 120 ? "0" : dpi <= 168 ? "0.25" : "0.5"; // Medium / Large / Huge

        string[] gpus = new string[0];
        try
        {
            using (var search = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                gpus = search.Get().Cast<System.Management.ManagementBaseObject>().Select(g => (string)g["Name"] ?? "").ToArray();
        }
        catch { }
        bool dedicated = gpus.Any(n => System.Text.RegularExpressions.Regex.IsMatch(n,
            @"nvidia|geforce|quadro|radeon\s*(\(tm\)\s*)?(rx|pro)|arc\s*(\(tm\)\s*)?[ab]\d", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        Step("Text size " + (scale == "0" ? "Medium" : scale == "0.25" ? "Large" : "Huge") + " (screen scaling " + (dpi * 100 / 96) + "%), "
            + (dedicated ? "normal effects (graphics card found)." : "Low effects (no dedicated graphics card)."));
        return "// Written by the setup for this laptop. Runs on the game's first launch only;\n"
            + "// after that, Settings in the game are yours to change.\n"
            + "alias _lan_firstrun_ \"menu_vid_scale " + scale + (dedicated ? "" : "; exec effects-low.cfg") + "; seta lan_firstrun_done 1\"\n"
            + "alias _lan_firstrun_1 \"\"\n"
            + "_lan_firstrun_${lan_firstrun_done ?}\n";
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

    // Files embedded with csc /resource (see build.bat), e.g. the LAN main menu add-on built by menu\build-menu.ps1.
    static void WriteResource(string name, string path)
    {
        using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (res == null) throw new Exception("This setup file is missing " + name + ". Download it again.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream f = File.Create(path)) res.CopyTo(f);
        }
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

    static bool ConfirmHost()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("This is the SERVER setup. Only ONE laptop - the host - needs it.");
        Console.WriteLine();
        Console.WriteLine("Just want to play? Close this window and run " + PlayerExe + " instead.");
        Console.ResetColor();
        Console.WriteLine();
        Console.Write("Make this laptop the server? Type Y and press Enter: ");
        string answer = Console.ReadLine();
        Console.WriteLine();
        if (answer != null && answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase)) return true;
        Console.WriteLine("Cancelled. Nothing was changed.");
        return false;
    }

    const string ServerCfg = @"// Work LAN Xonotic server

hostname ""Work LAN""
sv_public 0              // LAN only: don't advertise to the internet master server
port 26000
maxplayers 16
sv_motd ""Welcome to the work LAN. Be nice.""

// Team Deathmatch rotation. Change the mode, maps and limits in the Xonotic Server window.
gametype tdm
g_maplist ""afterslime boil implosion solarium stormkeep""
g_maplist_shuffle 1
timelimit_override 10
fraglimit_override 50

// Bots fill empty slots so odd numbers still get even teams
minplayers 6
skill 4                  // bot difficulty 0-10

// Uncomment and set to manage the server from a client console with rcon
// rcon_password ""change-me""
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

// Server: answer every server-list query. The default 1-second flood block drops the server browser's
// follow-up query, so the server vanished from the games list a few seconds after it appeared.
set net_getstatusfloodblockingtimeout 0
";

    // Xbox (XInput) controller preset. The engine sends Xbox buttons as X360_* keys, which Xonotic 0.8.6
    // leaves unbound. Runs every launch, so these buttons always match the README.
    const string ControllerCfg = @"// Xbox controller preset, run automatically at every launch.
// Mouse and keyboard keep working as normal; without a controller this does nothing.
joy_enable 1

// Sticks: left moves, right looks. Look speed at full tilt is these numbers times the default
// turn speed (140 degrees a second); the engine's default of 1 is too slow for a shooter.
joy_x360_sensitivityyaw -2.2
joy_x360_sensitivitypitch -1.6
joy_x360_deadzoneyaw 0.18
joy_x360_deadzonepitch 0.18

bind X360_RIGHT_TRIGGER +fire
bind X360_LEFT_TRIGGER +fire2
bind X360_A +jump
bind X360_B +crouch
bind X360_X reload
bind X360_Y weaplast
bind X360_RIGHT_SHOULDER weapnext
bind X360_LEFT_SHOULDER weapprev
bind X360_RIGHT_THUMB +zoom
bind X360_DPAD_UP weapbest
bind X360_DPAD_DOWN dropweapon
bind X360_BACK +showscores
bind X360_START togglemenu
";
}
