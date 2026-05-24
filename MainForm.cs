using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FsocietyCleaner
{
    // ─── Custom neon button ────────────────────────────────────────────────────

    sealed class NeonButton : Control
    {
        public string SubText { get; set; } = string.Empty;

        private static readonly Color Neon = Color.FromArgb(0, 207, 255);
        private bool _hovered;
        private bool _pressed;

        public NeonButton()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Height = 86;
            SetStyle(ControlStyles.Selectable, false);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            Color bg = !Enabled    ? Color.FromArgb(4,  4,  12)
                     : _pressed    ? Color.FromArgb(0,  13, 20)
                     : _hovered    ? Color.FromArgb(8,  24, 37)
                     :               Color.FromArgb(4,  4,  15);
            using (var br = new SolidBrush(bg))
                g.FillRectangle(br, ClientRectangle);

            // Glow halo layers (only when enabled)
            if (Enabled)
            {
                int[] alphas = { 6, 12, 22, 45 };
                for (int i = alphas.Length - 1; i >= 0; i--)
                {
                    int off = alphas.Length - 1 - i;
                    using var gp = new Pen(Color.FromArgb(alphas[i], Neon), 1f);
                    g.DrawRectangle(gp, off, off, Width - 1 - off * 2, Height - 1 - off * 2);
                }
            }

            // Main 1.5 px border
            Color borderColor = Enabled ? Neon : Color.FromArgb(35, Neon);
            using (var bp = new Pen(borderColor, 1.5f))
                g.DrawRectangle(bp, 1, 1, Width - 3, Height - 3);

            if (!Enabled) { g.Dispose(); return; }

            // Main text
            using var mf = new Font("Consolas", 13f, FontStyle.Bold);
            using var mb = new SolidBrush(Neon);
            var ms = g.MeasureString(Text, mf);
            bool hasSub = !string.IsNullOrEmpty(SubText);
            float my = hasSub ? Height / 2f - ms.Height - 1f : (Height - ms.Height) / 2f;
            g.DrawString(Text, mf, mb, (Width - ms.Width) / 2f, my);

            // Sub text
            if (hasSub)
            {
                using var sf = new Font("Consolas", 10f);
                using var sb2 = new SolidBrush(Color.FromArgb(0, 80, 110));
                var ss = g.MeasureString(SubText, sf);
                g.DrawString(SubText, sf, sb2, (Width - ss.Width) / 2f, Height / 2f + 5f);
            }
        }
    }

    // ─── Main form ─────────────────────────────────────────────────────────────

    sealed class MainForm : Form
    {
        private static readonly Color Neon     = Color.FromArgb(0, 207, 255);
        private static readonly Color BabyBlue = Color.FromArgb(137, 207, 240);
        private static readonly Color Dark      = Color.FromArgb(5, 5, 16);
        private static readonly Color TitleBg   = Color.FromArgb(7, 7, 26);

        private Panel      _titleBar    = null!;
        private NeonButton _btnRestore  = null!;
        private NeonButton _btnFN       = null!;
        private Panel      _progressStrip = null!;
        private RichTextBox _log        = null!;
        private Label      _statusDot   = null!;
        private Label      _statusText  = null!;

        private Point _dragOffset;
        private float _scanOffset;

        public MainForm()
        {
            Build();
            AppendLog("[FSOCIETY CLEANER] System ready. Awaiting orders...");
            AppendLog("[INFO] Run as Administrator for full functionality.");
            AppendLog("[INFO] Tip: Create a Restore Point before running FN Cleaner.");
        }

        // ─── UI construction ──────────────────────────────────────────────────

        void Build()
        {
            SuspendLayout();

            Text            = "Fsociety Cleaner";
            ClientSize      = new Size(860, 680);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Dark;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered  = true;
            Font            = new Font("Consolas", 9f);
            Paint          += OnFormPaint;

            // ── Title bar ─────────────────────────────────────────────────────
            _titleBar = new Panel
            {
                Bounds    = new Rectangle(1, 1, ClientSize.Width - 2, 43),
                BackColor = TitleBg,
            };
            _titleBar.Paint      += (_, e) => {
                using var p = new Pen(Color.FromArgb(20, Neon), 1);
                e.Graphics.DrawLine(p, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
            };
            _titleBar.MouseDown  += (_, e) => { if (e.Button == MouseButtons.Left) _dragOffset = new Point(e.X + 1, e.Y + 1); };
            _titleBar.MouseMove  += (_, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    var sp = _titleBar.PointToScreen(e.Location);
                    Location = new Point(sp.X - _dragOffset.X, sp.Y - _dragOffset.Y);
                }
            };

            var lblBarTitle = new Label
            {
                Text      = "[ FSOCIETY CLEANER ]",
                ForeColor = Neon,
                Font      = new Font("Consolas", 13f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(14, 9),
                BackColor = Color.Transparent,
            };
            _titleBar.Controls.Add(lblBarTitle);
            _titleBar.Controls.Add(WinCtrl("─", Neon,                  ClientSize.Width - 76, (_, __) => WindowState = FormWindowState.Minimized));
            _titleBar.Controls.Add(WinCtrl("✕", Color.FromArgb(255,68,68), ClientSize.Width - 38, (_, __) => Application.Exit()));

            Controls.Add(_titleBar);

            // ── Header ────────────────────────────────────────────────────────
            int y = 58;
            var szHeader = TextRenderer.MeasureText("FSOCIETY CLEANER", new Font("Consolas", 28f, FontStyle.Bold));
            Controls.Add(new Label
            {
                Text      = "FSOCIETY CLEANER",
                ForeColor = BabyBlue,
                Font      = new Font("Consolas", 28f, FontStyle.Bold),
                BackColor = Color.Transparent,
                Size      = szHeader,
                Location  = new Point((ClientSize.Width - szHeader.Width) / 2, y),
            });

            y += szHeader.Height + 2;
            var szSub = TextRenderer.MeasureText("// Fortnite & Epic Games Deep Clean Utility //", new Font("Consolas", 10f));
            Controls.Add(new Label
            {
                Text      = "// Fortnite & Epic Games Deep Clean Utility //",
                ForeColor = Color.FromArgb(0, 60, 80),
                Font      = new Font("Consolas", 10f),
                BackColor = Color.Transparent,
                Size      = szSub,
                Location  = new Point((ClientSize.Width - szSub.Width) / 2, y),
            });

            y += szSub.Height + 10;
            Controls.Add(new Panel { BackColor = Color.FromArgb(40, Neon), Bounds = new Rectangle(22, y, ClientSize.Width - 44, 1) });

            // ── Action buttons ────────────────────────────────────────────────
            y += 14;
            int btnW = (ClientSize.Width - 44 - 18) / 2;

            _btnRestore = new NeonButton { Text = "◈  CREATE RESTORE POINT", SubText = "Backup system state before cleaning",         Bounds = new Rectangle(22, y, btnW, 86) };
            _btnFN      = new NeonButton { Text = "⚡  FN CLEANER",           SubText = "Remove Epic Games & Fortnite completely",    Bounds = new Rectangle(22 + btnW + 18, y, btnW, 86) };

            _btnRestore.Click += BtnRestore_Click;
            _btnFN.Click      += BtnFN_Click;
            Controls.Add(_btnRestore);
            Controls.Add(_btnFN);

            // ── Progress strip ────────────────────────────────────────────────
            y += 86 + 10;
            _progressStrip = new Panel { Bounds = new Rectangle(22, y, ClientSize.Width - 44, 5), Visible = false, BackColor = Color.FromArgb(2, 2, 14) };
            _progressStrip.Paint += ProgressStrip_Paint;
            Controls.Add(_progressStrip);

            var scanTimer = new System.Windows.Forms.Timer { Interval = 25 };
            scanTimer.Tick += (_, __) => { if (_progressStrip.Visible) _progressStrip.Invalidate(); };
            scanTimer.Start();

            // ── Log panel ─────────────────────────────────────────────────────
            y += 5 + 8;
            int logH = ClientSize.Height - y - 34;

            var logWrap = new Panel { Bounds = new Rectangle(22, y, ClientSize.Width - 44, logH), BackColor = Color.FromArgb(2, 2, 14) };
            logWrap.Paint += (_, e) => { using var p = new Pen(Color.FromArgb(0, 24, 38), 1); e.Graphics.DrawRectangle(p, 0, 0, logWrap.Width - 1, logWrap.Height - 1); };

            var logHdr = new Label { Text = "[ SYSTEM LOG ]", ForeColor = Color.FromArgb(0, 70, 90), Font = new Font("Consolas", 10f), Bounds = new Rectangle(1, 1, logWrap.Width - 2, 22), BackColor = Color.FromArgb(4, 4, 20) };
            logWrap.Controls.Add(logHdr);

            _log = new RichTextBox
            {
                Bounds      = new Rectangle(1, 24, logWrap.Width - 2, logH - 25),
                BackColor   = Color.FromArgb(2, 2, 14),
                ForeColor   = Neon,
                Font        = new Font("Consolas", 10f),
                ReadOnly    = true,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
            };
            logWrap.Controls.Add(_log);
            Controls.Add(logWrap);

            // ── Status bar ────────────────────────────────────────────────────
            int sy = ClientSize.Height - 24;

            _statusDot = new Label { Text = "●", ForeColor = Neon, Font = new Font("Consolas", 10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(22, sy) };
            _statusText = new Label { Text = "STATUS: IDLE — Awaiting orders", ForeColor = Color.FromArgb(0, 55, 75), Font = new Font("Consolas", 10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(38, sy) };

            var szVer = TextRenderer.MeasureText("by Fsociety", new Font("Consolas", 10f));
            var lblVer = new Label { Text = "by Fsociety", ForeColor = Color.FromArgb(0, 30, 45), Font = new Font("Consolas", 10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(ClientSize.Width - 22 - szVer.Width, sy) };

            Controls.Add(_statusDot);
            Controls.Add(_statusText);
            Controls.Add(lblVer);

            ResumeLayout();
        }

        static Button WinCtrl(string text, Color fg, int x, EventHandler click)
        {
            var b = new Button { Text = text, ForeColor = fg, BackColor = Color.Transparent, FlatStyle = FlatStyle.Flat, Size = new Size(36, 36), Font = new Font("Consolas", 12f), Cursor = Cursors.Hand, Location = new Point(x, 4) };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(13, 26, 38);
            b.Click += click;
            return b;
        }

        // ─── Paint ────────────────────────────────────────────────────────────

        void OnFormPaint(object? sender, PaintEventArgs e)
        {
            using var p = new Pen(Neon, 1f);
            e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            int[] a = { 25, 14, 7, 3 };
            for (int i = 0; i < a.Length; i++)
            {
                using var gp = new Pen(Color.FromArgb(a[i], Neon), 1);
                e.Graphics.DrawRectangle(gp, i + 1, i + 1, Width - (i + 1) * 2 - 1, Height - (i + 1) * 2 - 1);
            }
        }

        void ProgressStrip_Paint(object? sender, PaintEventArgs e)
        {
            _scanOffset = (_scanOffset + 4f) % (_progressStrip.Width * 2);
            using var bg = new SolidBrush(Color.FromArgb(2, 2, 14));
            e.Graphics.FillRectangle(bg, e.ClipRectangle);

            float x = _scanOffset - _progressStrip.Width;
            var blend = new ColorBlend
            {
                Colors    = new[] { Color.FromArgb(0, Neon), Color.FromArgb(160, Neon), Color.FromArgb(255, Neon), Color.FromArgb(160, Neon), Color.FromArgb(0, Neon) },
                Positions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
            };
            using var grad = new LinearGradientBrush(new PointF(x, 0), new PointF(x + _progressStrip.Width, 0), Color.Transparent, Color.Transparent);
            grad.InterpolationColors = blend;
            e.Graphics.FillRectangle(grad, x, 0, _progressStrip.Width, _progressStrip.Height);
        }

        // ─── UI helpers ───────────────────────────────────────────────────────

        void AppendLog(string msg)
        {
            if (InvokeRequired) { Invoke(() => AppendLog(msg)); return; }
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            _log.ScrollToCaret();
        }

        void SetStatus(string text, bool busy = false)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(text, busy)); return; }
            _statusText.Text     = $"STATUS: {text}";
            _statusDot.ForeColor = busy ? Color.FromArgb(255, 170, 0) : Neon;
            _progressStrip.Visible = busy;
        }

        void SetButtons(bool on)
        {
            if (InvokeRequired) { Invoke(() => SetButtons(on)); return; }
            _btnRestore.Enabled = on;
            _btnFN.Enabled      = on;
        }

        // ─── Create Restore Point ─────────────────────────────────────────────

        async void BtnRestore_Click(object? sender, EventArgs e)
        {
            SetButtons(false);
            SetStatus("CREATING RESTORE POINT...", busy: true);
            AppendLog("═══════════════════════════════════════");
            AppendLog("  CREATING SYSTEM RESTORE POINT");
            AppendLog("═══════════════════════════════════════");

            bool ok = await Task.Run(() =>
            {
                try
                {
                    AppendLog("[1/2] Enabling System Restore on C:\\...");
                    RunPS("Enable-ComputerRestore -Drive 'C:\\'");
                    AppendLog("[2/2] Creating restore point...");
                    return RunPS("Checkpoint-Computer -Description 'Fsociety Cleaner - Pre-Cleanup' -RestorePointType 'MODIFY_SETTINGS'");
                }
                catch (Exception ex) { AppendLog($"[ERROR] {ex.Message}"); return false; }
            });

            AppendLog(ok ? "[SUCCESS] Restore point created!" : "[WARNING] Could not confirm — run as Administrator.");
            AppendLog("═══════════════════════════════════════");
            SetStatus(ok ? "RESTORE POINT CREATED" : "RESTORE POINT — CHECK MANUALLY");
            SetButtons(true);
        }

        // ─── FN Cleaner ───────────────────────────────────────────────────────

        async void BtnFN_Click(object? sender, EventArgs e)
        {
            var r = MessageBox.Show(
                "FN CLEANER will permanently remove:\n\n" +
                "  •  Epic Games Launcher\n" +
                "  •  Fortnite (all versions)\n" +
                "  •  All related Registry entries\n" +
                "  •  All related AppData / Program folders\n" +
                "  •  DNS cache will be flushed\n\n" +
                "Recommended: create a Restore Point first!\n\nContinue?",
                "FN CLEANER — Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (r != DialogResult.Yes) { AppendLog("[ABORTED] Cancelled by user."); return; }

            SetButtons(false);
            SetStatus("RUNNING FN CLEANER...", busy: true);

            await Task.Run(() =>
            {
                try
                {
                    AppendLog("═══════════════════════════════════════");
                    AppendLog("  FN CLEANER — DEEP CLEAN STARTED");
                    AppendLog("═══════════════════════════════════════");
                    S1_Kill();
                    S2_Uninstall();
                    S3_Registry();
                    S4_Folders();
                    S5_DNS();
                    AppendLog("═══════════════════════════════════════");
                    AppendLog("  FN CLEANER — COMPLETE!");
                    AppendLog("[INFO] Restart recommended.");
                    AppendLog("═══════════════════════════════════════");
                    SetStatus("FN CLEANER COMPLETE");
                }
                catch (Exception ex) { AppendLog($"[FATAL] {ex.Message}"); SetStatus("ERROR — SEE LOG"); }
            });

            SetButtons(true);
        }

        // ─── Step 1: Kill processes ───────────────────────────────────────────

        void S1_Kill()
        {
            AppendLog("[STEP 1/5] Terminating Epic / Fortnite processes...");
            string[] names = { "FortniteClient-Win64-Shipping", "FortniteLauncher", "EpicGamesLauncher", "EpicWebHelper", "UnrealCEFSubProcess", "CrashReportClient", "EpicOnlineServices", "EOSBootstrapper", "EpicGames" };
            int n = 0;
            foreach (var name in names)
                foreach (var p in Process.GetProcessesByName(name))
                { try { p.Kill(); p.WaitForExit(4000); n++; AppendLog($"  > Killed: {name}"); } catch { } }
            AppendLog($"  > {n} process(es) terminated.");
        }

        // ─── Step 2: Official uninstallers ────────────────────────────────────

        void S2_Uninstall()
        {
            AppendLog("[STEP 2/5] Searching for uninstallers...");
            foreach (var root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
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
                            string? us = k!.GetValue("QuietUninstallString") as string ?? k.GetValue("UninstallString") as string;
                            if (!string.IsNullOrWhiteSpace(us)) { AppendLog($"  > Uninstalling: {dn}"); RunShell(us + " /silent /S /SILENT"); }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            AppendLog("  > Uninstaller step complete.");
        }

        // ─── Step 3: Registry ─────────────────────────────────────────────────

        void S3_Registry()
        {
            AppendLog("[STEP 3/5] Cleaning registry...");
            int d = 0;

            foreach (var p in new[] { @"SOFTWARE\EpicGames", @"SOFTWARE\WOW6432Node\EpicGames", @"SOFTWARE\WOW6432Node\Epic Games", @"SYSTEM\CurrentControlSet\Services\EpicOnlineServices", @"SYSTEM\CurrentControlSet\Services\EpicGamesLauncher" })
                d += DelKey(Registry.LocalMachine, p, "HKLM");

            foreach (var p in new[] { @"SOFTWARE\EpicGames", @"SOFTWARE\Epic Games", @"SOFTWARE\Classes\com.epicgames.launcher", @"SOFTWARE\Classes\com.epicgames.fortnite" })
                d += DelKey(Registry.CurrentUser, p, "HKCU");

            d += DelRunValues(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run");
            d += DelRunValues(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU\\Run");
            d += DelUninstallEntries();

            AppendLog($"  > {d} registry item(s) removed.");
        }

        int DelKey(RegistryKey hive, string path, string lbl)
        {
            try { if (hive.OpenSubKey(path) != null) { hive.DeleteSubKeyTree(path, false); AppendLog($"  > Deleted {lbl}\\{path}"); return 1; } }
            catch (Exception ex) { AppendLog($"  > [WARN] {lbl}\\{path}: {ex.Message}"); }
            return 0;
        }

        int DelRunValues(RegistryKey hive, string path, string lbl)
        {
            int c = 0;
            try
            {
                using var k = hive.OpenSubKey(path, true);
                if (k == null) return 0;
                foreach (var n in k.GetValueNames())
                {
                    string v = k.GetValue(n) as string ?? "";
                    if (v.Contains("Epic", StringComparison.OrdinalIgnoreCase) || v.Contains("Fortnite", StringComparison.OrdinalIgnoreCase))
                    { k.DeleteValue(n, false); AppendLog($"  > Removed startup: {n} ({lbl})"); c++; }
                }
            }
            catch { }
            return c;
        }

        int DelUninstallEntries()
        {
            int d = 0;
            foreach (var root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
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
                            if (dn != null && (dn.Contains("Epic Games", StringComparison.OrdinalIgnoreCase) || dn.Contains("EpicGames", StringComparison.OrdinalIgnoreCase) || dn.Contains("Fortnite", StringComparison.OrdinalIgnoreCase)))
                                del.Add(sub);
                        }
                        catch { }
                    }
                    foreach (var s in del)
                    { try { bk.DeleteSubKeyTree(s, false); AppendLog($"  > Removed uninstall entry: {s}"); d++; } catch { } }
                }
                catch { }
            }
            return d;
        }

        // ─── Step 4: Folders ──────────────────────────────────────────────────

        void S4_Folders()
        {
            AppendLog("[STEP 4/5] Deleting files and folders...");
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
                    AppendLog($"  > Deleting: {Shorten(dir)}");
                    ClearAttrs(new DirectoryInfo(dir));
                    Directory.Delete(dir, true);
                    n++; AppendLog("  > [OK]");
                }
                catch (Exception ex) { AppendLog($"  > [WARN] {ex.Message}"); }
            }
            AppendLog($"  > {n} folder(s) deleted.");
        }

        static string Shorten(string p) { var pts = p.Split(Path.DirectorySeparatorChar); return pts.Length > 2 ? $"...\\{pts[^2]}\\{pts[^1]}" : p; }

        static void ClearAttrs(DirectoryInfo d)
        {
            foreach (var s in d.GetDirectories()) ClearAttrs(s);
            foreach (var f in d.GetFiles()) f.Attributes = FileAttributes.Normal;
        }

        // ─── Step 5: DNS ──────────────────────────────────────────────────────

        void S5_DNS()
        {
            AppendLog("[STEP 5/5] Flushing DNS cache...");
            try { RunCmd("ipconfig.exe", "/flushdns"); AppendLog("  > [OK] DNS cache flushed."); }
            catch (Exception ex) { AppendLog($"  > [WARN] {ex.Message}"); }
        }

        // ─── Process helpers ──────────────────────────────────────────────────

        bool RunPS(string script)
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi); p?.WaitForExit(60_000); return p?.ExitCode == 0;
            }
            catch { return false; }
        }

        void RunCmd(string file, string args)
        {
            var psi = new ProcessStartInfo { FileName = file, Arguments = args, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi); p?.WaitForExit(15_000);
        }

        void RunShell(string cmd) => RunCmd("cmd.exe", $"/c {cmd}");
    }
}
