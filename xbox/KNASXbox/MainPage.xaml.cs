using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Windows.Gaming.Input;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace KNASXbox
{
    /// <summary>
    /// KNAS 42 for Xbox: the cable box full screen, with the controller working as the remote.
    /// A = OK, B = back, LB/RB = channel down/up, D-pad = arrows, View = guide, Y = info,
    /// X = last channel, LT/RT = volume, right stick click = mute, Menu = this app's settings.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const int Port = 8642;
        readonly ApplicationDataContainer prefs = ApplicationData.Current.LocalSettings;
        readonly DispatcherTimer pad = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        GamepadButtons last = GamepadButtons.None;
        GamepadButtons repeatButton = GamepadButtons.None;
        DateTime repeatAt = DateTime.MaxValue;
        bool webReady, finding, dialogOpen;
        string current = "";

        public MainPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            // B is handled below; never let it close the app by itself
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, e) => e.Handled = true;
        }

        string Saved
        {
            get { return prefs.Values["address"] as string ?? ""; }
            set { prefs.Values["address"] = value; }
        }

        async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Web.EnsureCoreWebView2Async();
                var core = Web.CoreWebView2;
                core.Settings.UserAgent = core.Settings.UserAgent + " KNASApp/xbox";   // the cable box skips its own full-screen request
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.NewWindowRequested += (s, a) => a.Handled = true;
                core.NavigationCompleted += (s, a) =>
                {
                    if (!a.IsSuccess && Web.Visibility == Visibility.Visible)
                        ShowSetup("Can't reach KNAS", "Couldn't open " + current + ". Check that KNAS is running, or fix the address.");
                    else KeepFocus();
                };
                webReady = true;
            }
            catch (Exception ex)
            {
                ShowSetup("Something's missing", "The Xbox couldn't start its web view (" + ex.Message + "). Try updating the Xbox, then open KNAS again.");
                return;
            }
            Web.GotFocus += (s, a) => KeepFocus();
            pad.Tick += Poll;
            pad.Start();

            if (Saved == "")
            {
                ShowSetup("Where's KNAS?", "Looking for KNAS on your home network…");
                await Find();
            }
            else await Open(Saved);
        }

        void KeepFocus()
        {
            if (Web.Visibility == Visibility.Visible && Setup.Visibility != Visibility.Visible && Menu.Visibility != Visibility.Visible)
                Sink.Focus(FocusState.Programmatic);
        }

        // ------------------------------------------------------------ address

        static string Clean(string a)
        {
            a = (a ?? "").Trim();
            if (a.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) a = a.Substring(7);
            if (a.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) a = a.Substring(8);
            int slash = a.IndexOf('/');
            if (slash >= 0) a = a.Substring(0, slash);
            if (a != "" && !a.Contains(":")) a += ":" + Port;
            return a;
        }

        static async Task<bool> IsKnas(string hostPort, int ms)
        {
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(ms) })
                {
                    var text = await http.GetStringAsync("http://" + hostPort + "/api/version");
                    return text.Contains("\"settings_file\"");
                }
            }
            catch { return false; }
        }

        async Task Open(string address)
        {
            var hp = Clean(address);
            if (hp == "") { ShowSetup("Where's KNAS?", "Type your NAS's address, like 192.168.1.50."); return; }
            AddressBox.Text = hp.EndsWith(":" + Port) ? hp.Substring(0, hp.Length - Port.ToString().Length - 1) : hp;
            if (!await IsKnas(hp, 5000))
            {
                ShowSetup("Can't reach KNAS", "Nothing answered at " + hp + ". Check that KNAS is running and the address is right.");
                return;
            }
            Saved = hp;
            current = hp;
            Setup.Visibility = Visibility.Collapsed;
            Menu.Visibility = Visibility.Collapsed;
            Web.Visibility = Visibility.Visible;
            if (webReady) Web.CoreWebView2.Navigate("http://" + hp + "/tv");
            KeepFocus();
        }

        async Task Find()
        {
            if (finding) return;
            finding = true;
            FindBtn.IsEnabled = false;
            try
            {
                var bases = NetworkInformation.GetHostNames()
                    .Where(h => h.Type == HostNameType.Ipv4 && h.IPInformation != null)
                    .Select(h => h.CanonicalName)
                    .Where(ip => !ip.StartsWith("169.254.") && !ip.StartsWith("127."))
                    .Select(ip => ip.Substring(0, ip.LastIndexOf('.') + 1))
                    .Distinct().ToList();
                SetupMsg.Text = bases.Count > 0 ? "Looking for KNAS on your home network… (about 15 seconds)" : "This Xbox isn't on a network. Check its network settings.";
                string found = null;
                using (var gate = new SemaphoreSlim(48))
                using (var stop = new CancellationTokenSource())
                {
                    var jobs = new List<Task>();
                    foreach (var b in bases)
                        for (int i = 1; i < 255; i++)
                        {
                            var hp = b + i + ":" + Port;
                            jobs.Add(Task.Run(async () =>
                            {
                                await gate.WaitAsync();
                                try
                                {
                                    if (found == null && !stop.IsCancellationRequested && await IsKnas(hp, 1500)) { found = found ?? hp; stop.Cancel(); }
                                }
                                finally { gate.Release(); }
                            }));
                        }
                    await Task.WhenAll(jobs);
                }
                if (found != null) await Open(found);
                else ShowSetup("Couldn't find KNAS", "Type your NAS's address instead (it's in your NAS's network settings, like 192.168.1.50).");
            }
            finally
            {
                finding = false;
                FindBtn.IsEnabled = true;
            }
        }

        void ShowSetup(string title, string msg)
        {
            SetupTitle.Text = title;
            SetupMsg.Text = msg;
            Menu.Visibility = Visibility.Collapsed;
            Setup.Visibility = Visibility.Visible;
            if (AddressBox.Text == "" && Saved != "") AddressBox.Text = Saved.Replace(":" + Port, "");
            ConnectBtn.Focus(FocusState.Programmatic);
        }

        async void Connect_Click(object sender, RoutedEventArgs e) { await Open(AddressBox.Text); }
        async void Find_Click(object sender, RoutedEventArgs e) { SetupTitle.Text = "Finding KNAS…"; await Find(); }

        // ------------------------------------------------------------ menu

        void ShowMenu()
        {
            MenuWhere.Text = current == "" ? "" : "Connected to " + current;
            Menu.Visibility = Visibility.Visible;
            MenuBack.Focus(FocusState.Programmatic);
        }

        void HideMenu()
        {
            Menu.Visibility = Visibility.Collapsed;
            KeepFocus();
        }

        void MenuBack_Click(object sender, RoutedEventArgs e) { HideMenu(); }
        void Reload_Click(object sender, RoutedEventArgs e) { HideMenu(); if (webReady && current != "") Web.CoreWebView2.Navigate("http://" + current + "/tv"); }
        void ChangeAddress_Click(object sender, RoutedEventArgs e) { ShowSetup("Change address", "Type your NAS's address, or let the app look for it."); }
        void Exit_Click(object sender, RoutedEventArgs e) { Application.Current.Exit(); }

        // ------------------------------------------------------------ controller → remote

        async Task<bool> Send(string key)
        {
            if (!webReady) return false;
            var js = "(function(){try{var e=new KeyboardEvent('keydown',{key:'" + key + "',bubbles:true,cancelable:true});" +
                     "return document.dispatchEvent(e)?'0':'1'}catch(x){return '0'}})()";
            try { return (await Web.CoreWebView2.ExecuteScriptAsync(js)).Contains("1"); }
            catch { return false; }
        }

        async void Back()
        {
            if (await Send("GoBack") || dialogOpen) return;     // KNAS closed something (the guide, a banner...)
            dialogOpen = true;
            var d = new ContentDialog
            {
                Title = "Leave KNAS?",
                PrimaryButtonText = "Exit",
                SecondaryButtonText = "Settings",
                CloseButtonText = "Stay",
                DefaultButton = ContentDialogButton.Close
            };
            var r = await d.ShowAsync();
            dialogOpen = false;
            if (r == ContentDialogResult.Primary) Application.Current.Exit();
            else if (r == ContentDialogResult.Secondary) ShowMenu();
            else KeepFocus();
        }

        static readonly Dictionary<GamepadButtons, string> Keys = new Dictionary<GamepadButtons, string>
        {
            { GamepadButtons.DPadUp, "ArrowUp" }, { GamepadButtons.DPadDown, "ArrowDown" },
            { GamepadButtons.DPadLeft, "ArrowLeft" }, { GamepadButtons.DPadRight, "ArrowRight" },
            { GamepadButtons.A, "Enter" }, { GamepadButtons.RightShoulder, "ChannelUp" }, { GamepadButtons.LeftShoulder, "ChannelDown" },
            { GamepadButtons.View, "Guide" }, { GamepadButtons.Y, "Info" }, { GamepadButtons.X, "ArrowLeft" },
            { GamepadButtons.RightThumbstick, "m" },
        };
        const GamepadButtons Repeats = GamepadButtons.DPadUp | GamepadButtons.DPadDown | GamepadButtons.DPadLeft | GamepadButtons.DPadRight |
                                       GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder;
        // triggers and the left stick act like buttons too
        const GamepadButtons VolUp = (GamepadButtons)(1 << 20), VolDown = (GamepadButtons)(1 << 21);

        void Poll(object sender, object e)
        {
            var g = Gamepad.Gamepads.FirstOrDefault();
            if (g == null) { last = GamepadButtons.None; return; }
            var r = g.GetCurrentReading();
            var now = r.Buttons;
            if (r.LeftThumbstickY > 0.6) now |= GamepadButtons.DPadUp;
            if (r.LeftThumbstickY < -0.6) now |= GamepadButtons.DPadDown;
            if (r.LeftThumbstickX < -0.6) now |= GamepadButtons.DPadLeft;
            if (r.LeftThumbstickX > 0.6) now |= GamepadButtons.DPadRight;
            if (r.RightTrigger > 0.5) now |= VolUp;
            if (r.LeftTrigger > 0.5) now |= VolDown;
            var pressed = now & ~last;
            last = now;

            // the Menu button always opens or closes this app's menu
            if ((pressed & GamepadButtons.Menu) != 0)
            {
                if (Menu.Visibility == Visibility.Visible) HideMenu();
                else if (Setup.Visibility != Visibility.Visible && !dialogOpen) ShowMenu();
                return;
            }
            if (Menu.Visibility == Visibility.Visible)
            {
                if ((pressed & GamepadButtons.B) != 0) HideMenu();
                return;                                       // the menu's buttons work by themselves
            }
            if (Setup.Visibility == Visibility.Visible)
            {
                if ((pressed & GamepadButtons.B) != 0 && current != "" && FocusManager.GetFocusedElement() != AddressBox)
                {
                    Setup.Visibility = Visibility.Collapsed;
                    KeepFocus();
                }
                return;
            }
            if (dialogOpen || Web.Visibility != Visibility.Visible) return;

            if ((pressed & GamepadButtons.B) != 0) { Back(); return; }
            if ((pressed & VolUp) != 0) { _ = Send("+"); repeatButton = VolUp; repeatAt = DateTime.Now.AddMilliseconds(350); }
            if ((pressed & VolDown) != 0) { _ = Send("-"); repeatButton = VolDown; repeatAt = DateTime.Now.AddMilliseconds(350); }
            foreach (var kv in Keys)
            {
                if ((pressed & kv.Key) == 0) continue;
                _ = Send(kv.Value);
                if ((kv.Key & Repeats) != 0) { repeatButton = kv.Key; repeatAt = DateTime.Now.AddMilliseconds(400); }
            }
            // holding a direction, a shoulder or a trigger keeps going
            if (repeatButton != GamepadButtons.None)
            {
                if ((now & repeatButton) == 0) repeatButton = GamepadButtons.None;
                else if (DateTime.Now >= repeatAt && pressed == GamepadButtons.None)
                {
                    var key = repeatButton == VolUp ? "+" : repeatButton == VolDown ? "-" : Keys[repeatButton];
                    _ = Send(key);
                    repeatAt = DateTime.Now.AddMilliseconds(repeatButton == VolUp || repeatButton == VolDown ? 120 : 140);
                }
            }
        }
    }
}
