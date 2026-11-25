using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Deskosaur
{
    static class Program
    {
        public static string DataDir;

        [STAThread]
        static void Main()
        {
            Native.MakeDpiAware();
            DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Deskosaur");
            bool created;
            using (Mutex m = new Mutex(true, "Deskosaur.SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Crash(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Crash(e.ExceptionObject as Exception); };
                Host host = new Host();
                host.Run();
            }
        }

        static bool crashed;
        public static void Crash(Exception e)
        {
            if (crashed) return; crashed = true;
            try
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(Path.Combine(DataDir, "crash.log"), DateTime.Now + "\r\n" + e + "\r\n\r\n");
            }
            catch { }
            MessageBox.Show("Deskosaur tripped over its tail:\n\n" + (e == null ? "unknown error" : e.Message) + "\n\nDetails were saved to crash.log in " + DataDir, "Deskosaur", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    // Owns both windows and runs the 60 fps loop on the UI thread.
    sealed class Host : ApplicationContext
    {
        SaveData save;
        Launcher launcher; LauncherForm lform; Input lin = new Input();
        Game game; OverlayForm oform; Input gin = new Input();
        KeyHook hook = new KeyHook();
        WaveOut wave;
        DesktopScanner scanner = new DesktopScanner();
        bool inGame;
        Stopwatch clock = Stopwatch.StartNew();
        double next;
        int scale, screenX, screenY, screenW, screenH, floorScreen, frame;

        public void Run()
        {
            save = SaveData.Load(Path.Combine(Program.DataDir, "save.txt"));
            ApplyAudio();
            Sfx.Player = delegate (string n) { Audio.Play(n); };
            wave = new WaveOut();
            if (!wave.Start()) wave = null;
            Audio.SetSong(0);

            launcher = new Launcher(save);
            lform = new LauncherForm(lin, launcher);
            lform.FormClosed += delegate { Quit(); };
            lform.Show();
            lform.Activate();

            hook.Swallow = ShouldSwallow;
            Native.timeBeginPeriod(1);
            Application.Idle += OnIdle;
            Application.Run(this);
            Native.timeEndPeriod(1);
        }

        void ApplyAudio()
        {
            Audio.SoundOn = save.Sound; Audio.MusicOn = save.Music; Audio.Master = save.Volume / 10f * 0.9f;
        }

        bool AppIdle()
        {
            Native.MSG m;
            return !Native.PeekMessage(out m, IntPtr.Zero, 0, 0, 0);
        }

        void OnIdle(object sender, EventArgs e)
        {
            while (AppIdle())
            {
                double now = clock.Elapsed.TotalSeconds;
                if (now >= next)
                {
                    next += 1 / 60.0;
                    if (now - next > 0.2) next = now;
                    try { Step(); } catch (Exception ex) { Program.Crash(ex); return; }
                }
                else
                {
                    int ms = (int)((next - now) * 1000) - 1;
                    Thread.Sleep(ms > 0 ? Math.Min(ms, 10) : 0);
                }
            }
        }

        void Step()
        {
            frame++;
            ApplyAudio();
            if (!inGame)
            {
                lin.Latch();
                launcher.Tick(lin);
                launcher.Render(lin);
                lform.Present(save.Crt);
                if (launcher.Request == LReq.Quit) { Quit(); return; }
                if (launcher.Request == LReq.Minimize) { launcher.Request = LReq.None; lform.WindowState = FormWindowState.Minimized; }
                if (launcher.Request == LReq.Play) { launcher.Request = LReq.None; StartGame(launcher.PlayLevel); }
            }
            else
            {
                ReadGameKeys();
                if (frame % 3 == 0 && save.DeskOn)
                {
                    List<DeskPlat> plats = scanner.Scan(screenX, screenY, screenW, screenH, scale, floorScreen);
                    game.SetDesk(plats);
                }
                else if (!save.DeskOn && frame % 30 == 0) game.SetDesk(new List<DeskPlat>());
                game.Tick(gin);
                game.Render(gin);
                oform.Clickable = game.WantsMouse;
                oform.Present();
                if (frame % 60 == 0) oform.KeepOnTop();
                if (game.Request == GameReq.Menu) EndGame(false);
                else if (game.Request == GameReq.Quit) { EndGame(true); Quit(); }
            }
        }

        // ---------------- game session ----------------
        void StartGame(int level)
        {
            Rectangle b = Screen.PrimaryScreen.Bounds, wa = Screen.PrimaryScreen.WorkingArea;
            screenX = b.X; screenY = b.Y; screenW = b.Width; screenH = b.Height;
            scale = Math.Max(1, Math.Min(b.Width / 640, b.Height / 360));
            int lw = b.Width / scale, lh = b.Height / scale;
            int floorLow = (wa.Bottom - b.Y) / scale;
            if (floorLow < 352) floorLow = Math.Min(352, lh);
            floorScreen = b.Y + floorLow * scale;
            if (game == null || game.Fb.W != lw || game.Fb.H != lh) game = new Game(lw, lh, floorLow, save);
            if (oform == null || oform.IsDisposed) oform = new OverlayForm(gin, game, scale);
            else oform.Rebind(game, scale);
            oform.Bounds = b;
            game.Load(level);
            game.SetDesk(scanner.Scan(screenX, screenY, screenW, screenH, scale, floorScreen));
            lform.Hide();
            oform.PrepareShow();
            oform.Show();
            oform.KeepOnTop();
            game.Render(gin);
            oform.Present();
            gin.Reset();
            hook.Install();
            if (save.DeskOn) scanner.Start();
            inGame = true;
            Audio.SetSong(1);
        }

        void EndGame(bool quitting)
        {
            inGame = false;
            hook.Uninstall();
            scanner.Pause();
            oform.Hide();
            save.Write();
            if (quitting) return;
            launcher.ShowLevels();
            lin.Reset();
            lform.Show();
            lform.Activate();
            Audio.SetSong(0);
        }

        static bool IsGameVk(int vk, bool wasd)
        {
            switch (vk)
            {
                case 0x25: case 0x26: case 0x27: case 0x28: // arrows
                case 0x20: case 0x0D: case 0x1B:            // space, enter, esc
                case 0x52: case 0x50:                       // R, P
                    return true;
                case 0x57: case 0x41: case 0x53: case 0x44: // W A S D
                    return wasd;
            }
            return false;
        }

        bool ShouldSwallow(int vk)
        {
            if (!inGame || game == null) return false;
            if (game.CapturingKeys) return IsGameVk(vk, save.Wasd);
            if (vk == 0x1B) return true;
            // paused: only take menu keys when the overlay itself is focused
            return oform != null && Native.GetForegroundWindow() == oform.Handle && IsGameVk(vk, save.Wasd);
        }

        void ReadGameKeys()
        {
            bool[] d = hook.Down;
            bool take = game.CapturingKeys || Native.GetForegroundWindow() == oform.Handle;
            bool w = save.Wasd;
            for (int i = 0; i < gin.Down.Length; i++) gin.Down[i] = false;
            gin.Down[(int)Key.Back] = d[0x1B];
            if (take)
            {
                gin.Down[(int)Key.Left] = d[0x25] || (w && d[0x41]);
                gin.Down[(int)Key.Right] = d[0x27] || (w && d[0x44]);
                gin.Down[(int)Key.Up] = d[0x26] || (w && d[0x57]);
                gin.Down[(int)Key.Down] = d[0x28] || (w && d[0x53]);
                gin.Down[(int)Key.Jump] = d[0x20];
                gin.Down[(int)Key.Confirm] = d[0x0D];
                gin.Down[(int)Key.Restart] = d[0x52];
                gin.Down[(int)Key.Pause] = d[0x50];
            }
            gin.Latch();
        }

        void Quit()
        {
            try { hook.Uninstall(); } catch { }
            try { save.Write(); } catch { }
            try { if (wave != null) wave.Dispose(); } catch { }
            scanner.Dispose();
            Application.Idle -= OnIdle;
            ExitThread();
        }
    }

    // ---------------- the launcher window ----------------
    sealed class LauncherForm : Form
    {
        Input inp; Launcher l; int k;
        int[] crt = new int[Launcher.W * Launcher.H * 2];

        public LauncherForm(Input inp, Launcher l)
        {
            this.inp = inp; this.l = l;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            k = Math.Max(2, Math.Min(wa.Width * 85 / 100 / Launcher.W, wa.Height * 85 / 100 / Launcher.H));
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(Launcher.W * k, Launcher.H * k);
            Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
            Text = "Deskosaur";
            MaximizeBox = false;
            BackColor = Color.Black;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        public void Present(bool scan)
        {
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            int[] src = l.Fb.P; int W = Launcher.W, H = Launcher.H;
            if (scan)
            {
                for (int y = 0; y < H; y++)
                {
                    int so = y * W, d0 = y * 2 * W, d1 = d0 + W;
                    for (int x = 0; x < W; x++)
                    {
                        int c = src[so + x];
                        crt[d0 + x] = c;
                        crt[d1 + x] = ((c >> 1) & 0x7F7F7F) + ((c >> 2) & 0x3F3F3F);
                    }
                }
                Native.Present(Handle, crt, W, H * 2, ClientSize.Width, ClientSize.Height);
            }
            else Native.Present(Handle, src, W, H, ClientSize.Width, ClientSize.Height);
        }

        protected override void OnPaint(PaintEventArgs e) { Present(true); }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.Style |= 0x20000 | 0x80000; return cp; } // WS_MINIMIZEBOX | WS_SYSMENU: taskbar minimize/restore
        }

        static Key? MapKey(int vk)
        {
            switch (vk)
            {
                case 0x25: case 0x41: return Key.Left;
                case 0x27: case 0x44: return Key.Right;
                case 0x26: case 0x57: return Key.Up;
                case 0x28: case 0x53: return Key.Down;
                case 0x20: return Key.Jump;
                case 0x0D: return Key.Confirm;
                case 0x1B: case 0x08: return Key.Back;
                case 0x52: return Key.Restart;
            }
            return null;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x100: case 0x104: { Key? kk = MapKey(m.WParam.ToInt32()); if (kk.HasValue) { inp.Down[(int)kk.Value] = true; return; } break; }
                case 0x101: case 0x105: { Key? kk = MapKey(m.WParam.ToInt32()); if (kk.HasValue) { inp.Down[(int)kk.Value] = false; return; } break; }
                case 0x200: SetMouse(m.LParam); break;
                case 0x201: SetMouse(m.LParam); inp.Click = true; inp.MouseDown = true; break;
                case 0x202: inp.MouseDown = false; break;
                case 0x2A3: inp.MouseX = inp.MouseY = -1; break;
                case 0x84: // WM_NCHITTEST -> drag by the top strip
                    {
                        base.WndProc(ref m);
                        int sx = (short)(m.LParam.ToInt64() & 0xFFFF), sy = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                        Point p = PointToClient(new Point(sx, sy));
                        if (m.Result.ToInt32() == 1 && l.IsDragArea(p.X / k, p.Y / k)) m.Result = new IntPtr(2);
                        return;
                    }
                case 0xA3: return; // ignore double-click on the drag strip (no maximize)
                case 0x0006: if (m.WParam.ToInt32() == 0) { for (int i = 0; i < inp.Down.Length; i++) inp.Down[i] = false; } break; // WM_ACTIVATE inactive
            }
            base.WndProc(ref m);
        }

        void SetMouse(IntPtr lp)
        {
            int x = (short)(lp.ToInt64() & 0xFFFF), y = (short)((lp.ToInt64() >> 16) & 0xFFFF);
            inp.MouseX = x / k; inp.MouseY = y / k;
        }
    }

    // ---------------- the full-screen transparent overlay the dino lives on ----------------
    sealed class OverlayForm : Form
    {
        Input inp; Game game; int scale;
        bool clickable;

        public OverlayForm(Input inp, Game game, int scale)
        {
            this.inp = inp; this.game = game; this.scale = scale;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(255, 0, 255);
            Text = "Deskosaur Overlay";
            TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);
        }

        public void Rebind(Game g, int s) { game = g; scale = s; }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SetLayeredWindowAttributes(Handle, 0x00FF00FF, 0, 3);
            hiddenUntilDrawn = true;
        }

        bool hiddenUntilDrawn;
        // fade-in guard: keep the window fully transparent until the first real frame is drawn (no black flash)
        public void PrepareShow()
        {
            if (IsHandleCreated) Native.SetLayeredWindowAttributes(Handle, 0x00FF00FF, 0, 3);
            hiddenUntilDrawn = true;
        }

        public bool Clickable
        {
            set
            {
                if (value == clickable || !IsHandleCreated) return;
                clickable = value;
                int ex = Native.GetExStyle(Handle);
                if (value) ex &= ~(Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE);
                else ex |= Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE;
                Native.SetExStyle(Handle, ex);
                if (!value) { inp.MouseX = inp.MouseY = -1; }
            }
        }

        public void KeepOnTop()
        {
            if (IsHandleCreated) Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }

        public void Present()
        {
            if (!IsHandleCreated || !Visible) return;
            Native.Present(Handle, game.Fb.P, game.Fb.W, game.Fb.H, game.Fb.W * scale, game.Fb.H * scale);
            if (hiddenUntilDrawn) { hiddenUntilDrawn = false; Native.SetLayeredWindowAttributes(Handle, 0x00FF00FF, 255, 3); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(255, 0, 255));
            Present();
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x200: SetMouse(m.LParam); break;
                case 0x201: SetMouse(m.LParam); inp.Click = true; break;
                case 0x2A3: inp.MouseX = inp.MouseY = -1; break;
            }
            base.WndProc(ref m);
        }

        void SetMouse(IntPtr lp)
        {
            int x = (short)(lp.ToInt64() & 0xFFFF), y = (short)((lp.ToInt64() >> 16) & 0xFFFF);
            inp.MouseX = x / scale; inp.MouseY = y / scale;
        }
    }
}
