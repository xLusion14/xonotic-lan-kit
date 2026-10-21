// Xonotic LAN kit server panel ("Xonotic Server.exe"). Runs the dedicated server in the background (no console
// window) and lets the host change mode, map and settings while it runs. Commands go to the server over rcon on
// localhost, with a random password made fresh each time the panel starts the server.
// The window layout is source\ServerPanel.xaml, embedded in the exe. Build: see build.bat.
// Lives next to xonotic.exe; the server setup installs it.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

static class Program
{
    [STAThread]
    static void Main()
    {
        new Application().Run(new ServerPanel().Window);
    }
}

// One row in the Players card.
public class PlayerRow
{
    public string Name { get; set; }
    public string Initial { get; set; }
    public string Detail { get; set; }
    public Brush Color { get; set; }
}

class ServerPanel
{
    // Game modes the panel offers: Xonotic's short name and what players call it.
    static readonly string[,] Modes =
    {
        { "tdm", "Team Deathmatch" }, { "dm", "Deathmatch" }, { "ctf", "Capture the Flag" }, { "ca", "Clan Arena" },
        { "ft", "Freeze Tag" }, { "lms", "Last Man Standing" }, { "kh", "Key Hunt" }, { "dom", "Domination" },
        { "ka", "Keepaway" }, { "duel", "Duel" },
    };
    static readonly Brush[] AvatarColors =
    {
        Hex("#007AFF"), Hex("#34C759"), Hex("#FF9500"), Hex("#AF52DE"), Hex("#FF2D55"), Hex("#5AC8FA"), Hex("#5856D6"), Hex("#FF3B30"),
    };
    const string RandomMap = "Random";
    const int Port = 26000;

    public readonly Window Window;
    readonly string gameDir = AppDomain.CurrentDomain.BaseDirectory;
    readonly Dictionary<string, List<string>> mapsByMode = new Dictionary<string, List<string>>();
    readonly Random random = new Random();
    readonly Dictionary<string, Stepper> steppers = new Dictionary<string, Stepper>();
    Process server;
    string rconPassword;
    string currentMap = "";
    bool closing;

    Border statusPill; Ellipse statusDot; TextBlock statusText, joinText, addressText, nowPlayingText, nowPlayingDetail, playersTitle, nobodyText;
    Button startButton, stopButton, restartServerButton, changeNowButton, restartMatchButton, saveButton, copyButton;
    ListBox modeChips, mapChips;
    ItemsControl playersList;
    TextBox serverNameBox, welcomeBox, logBox;
    Grid settingsGrid;

    public ServerPanel()
    {
        using (Stream xaml = Assembly.GetExecutingAssembly().GetManifestResourceStream("ServerPanel.xaml"))
            Window = (Window)XamlReader.Load(xaml);
        try { Window.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location).Handle, Int32Rect.Empty, null); } catch { }

        statusPill = Find<Border>("StatusPill"); statusDot = Find<Ellipse>("StatusDot"); statusText = Find<TextBlock>("StatusText");
        joinText = Find<TextBlock>("JoinText"); addressText = Find<TextBlock>("AddressText");
        nowPlayingText = Find<TextBlock>("NowPlayingText"); nowPlayingDetail = Find<TextBlock>("NowPlayingDetail");
        playersTitle = Find<TextBlock>("PlayersTitle"); nobodyText = Find<TextBlock>("NobodyText");
        startButton = Find<Button>("StartButton"); stopButton = Find<Button>("StopButton"); restartServerButton = Find<Button>("RestartServerButton");
        changeNowButton = Find<Button>("ChangeNowButton"); restartMatchButton = Find<Button>("RestartMatchButton");
        saveButton = Find<Button>("SaveButton"); copyButton = Find<Button>("CopyButton");
        modeChips = Find<ListBox>("ModeChips"); mapChips = Find<ListBox>("MapChips");
        playersList = Find<ItemsControl>("PlayersList");
        serverNameBox = Find<TextBox>("ServerNameBox"); welcomeBox = Find<TextBox>("WelcomeBox"); logBox = Find<TextBox>("LogBox");
        settingsGrid = Find<Grid>("SettingsGrid");

        AddStepper("time", "Time limit", "minutes", 0, 120, 5, "No limit");
        AddStepper("score", "Score limit", "points", 0, 1000, 10, "No limit");
        AddStepper("max", "Max players", "", 2, 32, 1, null);
        AddStepper("bots", "Bots fill up to", "players", 0, 32, 1, "No bots");
        AddStepper("skill", "Bot skill", "of 10", 0, 10, 1, null);

        for (int i = 0; i < Modes.GetLength(0); i++) modeChips.Items.Add(Modes[i, 1]);
        modeChips.SelectionChanged += (s, e) => FillMaps();

        startButton.Click += (s, e) => StartServer();
        stopButton.Click += (s, e) => StopServer();
        restartServerButton.Click += (s, e) => { StopServer(); StartServer(); };
        changeNowButton.Click += (s, e) => ApplyGame();
        restartMatchButton.Click += (s, e) => { if (currentMap != "") { Rcon("changelevel " + currentMap); Log("Restarting the match on " + currentMap + "..."); } };
        saveButton.Click += (s, e) => SaveSettings(true);
        copyButton.Click += (s, e) => { if (addressText.Text != "—") { Clipboard.SetText(addressText.Text); copyButton.Content = "Copied"; } };

        LoadMaps();
        LoadSettings();
        SetStatus(false, null);

        var poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        poll.Tick += async (s, e) => await PollStatus();
        poll.Start();

        Window.ContentRendered += (s, e) => StartServer();
        Window.Closing += OnClosingPanel;
    }

    T Find<T>(string name) where T : class
    {
        var e = Window.FindName(name) as T;
        if (e == null) throw new Exception("ServerPanel.xaml is missing " + name);
        return e;
    }

    static Brush Hex(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    // ---- Steppers: label on the left, "- value +" on the right ----

    class Stepper
    {
        public int Value, Min, Max, Step;
        public string Unit, ZeroText;
        public TextBlock Text;
        public void Set(int v)
        {
            Value = Math.Min(Max, Math.Max(Min, v));
            Text.Text = Value == 0 && ZeroText != null ? ZeroText : (Value + (Unit != "" ? " " + Unit : ""));
        }
    }

    void AddStepper(string key, string label, string unit, int min, int max, int step, string zeroText)
    {
        int row = settingsGrid.RowDefinitions.Count;
        settingsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var name = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        var sep = new Border { BorderBrush = (Brush)Window.FindResource("Separator"), BorderThickness = new Thickness(0, 0, 0, row == 4 ? 0 : 1) };
        Grid.SetRow(sep, row); Grid.SetColumnSpan(sep, 2);
        Grid.SetRow(name, row);
        settingsGrid.Children.Add(sep);
        settingsGrid.Children.Add(name);

        var st = new Stepper { Min = min, Max = max, Step = step, Unit = unit, ZeroText = zeroText };
        st.Text = new TextBlock { MinWidth = 96, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
        var minus = new Button { Content = "−", Style = (Style)Window.FindResource("Step") };
        var plus = new Button { Content = "+", Style = (Style)Window.FindResource("Step") };
        System.Windows.Automation.AutomationProperties.SetAutomationId(minus, key + "Minus");
        System.Windows.Automation.AutomationProperties.SetAutomationId(plus, key + "Plus");
        minus.Click += (s, e) => st.Set(st.Value - st.Step);
        plus.Click += (s, e) => st.Set(st.Value + st.Step);
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(minus); panel.Children.Add(st.Text); panel.Children.Add(plus);
        var box = new Border { Background = (Brush)Window.FindResource("Fill"), CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 6, 0, 6), Child = panel };
        Grid.SetRow(box, row); Grid.SetColumn(box, 1);
        settingsGrid.Children.Add(box);
        steppers[key] = st;
        st.Set(min);
    }

    // ---- Maps and settings ----

    // Which maps support which mode, from the maps' own .mapinfo files ("gametype ctf" lines).
    void LoadMaps()
    {
        foreach (string pk3 in Directory.GetFiles(System.IO.Path.Combine(gameDir, "data"), "*maps*.pk3"))
        {
            try
            {
                using (var zip = ZipFile.OpenRead(pk3))
                    foreach (var e in zip.Entries.Where(x => x.FullName.StartsWith("maps/") && x.FullName.EndsWith(".mapinfo")))
                    {
                        string map = System.IO.Path.GetFileNameWithoutExtension(e.Name);
                        if (map.StartsWith("_")) continue;
                        string text;
                        using (var r = new StreamReader(e.Open())) text = r.ReadToEnd();
                        foreach (Match m in Regex.Matches(text, @"(?m)^(?:gametype|type)\s+(\w+)"))
                        {
                            List<string> list;
                            if (!mapsByMode.TryGetValue(m.Groups[1].Value, out list)) mapsByMode[m.Groups[1].Value] = list = new List<string>();
                            if (!list.Contains(map)) list.Add(map);
                        }
                    }
            }
            catch (Exception ex) { Log("Couldn't read maps from " + System.IO.Path.GetFileName(pk3) + ": " + ex.Message); }
        }
        foreach (var list in mapsByMode.Values) list.Sort();
    }

    string SelectedMode() { return modeChips.SelectedIndex < 0 ? "tdm" : Modes[modeChips.SelectedIndex, 0]; }
    string ModeName(string mode) { for (int i = 0; i < Modes.GetLength(0); i++) if (Modes[i, 0] == mode) return Modes[i, 1]; return mode; }

    List<string> MapsFor(string mode)
    {
        List<string> list;
        return mapsByMode.TryGetValue(mode, out list) ? list : new List<string>();
    }

    void FillMaps()
    {
        mapChips.Items.Clear();
        mapChips.Items.Add(RandomMap);
        foreach (string m in MapsFor(SelectedMode())) mapChips.Items.Add(m);
        mapChips.SelectedIndex = 0;
    }

    string CfgPath { get { return System.IO.Path.Combine(gameDir, @"data\server.cfg"); } }

    void LoadSettings()
    {
        var v = new Dictionary<string, string>();
        if (File.Exists(CfgPath))
            foreach (string line in File.ReadAllLines(CfgPath))
            {
                var m = Regex.Match(line, @"^\s*(\w+)\s+(""[^""]*""|\S+)");
                if (m.Success) v[m.Groups[1].Value] = m.Groups[2].Value.Trim('"');
            }
        string mode = Get(v, "gametype", "tdm");
        modeChips.SelectedIndex = Math.Max(0, Enumerable.Range(0, Modes.GetLength(0)).FirstOrDefault(i => Modes[i, 0] == mode));
        steppers["time"].Set(Int(Get(v, "timelimit_override", "10")));
        steppers["score"].Set(Int(Get(v, "fraglimit_override", "50")));
        steppers["max"].Set(Int(Get(v, "maxplayers", "16")));
        steppers["bots"].Set(Int(Get(v, "minplayers", "6")));
        steppers["skill"].Set(Int(Get(v, "skill", "4")));
        serverNameBox.Text = Get(v, "hostname", "Work LAN");
        welcomeBox.Text = Get(v, "sv_motd", "Welcome to the work LAN. Be nice.");
    }

    static string Get(Dictionary<string, string> v, string key, string fallback) { string s; return v.TryGetValue(key, out s) ? s : fallback; }
    static int Int(string s) { int i; return int.TryParse(s, out i) && i > 0 ? i : 0; }  // -1 ("map default") shows as 0
    static string Q(string s) { return "\"" + s.Replace("\"", "'") + "\""; }
    int V(string key) { return steppers[key].Value; }

    // Writes server.cfg from the panel's values; with live=true also sends them to the running server.
    void SaveSettings(bool live)
    {
        var maps = MapsFor(SelectedMode());
        var cfg = new StringBuilder();
        cfg.AppendLine("// Work LAN Xonotic server. Written by the Xonotic Server panel; change these there.");
        cfg.AppendLine();
        cfg.AppendLine("hostname " + Q(serverNameBox.Text));
        cfg.AppendLine("sv_public 0              // LAN only: don't advertise to the internet master server");
        cfg.AppendLine("port " + Port);
        cfg.AppendLine("maxplayers " + V("max"));
        cfg.AppendLine("sv_motd " + Q(welcomeBox.Text));
        cfg.AppendLine();
        cfg.AppendLine("gametype " + SelectedMode());
        cfg.AppendLine("g_maplist " + Q(string.Join(" ", maps)));
        cfg.AppendLine("g_maplist_shuffle 1");
        cfg.AppendLine("timelimit_override " + V("time"));
        cfg.AppendLine("fraglimit_override " + V("score"));
        cfg.AppendLine();
        cfg.AppendLine("minplayers " + V("bots") + "             // bots fill empty slots up to this many players");
        cfg.AppendLine("skill " + V("skill") + "                  // bot difficulty 0-10");
        try { File.WriteAllText(CfgPath, cfg.ToString()); }
        catch (Exception ex) { MessageBox.Show(Window, "Couldn't save server.cfg: " + ex.Message, "Xonotic Server"); return; }

        if (live && IsRunning())
        {
            Rcon("hostname " + Q(serverNameBox.Text) + "; sv_motd " + Q(welcomeBox.Text) + "; maxplayers " + V("max")
                + "; timelimit_override " + V("time") + "; fraglimit_override " + V("score")
                + "; minplayers " + V("bots") + "; skill " + V("skill"));
            Log("Settings saved and sent to the server.");
        }
        else if (live) Log("Settings saved. They'll be used when the server starts.");
    }

    void ApplyGame()
    {
        string mode = SelectedMode();
        var maps = MapsFor(mode);
        if (maps.Count == 0) { MessageBox.Show(Window, "No maps support " + ModeName(mode) + ".", "Xonotic Server"); return; }
        string map = (mapChips.SelectedItem as string ?? RandomMap) == RandomMap ? maps[random.Next(maps.Count)] : (string)mapChips.SelectedItem;
        SaveSettings(false);
        // sv_cmd takes the rest of the line as its arguments, so it gets its own packet; changelevel switches
        // right away and keeps everyone connected (sv_cmd gotomap would wait for the end-of-match scoreboard).
        Rcon("timelimit_override " + V("time") + "; fraglimit_override " + V("score") + "; g_maplist " + Q(string.Join(" ", maps)),
            "sv_cmd gametype " + mode,
            "changelevel " + map);
        Log("Changing to " + ModeName(mode) + " on " + map + "...");
    }

    // ---- Server process ----

    bool IsRunning() { return server != null && !server.HasExited; }

    void UpdateButtons()
    {
        bool running = IsRunning();
        startButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        stopButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        restartServerButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        changeNowButton.IsEnabled = running;
        restartMatchButton.IsEnabled = running;
    }

    void StartServer()
    {
        if (IsRunning()) return;
        string exe = System.IO.Path.Combine(gameDir, "xonotic-dedicated.exe");
        if (!File.Exists(exe)) { MessageBox.Show(Window, "Can't find xonotic-dedicated.exe next to this program.", "Xonotic Server"); return; }

        var others = Process.GetProcessesByName("xonotic-dedicated");
        if (others.Length > 0)
        {
            var answer = MessageBox.Show(Window, "Another Xonotic server is already running on this laptop.\n\nStop it and run the server from here instead?", "Xonotic Server", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            foreach (var p in others) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
        }

        if (!File.Exists(CfgPath)) SaveSettings(false);
        rconPassword = Guid.NewGuid().ToString("N");
        // Xonotic's commands.cfg sets rcon_secure 1, which ignores plain rcon; set it back to 0 first,
        // since changing rcon_secure clears rcon_password.
        var psi = new ProcessStartInfo(exe,
            "-sessionid server +set net_getstatusfloodblockingtimeout 0 +set rcon_secure 0 +set rcon_password " + rconPassword + " +serverconfig server.cfg")
        {
            WorkingDirectory = gameDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        server = new Process { StartInfo = psi, EnableRaisingEvents = true };
        server.OutputDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
        server.ErrorDataReceived += (s, e) => { if (e.Data != null) Log(e.Data); };
        server.Exited += (s, e) => Window.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!closing) Log("Server stopped.");
            SetStatus(false, null);
            UpdateButtons();
        }));
        try
        {
            server.Start();
            server.BeginOutputReadLine();
            server.BeginErrorReadLine();
            Log("Starting the server... If Windows Firewall asks about xonotic-dedicated, click Allow.");
            SetPill("Starting", "#FFF4E0", "#FF9500", "#B36200");
        }
        catch (Exception ex) { Log("Couldn't start the server: " + ex.Message); server = null; }
        UpdateButtons();
    }

    void StopServer()
    {
        if (!IsRunning()) return;
        try { server.Kill(); server.WaitForExit(5000); } catch { }
        SetStatus(false, null);
        UpdateButtons();
    }

    void OnClosingPanel(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!IsRunning()) return;
        var answer = MessageBox.Show(Window, "Closing this window stops the server, and everyone playing will be disconnected.\n\nStop the server and close?", "Xonotic Server", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) { e.Cancel = true; return; }
        closing = true;
        StopServer();
    }

    // ---- Talking to the server ----

    // Sends each command line as its own rcon packet, in order, a moment apart.
    void Rcon(params string[] commands)
    {
        if (!IsRunning()) return;
        string password = rconPassword;
        Task.Run(() =>
        {
            try
            {
                using (var udp = new UdpClient())
                    foreach (string command in commands)
                    {
                        // out-of-band packet: four 0xFF bytes, then the text
                        byte[] packet = new byte[] { 0xff, 0xff, 0xff, 0xff }.Concat(Encoding.UTF8.GetBytes("rcon " + password + " " + command)).ToArray();
                        udp.Send(packet, packet.Length, "127.0.0.1", Port);
                        System.Threading.Thread.Sleep(200);
                    }
            }
            catch (Exception ex) { Log("Couldn't send the command to the server: " + ex.Message); }
        });
    }

    async Task PollStatus()
    {
        if (!IsRunning()) return;
        string reply = await Task.Run(() =>
        {
            try
            {
                using (var udp = new UdpClient())
                {
                    udp.Client.ReceiveTimeout = 1000;
                    udp.Connect("127.0.0.1", Port);
                    byte[] q = new byte[] { 0xff, 0xff, 0xff, 0xff }.Concat(Encoding.ASCII.GetBytes("getstatus panel")).ToArray();
                    udp.Send(q, q.Length);
                    IPEndPoint ep = null;
                    return Encoding.UTF8.GetString(udp.Receive(ref ep));
                }
            }
            catch { return null; }
        });
        if (reply != null && IsRunning()) SetStatus(true, reply);
    }

    void SetPill(string text, string background, string dot, string foreground)
    {
        statusText.Text = text;
        statusPill.Background = Hex(background);
        statusDot.Fill = Hex(dot);
        statusText.Foreground = Hex(foreground);
    }

    void SetStatus(bool running, string statusReply)
    {
        if (!running)
        {
            SetPill("Stopped", "#FFE5E3", "#FF3B30", "#C4271D");
            joinText.Text = "Start the server to see how players join.";
            addressText.Text = "—";
            nowPlayingText.Text = "Not running";
            nowPlayingDetail.Text = "";
            playersList.ItemsSource = null;
            playersTitle.Text = "PLAYERS";
            nobodyText.Visibility = Visibility.Visible;
            currentMap = "";
            return;
        }
        string[] lines = statusReply.Split('\n');
        var info = new Dictionary<string, string>();
        string[] kv = lines.Length > 1 ? lines[1].TrimStart('\\').Split('\\') : new string[0];
        for (int i = 0; i + 1 < kv.Length; i += 2) info[kv[i]] = kv[i + 1];

        currentMap = Get(info, "mapname", "");
        if (currentMap.StartsWith("_init")) return;  // still booting
        string modeName = ModeName(Get(info, "qcstatus", "").Split(':')[0]);
        SetPill("Running", "#E3F7E8", "#34C759", "#1E7B34");

        var rows = new List<PlayerRow>();
        int bots = 0;
        for (int i = 2; i < lines.Length; i++)
        {
            var m = Regex.Match(lines[i], "^(-?\\d+) (\\d+) \"(.*)\"");  // score ping "name"
            if (!m.Success) continue;
            string name = StripColors(m.Groups[3].Value).Trim();
            bool bot = m.Groups[2].Value == "0";
            if (bot) bots++;
            rows.Add(new PlayerRow
            {
                Name = name,
                Initial = name.Length > 0 ? name.Substring(0, 1).ToUpper() : "?",
                Detail = (bot ? "Bot" : "Ping " + m.Groups[2].Value + " ms") + "  ·  Score " + m.Groups[1].Value,
                Color = bot ? Hex("#C7C7CC") : AvatarColors[Math.Abs(name.GetHashCode()) % AvatarColors.Length],
            });
        }
        rows = rows.OrderBy(r => r.Detail.StartsWith("Bot")).ThenBy(r => r.Name).ToList();
        playersList.ItemsSource = rows;
        nobodyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        playersTitle.Text = "PLAYERS  ·  " + (rows.Count - bots) + (bots > 0 ? " + " + bots + " BOTS" : "");

        joinText.Text = "Players pick \"" + StripColors(Get(info, "hostname", "")) + "\" in the games list and click Join. If it isn't listed, they can type this address:";
        string address = string.Join("   ", LanAddresses().Select(a => a + ":" + Port));
        if (addressText.Text != address) { addressText.Text = address; copyButton.Content = "Copy"; }
        nowPlayingText.Text = modeName + "  ·  " + currentMap;
        nowPlayingDetail.Text = (rows.Count - bots) + " playing" + (bots > 0 ? ", " + bots + " bots" : "") + "  ·  up to " + Get(info, "sv_maxclients", "?") + " players";
    }

    static string StripColors(string s) { return Regex.Replace(s, @"\^(x[0-9a-fA-F]{3}|\d)", ""); }

    static string[] LanAddresses()
    {
        // Real network adapters only: ones with a default gateway (skips virtual Hyper-V/WSL/VPN switches).
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetIPProperties())
            .Where(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .SelectMany(p => p.UnicastAddresses)
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(a => a.Address.ToString())
            .ToArray();
    }

    void Log(string line)
    {
        if (!Window.Dispatcher.CheckAccess()) { Window.Dispatcher.BeginInvoke(new Action<string>(Log), line); return; }
        line = StripColors(line);
        if (line.Trim().Length == 0) return;
        if (logBox.Text.Length > 200000) logBox.Text = logBox.Text.Substring(100000);
        logBox.AppendText(line + Environment.NewLine);
        logBox.ScrollToEnd();
    }
}
