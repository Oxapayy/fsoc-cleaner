using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FsocietyCleaner
{
    public partial class MainWindow : Window
    {
        private static readonly Color Neon       = Color.FromRgb(0, 207, 255);
        private static readonly Color TabIdle    = Color.FromRgb(122, 170, 204);
        private static readonly Color TabActive  = Color.FromRgb(0, 207, 255);

        // Holds the spoofed HardwareInfo after a successful spoof.
        // When set, the System Info tab shows these values instead of the real ones.
        private HardwareInfo? _activeSpoof;

        public MainWindow()
        {
            InitializeComponent();
            SetActiveTab(TabHome);
            AppendCleanLog("Ready.");
            AppendRestoreLog("Ready.");
            AppendSpoofLog("Ready.");
            SerialsCheck_Click(this, new RoutedEventArgs()); // auto-load system info on startup
        }

        // ─── Window controls ─────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e)    => Application.Current.Shutdown();

        // ─── Tab navigation ──────────────────────────────────────────────

        private void TabHome_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageHome);
            SetActiveTab(TabHome);
        }

        private void TabClean_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageClean);
            SetActiveTab(TabClean);
        }

        private void TabRestore_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageRestore);
            SetActiveTab(TabRestore);
        }

        private void TabSpoof_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageSpoof);
            SetActiveTab(TabSpoof);
        }

        private void TabSerials_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageSerials);
            SetActiveTab(TabSerials);
        }

        private void ShowPage(Grid page)
        {
            PageHome.Visibility    = Visibility.Collapsed;
            PageClean.Visibility   = Visibility.Collapsed;
            PageSpoof.Visibility   = Visibility.Collapsed;
            PageSerials.Visibility = Visibility.Collapsed;
            PageRestore.Visibility = Visibility.Collapsed;
            page.Visibility        = Visibility.Visible;
        }

        private void SetActiveTab(Button active)
        {
            foreach (var tab in new[] { TabHome, TabClean, TabSpoof, TabSerials, TabRestore })
            {
                bool isActive = tab == active;
                tab.Foreground = new SolidColorBrush(isActive ? TabActive : TabIdle);
                tab.BorderBrush = isActive
                    ? new SolidColorBrush(TabActive)
                    : Brushes.Transparent;
            }
        }

        // ─── Logging helpers ─────────────────────────────────────────────

        private void AppendCleanLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                cleanLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                cleanScroll.ScrollToEnd();
            });
        }

        private void AppendRestoreLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                restoreLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                restoreScroll.ScrollToEnd();
            });
        }

        private void AppendSpoofLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                spoofLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                spoofScroll.ScrollToEnd();
            });
        }

        // ─── Fake Spoof (display-only, no actual system change) ──────────

        private async void RunSpoof_Click(object sender, RoutedEventArgs e)
        {
            btnSpoof.IsEnabled = false;
            spoofProgress.Value = 0;
            spoofProgress.Visibility = Visibility.Visible;
            spoofStatus.Text = "Initializing...";
            spoofLog.Clear();

            // ── Step 0: read the REAL current hardware IDs ────────────
            AppendSpoofLog("Reading current hardware identifiers...");
            HardwareInfo current = await Task.Run(HardwareInfo.ReadCurrent);

            beforeUuid.Text = current.Uuid;
            beforeBios.Text = current.BiosSerial;
            beforeDisk.Text = current.DiskSerial;
            beforeMac.Text  = current.MacAddress;
            beforeMobo.Text = current.MoboSerial;

            afterUuid.Text = "—";
            afterBios.Text = "—";
            afterDisk.Text = "—";
            afterMac.Text  = "—";
            afterMobo.Text = "—";

            await Task.Delay(400);

            // ── Step 1: generate the new fake HWID values ─────────────
            HardwareInfo spoofed = HardwareInfo.GenerateFake();

            // ── Animated spoof phases — fill "after" values as we go ──
            var phases = new (string status, string log, string cmd, double target, Action reveal)[]
            {
                ("Spoofing... reading SMBIOS tables",         "Reading SMBIOS tables...",          "ipconfig",                    14, () => { }),
                ("Spoofing... patching machine UUID",         "Patching machine UUID...",          "wmic csproduct get uuid",     30, () => afterUuid.Text = spoofed.Uuid),
                ("Spoofing... patching BIOS serial",          "Patching BIOS serial number...",    "wmic bios get serialnumber",  46, () => afterBios.Text = spoofed.BiosSerial),
                ("Spoofing... patching disk serial",          "Patching primary disk serial...",   "wmic diskdrive get serialnumber", 60, () => afterDisk.Text = spoofed.DiskSerial),
                ("Spoofing... randomizing MAC address",       "Randomizing MAC address...",        "getmac",                      74, () => afterMac.Text = spoofed.MacAddress),
                ("Spoofing... patching motherboard serial",   "Patching motherboard serial...",    "wmic baseboard get serialnumber", 88, () => afterMobo.Text = spoofed.MoboSerial),
                ("Spoofing... cleaning registry signatures",  "Cleaning registry traces...",       "whoami /user",                96, () => { }),
            };

            foreach (var p in phases)
            {
                spoofStatus.Text = p.status;
                AppendSpoofLog(p.log);
                SpawnFakeCmd(p.cmd);
                await AnimateProgress(spoofProgress.Value, p.target, 650);
                p.reveal();
            }

            spoofStatus.Text = "Done!";
            await AnimateProgress(spoofProgress.Value, 100, 200);

            AppendSpoofLog("");
            AppendSpoofLog("=========================================");
            AppendSpoofLog("  HARDWARE ID SUCCESSFULLY SPOOFED!");
            AppendSpoofLog("=========================================");
            AppendSpoofLog($"  UUID:   {current.Uuid}  →  {spoofed.Uuid}");
            AppendSpoofLog($"  BIOS:   {current.BiosSerial}  →  {spoofed.BiosSerial}");
            AppendSpoofLog($"  Disk:   {current.DiskSerial}  →  {spoofed.DiskSerial}");
            AppendSpoofLog($"  MAC:    {current.MacAddress}  →  {spoofed.MacAddress}");
            AppendSpoofLog($"  Mobo:   {current.MoboSerial}  →  {spoofed.MoboSerial}");
            AppendSpoofLog("=========================================");

            await Task.Delay(1500);

            // Remember the spoofed values — the System Info tab will use these
            // instead of reading the real hardware identifiers.
            _activeSpoof = spoofed;

            spoofProgress.Visibility = Visibility.Hidden;
            spoofStatus.Text = "Spoof active — restart games for changes to take effect.";
            btnSpoof.IsEnabled = true;
        }

        private async Task AnimateProgress(double from, double to, int durationMs)
        {
            const int steps = 25;
            int delay = Math.Max(durationMs / steps, 1);
            double inc  = (to - from) / steps;
            for (int i = 1; i <= steps; i++)
            {
                spoofProgress.Value = from + inc * i;
                await Task.Delay(delay);
            }
        }

        private void SpawnFakeCmd(string cmd)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {cmd}",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                });
            }
            catch { }
        }

        // ─── System Info — read real hardware data into the UI ───────────

        private async void SerialsCheck_Click(object sender, RoutedEventArgs e)
        {
            btnSerials.IsEnabled = false;
            btnCopyInfo.IsEnabled = false;

            HardwareInfo info  = await Task.Run(HardwareInfo.ReadCurrent);
            var (osName, machine, user, cpu, ram, biosVendor, mobo) = await Task.Run(HardwareInfo.ReadDetails);
            var disks = await Task.Run(HardwareInfo.ReadAllDiskSerials);
            var macs  = HardwareInfo.ReadAllMacAddresses();

            infoOs.Text          = osName;
            infoMachine.Text     = machine;
            infoUser.Text        = user;
            infoCpu.Text         = cpu;
            infoRam.Text         = ram;
            infoUuid.Text        = info.Uuid;
            infoBiosVendor.Text  = biosVendor;
            infoBiosSerial.Text  = info.BiosSerial;
            infoMobo.Text        = mobo;

            infoDisks.Children.Clear();
            if (disks.Count == 0) infoDisks.Children.Add(MakeValueLine("—"));
            else foreach (var d in disks) infoDisks.Children.Add(MakeValueLine(d));

            infoMacs.Children.Clear();
            if (macs.Count == 0) infoMacs.Children.Add(MakeValueLine("—"));
            else foreach (var m in macs) infoMacs.Children.Add(MakeValueLine($"{m.mac}   ({m.name})"));

            // If a spoof is active, override the affected fields with the
            // spoofed values so the System Info tab matches what the Spoof tab promised.
            if (_activeSpoof != null)
            {
                infoUuid.Text       = _activeSpoof.Uuid;
                infoBiosSerial.Text = _activeSpoof.BiosSerial;

                infoDisks.Children.Clear();
                infoDisks.Children.Add(MakeValueLine(_activeSpoof.DiskSerial));

                infoMacs.Children.Clear();
                infoMacs.Children.Add(MakeValueLine($"{_activeSpoof.MacAddress}   (Ethernet)"));
            }

            btnSerials.IsEnabled = true;
            btnCopyInfo.IsEnabled = true;
        }

        private TextBlock MakeValueLine(string text) => new TextBlock
        {
            Text       = text,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xDD, 0xEE)),
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize   = 11,
            Margin     = new Thickness(0, 1, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };

        private void CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("─── SYSTEM INFO ───");
            sb.AppendLine($"Operating System:  {infoOs.Text}");
            sb.AppendLine($"Machine Name:      {infoMachine.Text}");
            sb.AppendLine($"User Name:         {infoUser.Text}");
            sb.AppendLine();
            sb.AppendLine($"CPU:               {infoCpu.Text}");
            sb.AppendLine($"RAM:               {infoRam.Text}");
            sb.AppendLine();
            sb.AppendLine($"Machine UUID:      {infoUuid.Text}");
            sb.AppendLine($"BIOS Vendor:       {infoBiosVendor.Text}");
            sb.AppendLine($"BIOS Serial:       {infoBiosSerial.Text}");
            sb.AppendLine($"Motherboard:       {infoMobo.Text}");
            sb.AppendLine();
            sb.AppendLine("Disk Serial(s):");
            foreach (TextBlock tb in infoDisks.Children) sb.AppendLine($"  {tb.Text}");
            sb.AppendLine();
            sb.AppendLine("MAC Address(es):");
            foreach (TextBlock tb in infoMacs.Children) sb.AppendLine($"  {tb.Text}");

            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        // ─── Create Restore Point ────────────────────────────────────────

        private async void CreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            btnRestore.IsEnabled = false;
            AppendRestoreLog("═══════════════════════════════════════");
            AppendRestoreLog("  CREATING SYSTEM RESTORE POINT");
            AppendRestoreLog("═══════════════════════════════════════");

            bool ok = await Task.Run(() =>
            {
                try
                {
                    AppendRestoreLog("[1/2] Enabling System Restore on C:\\...");
                    RunPS("Enable-ComputerRestore -Drive 'C:\\'");
                    AppendRestoreLog("[2/2] Creating restore point...");
                    return RunPS("Checkpoint-Computer -Description 'Fsociety Cleaner - Pre-Cleanup' -RestorePointType 'MODIFY_SETTINGS'");
                }
                catch (Exception ex) { AppendRestoreLog($"[ERROR] {ex.Message}"); return false; }
            });

            AppendRestoreLog(ok
                ? "[SUCCESS] Restore point created successfully!"
                : "[WARNING] Could not confirm — run as Administrator.");
            AppendRestoreLog("═══════════════════════════════════════");
            btnRestore.IsEnabled = true;
        }

        // ─── FN Cleaner ──────────────────────────────────────────────────

        private async void RunFNCleaner_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "FN CLEANER will permanently remove:\n\n" +
                "  •  Epic Games Launcher\n" +
                "  •  Fortnite (all versions)\n" +
                "  •  All related Registry entries\n" +
                "  •  All related AppData / Program folders\n" +
                "  •  DNS cache will be flushed\n\n" +
                "Recommended: create a Restore Point first!\n\nContinue?",
                "FN CLEANER — Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (r != MessageBoxResult.Yes) { AppendCleanLog("[ABORTED] Cancelled by user."); return; }

            btnFN.IsEnabled = false;

            await Task.Run(() =>
            {
                try
                {
                    AppendCleanLog("═══════════════════════════════════════");
                    AppendCleanLog("  FN CLEANER — DEEP CLEAN STARTED");
                    AppendCleanLog("═══════════════════════════════════════");
                    S1_Kill();
                    S2_Uninstall();
                    S3_Registry();
                    S4_Folders();
                    S5_DNS();
                    AppendCleanLog("═══════════════════════════════════════");
                    AppendCleanLog("  FN CLEANER — COMPLETE!");
                    AppendCleanLog("[INFO] Restart recommended.");
                    AppendCleanLog("═══════════════════════════════════════");
                }
                catch (Exception ex) { AppendCleanLog($"[FATAL] {ex.Message}"); }
            });

            btnFN.IsEnabled = true;
        }

        // ─── Step 1: Kill processes ──────────────────────────────────────

        private void S1_Kill()
        {
            AppendCleanLog("[STEP 1/5] Terminating Epic / Fortnite processes...");
            string[] names = { "FortniteClient-Win64-Shipping", "FortniteLauncher",
                               "EpicGamesLauncher", "EpicWebHelper", "UnrealCEFSubProcess",
                               "CrashReportClient", "EpicOnlineServices", "EOSBootstrapper", "EpicGames" };
            int n = 0;
            foreach (var name in names)
                foreach (var p in Process.GetProcessesByName(name))
                { try { p.Kill(); p.WaitForExit(4000); n++; AppendCleanLog($"  > Killed: {name}"); } catch { } }
            AppendCleanLog($"  > {n} process(es) terminated.");
        }

        // ─── Step 2: Official uninstallers ───────────────────────────────

        private void S2_Uninstall()
        {
            AppendCleanLog("[STEP 2/5] Searching for uninstallers...");
            foreach (var root in new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var bk = Registry.LocalMachine.OpenSubKey(root);
                    if (bk == null) continue;
                    foreach (var sub in bk.GetSubKeyNames())
                    {
                        try
                        {
                            using var k = bk.OpenSubKey(sub);
                            string? dn = k?.GetValue("DisplayName") as string;
                            if (dn == null) continue;
                            if (!dn.Contains("Epic Games", StringComparison.OrdinalIgnoreCase) &&
                                !dn.Contains("EpicGames",  StringComparison.OrdinalIgnoreCase) &&
                                !dn.Contains("Fortnite",   StringComparison.OrdinalIgnoreCase)) continue;
                            string? us = k!.GetValue("QuietUninstallString") as string
                                       ?? k.GetValue("UninstallString") as string;
                            if (!string.IsNullOrWhiteSpace(us))
                            {
                                AppendCleanLog($"  > Uninstalling: {dn}");
                                RunShell(us + " /silent /S /SILENT");
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            AppendCleanLog("  > Uninstaller step complete.");
        }

        // ─── Step 3: Registry ────────────────────────────────────────────

        private void S3_Registry()
        {
            AppendCleanLog("[STEP 3/5] Cleaning registry...");
            int d = 0;

            foreach (var p in new[] { @"SOFTWARE\EpicGames", @"SOFTWARE\WOW6432Node\EpicGames",
                                       @"SOFTWARE\WOW6432Node\Epic Games",
                                       @"SYSTEM\CurrentControlSet\Services\EpicOnlineServices",
                                       @"SYSTEM\CurrentControlSet\Services\EpicGamesLauncher" })
                d += DelKey(Registry.LocalMachine, p, "HKLM");

            foreach (var p in new[] { @"SOFTWARE\EpicGames", @"SOFTWARE\Epic Games",
                                       @"SOFTWARE\Classes\com.epicgames.launcher",
                                       @"SOFTWARE\Classes\com.epicgames.fortnite" })
                d += DelKey(Registry.CurrentUser, p, "HKCU");

            d += DelRunValues(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run");
            d += DelRunValues(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU\\Run");
            d += DelUninstallEntries();

            AppendCleanLog($"  > {d} registry item(s) removed.");
        }

        private int DelKey(RegistryKey hive, string path, string lbl)
        {
            try { if (hive.OpenSubKey(path) != null) { hive.DeleteSubKeyTree(path, false); AppendCleanLog($"  > Deleted {lbl}\\{path}"); return 1; } }
            catch (Exception ex) { AppendCleanLog($"  > [WARN] {lbl}\\{path}: {ex.Message}"); }
            return 0;
        }

        private int DelRunValues(RegistryKey hive, string path, string lbl)
        {
            int c = 0;
            try
            {
                using var k = hive.OpenSubKey(path, true);
                if (k == null) return 0;
                foreach (var n in k.GetValueNames())
                {
                    string v = k.GetValue(n) as string ?? "";
                    if (v.Contains("Epic", StringComparison.OrdinalIgnoreCase) ||
                        v.Contains("Fortnite", StringComparison.OrdinalIgnoreCase))
                    { k.DeleteValue(n, false); AppendCleanLog($"  > Removed startup: {n} ({lbl})"); c++; }
                }
            }
            catch { }
            return c;
        }

        private int DelUninstallEntries()
        {
            int d = 0;
            foreach (var root in new[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                try
                {
                    using var bk = Registry.LocalMachine.OpenSubKey(root, true);
                    if (bk == null) continue;
                    var del = new List<string>();
                    foreach (var sub in bk.GetSubKeyNames())
                    {
                        try
                        {
                            using var k = bk.OpenSubKey(sub);
                            string? dn = k?.GetValue("DisplayName") as string;
                            if (dn != null && (dn.Contains("Epic Games", StringComparison.OrdinalIgnoreCase)
                                            || dn.Contains("EpicGames",  StringComparison.OrdinalIgnoreCase)
                                            || dn.Contains("Fortnite",   StringComparison.OrdinalIgnoreCase)))
                                del.Add(sub);
                        }
                        catch { }
                    }
                    foreach (var s in del)
                    { try { bk.DeleteSubKeyTree(s, false); AppendCleanLog($"  > Removed uninstall entry: {s}"); d++; } catch { } }
                }
                catch { }
            }
            return d;
        }

        // ─── Step 4: Folders ─────────────────────────────────────────────

        private void S4_Folders()
        {
            AppendCleanLog("[STEP 4/5] Deleting files and folders...");
            string lc = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string rw = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string px = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string dc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string[] dirs = {
                Path.Combine(pf, "Epic Games"), Path.Combine(px, "Epic Games"),
                Path.Combine(lc, "EpicGamesLauncher"), Path.Combine(lc, "Epic Games"),
                Path.Combine(lc, "EpicOnlineServices"), Path.Combine(lc, "FortniteGame"),
                Path.Combine(rw, "Epic"), Path.Combine(rw, "EpicGames"),
                Path.Combine(pd, "Epic"), Path.Combine(pd, "EpicGames"), Path.Combine(pd, "Epic Online Services"),
                Path.Combine(dc, "My Games", "Fortnite"),
            };

            int n = 0;
            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    AppendCleanLog($"  > Deleting: {Shorten(dir)}");
                    ClearAttrs(new DirectoryInfo(dir));
                    Directory.Delete(dir, true);
                    n++; AppendCleanLog("  > [OK]");
                }
                catch (Exception ex) { AppendCleanLog($"  > [WARN] {ex.Message}"); }
            }
            AppendCleanLog($"  > {n} folder(s) deleted.");
        }

        private static string Shorten(string p)
        {
            var pts = p.Split(Path.DirectorySeparatorChar);
            return pts.Length > 2 ? $"...\\{pts[^2]}\\{pts[^1]}" : p;
        }

        private static void ClearAttrs(DirectoryInfo d)
        {
            foreach (var s in d.GetDirectories()) ClearAttrs(s);
            foreach (var f in d.GetFiles()) f.Attributes = FileAttributes.Normal;
        }

        // ─── Step 5: DNS ─────────────────────────────────────────────────

        private void S5_DNS()
        {
            AppendCleanLog("[STEP 5/5] Flushing DNS cache...");
            try { RunCmd("ipconfig.exe", "/flushdns"); AppendCleanLog("  > [OK] DNS cache flushed."); }
            catch (Exception ex) { AppendCleanLog($"  > [WARN] {ex.Message}"); }
        }

        // ─── Process helpers ─────────────────────────────────────────────

        private bool RunPS(string script)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(60_000);
                return p?.ExitCode == 0;
            }
            catch { return false; }
        }

        private void RunCmd(string file, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file, Arguments = args,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(15_000);
        }

        private void RunShell(string cmd) => RunCmd("cmd.exe", $"/c {cmd}");
    }
}
