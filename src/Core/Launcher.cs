using System;
using System.Collections.Generic;

namespace Deskosaur
{
    public enum LReq { None, Play, Quit, Minimize }
    enum LScreen { Title, Levels, Wardrobe, Options, Help }

    public sealed class Launcher
    {
        public const int W = 480, H = 270;
        public Surface Fb = new Surface(W, H);
        LauncherScene scene = new LauncherScene();
        Sprite logo; bool[,] logoMask;
        SaveData save;
        public LReq Request; public int PlayLevel;
        LScreen screen = LScreen.Title, nextScreen; int wipe = 0;
        int t, sel, levelSel, wardCat, optSel, resetArm;
        Outfit preview;
        Art.FrameSet dinoFrames, previewFrames;
        Random rng = new Random(42);
        Particles fx = new Particles();
        int lastMX = -1, lastMY = -1;

        static readonly int Ink = G.Hex(0x07040c);
        static readonly string[] MainItems = { "PLAY", "LEVELS", "WARDROBE", "OPTIONS", "HOW TO PLAY", "QUIT" };
        const int MenuX = 30, MenuY = 92, MenuStep = 24;

        public Launcher(SaveData save)
        {
            this.save = save;
            preview = save.Look.Clone();
            levelSel = Math.Max(0, Math.Min(19, save.Unlocked - 1));
            BuildLogo();
        }

        public void ShowLevels() { screen = LScreen.Levels; levelSel = Math.Max(0, Math.Min(19, save.Unlocked - 1)); sel = 0; Request = LReq.None; }
        public void ShowTitle() { screen = LScreen.Title; Request = LReq.None; }

        public bool IsDragArea(int x, int y) { return y < 12 && x < W - 30; }

        bool Moved(Input inp) { return inp.MouseX != lastMX || inp.MouseY != lastMY; }

        // =========================== update ===========================
        public void Tick(Input inp)
        {
            t++;
            if (wipe > 0) { wipe--; if (wipe == 8) { screen = nextScreen; sel = 0; } lastMX = inp.MouseX; lastMY = inp.MouseY; return; }
            if (inp.Click && Ui.Hit(inp, W - 14, 2, 12, 10)) { Request = LReq.Quit; inp.Click = false; return; }
            if (inp.Click && Ui.Hit(inp, W - 28, 2, 12, 10)) { Request = LReq.Minimize; inp.Click = false; return; }
            switch (screen)
            {
                case LScreen.Title: TitleTick(inp); break;
                case LScreen.Levels: LevelsTick(inp); break;
                case LScreen.Wardrobe: WardTick(inp); break;
                case LScreen.Options: OptTick(inp); break;
                case LScreen.Help: if (inp.P(Key.Back) || inp.P(Key.Confirm) || inp.P(Key.Jump) || inp.Click) Go(LScreen.Title); break;
            }
            lastMX = inp.MouseX; lastMY = inp.MouseY;
            fx.Update();
        }

        void Go(LScreen s) { nextScreen = s; wipe = 16; Sfx.Play(s == LScreen.Title ? "back" : "select"); resetArm = 0; }

        int NavV(Input inp, int cur, int n)
        {
            if (inp.P(Key.Up)) { Sfx.Play("click"); return (cur + n - 1) % n; }
            if (inp.P(Key.Down)) { Sfx.Play("click"); return (cur + 1) % n; }
            return cur;
        }

        void TitleTick(Input inp)
        {
            int before = sel;
            sel = NavV(inp, sel, MainItems.Length);
            int clicked = -1;
            for (int i = 0; i < MainItems.Length; i++)
                if (Ui.Hit(inp, MenuX - 8, MenuY + i * MenuStep - 3, 150, 20)) { if (Moved(inp)) sel = i; if (inp.Click) clicked = i; }
            if (sel != before && clicked < 0 && !inp.P(Key.Up) && !inp.P(Key.Down)) Sfx.Play("click");
            if (inp.P(Key.Confirm) || inp.P(Key.Jump)) clicked = sel;
            if (inp.P(Key.Back)) sel = MainItems.Length - 1;
            if (clicked < 0) return;
            switch (clicked)
            {
                case 0: Sfx.Play("start"); PlayLevel = FirstUnbeaten(); Request = LReq.Play; break;
                case 1: Go(LScreen.Levels); break;
                case 2: preview = save.Look.Clone(); wardCat = 0; Go(LScreen.Wardrobe); break;
                case 3: optSel = 0; Go(LScreen.Options); break;
                case 4: Go(LScreen.Help); break;
                case 5: Request = LReq.Quit; break;
            }
        }

        int FirstUnbeaten()
        {
            for (int i = 0; i < 20; i++) if (save.Best[i] == 0) return Math.Max(0, Math.Min(i, save.Unlocked - 1));
            return 0;
        }

        static void CardPos(int i, out int x, out int y) { x = 128 + (i % 5) * 68; y = 30 + (i / 5) * 52; }

        void LevelsTick(Input inp)
        {
            int c = levelSel % 5, r = levelSel / 5;
            if (inp.P(Key.Left)) { c = (c + 4) % 5; Sfx.Play("click"); }
            if (inp.P(Key.Right)) { c = (c + 1) % 5; Sfx.Play("click"); }
            if (inp.P(Key.Up)) { r = (r + 3) % 4; Sfx.Play("click"); }
            if (inp.P(Key.Down)) { r = (r + 1) % 4; Sfx.Play("click"); }
            levelSel = r * 5 + c;
            bool go = inp.P(Key.Confirm) || inp.P(Key.Jump);
            for (int i = 0; i < 20; i++)
            {
                int x, y; CardPos(i, out x, out y);
                if (Ui.Hit(inp, x, y, 62, 44)) { if (Moved(inp)) levelSel = i; if (inp.Click) { levelSel = i; go = true; } }
            }
            if (Ui.Hit(inp, 14, H - 22, 50, 13) && inp.Click) { Go(LScreen.Title); return; }
            if (inp.P(Key.Back)) { Go(LScreen.Title); return; }
            if (go)
            {
                if (levelSel < save.Unlocked) { Sfx.Play("start"); PlayLevel = levelSel; Request = LReq.Play; }
                else Sfx.Play("locked");
            }
        }

        Item[] Cat(int c)
        {
            switch (c) { case 1: return Art.Hats; case 2: return Art.Glasses; case 3: return Art.Shirts; case 4: return Art.Shoes; }
            return null;
        }
        int GetCat(Outfit o, int c) { switch (c) { case 0: return o.Char; case 1: return o.Hat; case 2: return o.Glasses; case 3: return o.Shirt; default: return o.Shoes; } }
        void SetCat(Outfit o, int c, int v) { switch (c) { case 0: o.Char = v; break; case 1: o.Hat = v; break; case 2: o.Glasses = v; break; case 3: o.Shirt = v; break; default: o.Shoes = v; break; } }
        int CatCount(int c) { return c == 0 ? Art.Dinos.Length : Cat(c).Length; }
        bool Locked(int c, int v) { return c > 0 && Cat(c)[v].Cost > save.TotalAmber(); }

        const int WardX = 262, WardY = 44, WardStep = 34, WardW = 200;

        void WardTick(Input inp)
        {
            wardCat = NavV(inp, wardCat, 6);
            int dir = 0;
            if (inp.P(Key.Left)) dir = -1;
            if (inp.P(Key.Right)) dir = 1;
            for (int c = 0; c < 5; c++)
            {
                int y = WardY + c * WardStep;
                if (Ui.Hit(inp, WardX, y, WardW, 26) && Moved(inp)) wardCat = c;
                if (inp.Click && Ui.Hit(inp, WardX, y + 10, 18, 16)) { wardCat = c; dir = -1; }
                if (inp.Click && Ui.Hit(inp, WardX + WardW - 18, y + 10, 18, 16)) { wardCat = c; dir = 1; }
            }
            if (Ui.Hit(inp, 312, 222, 100, 13) && Moved(inp)) wardCat = 5;
            if (dir != 0 && wardCat < 5)
            {
                int n = CatCount(wardCat);
                SetCat(preview, wardCat, (GetCat(preview, wardCat) + dir + n) % n);
                Sfx.Play(Locked(wardCat, GetCat(preview, wardCat)) ? "locked" : "equip");
                for (int i = 0; i < 10; i++) fx.Spawn(PT.Star, 120 + Particles.R(-30, 30), 160 + Particles.R(-50, 10), Particles.R(-1, 1), Particles.R(-1.5f, 0), 22, G.Hex(0xffd23a), 1, 0.05f);
            }
            bool done = inp.P(Key.Back) || ((inp.P(Key.Confirm) || inp.P(Key.Jump)) && wardCat == 5) || (inp.Click && Ui.Hit(inp, 312, 222, 100, 13));
            if (done)
            {
                Outfit o = save.Look;
                o.Char = preview.Char;
                for (int c = 1; c < 5; c++) { int v = GetCat(preview, c); if (!Locked(c, v)) SetCat(o, c, v); }
                save.Write();
                Go(LScreen.Title);
            }
        }

        static readonly string[] OptNames = { "DESKTOP PLATFORMS", "SHOW DESKTOP EDGES", "SOUND FX", "MUSIC", "VOLUME", "CAPTURE WASD KEYS", "CRT SCANLINES", "RESET PROGRESS", "BACK" };
        const int OptX = 90, OptY = 46, OptStep = 18, OptW = 300;

        void OptTick(Input inp)
        {
            optSel = NavV(inp, optSel, OptNames.Length);
            int dir = 0; bool act = inp.P(Key.Confirm) || inp.P(Key.Jump);
            if (inp.P(Key.Left)) dir = -1; if (inp.P(Key.Right)) dir = 1;
            for (int i = 0; i < OptNames.Length; i++)
                if (Ui.Hit(inp, OptX, OptY + i * OptStep - 3, OptW, 15)) { if (Moved(inp)) optSel = i; if (inp.Click) { optSel = i; act = true; } }
            if (inp.P(Key.Back)) { save.Write(); Go(LScreen.Title); return; }
            if (!act && dir == 0) return;
            if (dir == 0) dir = 1;
            Sfx.Play("click");
            switch (optSel)
            {
                case 0: save.DeskOn = !save.DeskOn; break;
                case 1: save.ShowEdges = !save.ShowEdges; break;
                case 2: save.Sound = !save.Sound; break;
                case 3: save.Music = !save.Music; break;
                case 4: save.Volume = Math.Max(0, Math.Min(10, save.Volume + (act && inp.Click ? (save.Volume >= 10 ? -10 : 1) : dir))); break;
                case 5: save.Wasd = !save.Wasd; break;
                case 6: save.Crt = !save.Crt; break;
                case 7:
                    if (!act) break;
                    if (resetArm == 0) { resetArm = 1; Sfx.Play("locked"); }
                    else
                    {
                        save.Unlocked = 1; for (int i = 0; i < 20; i++) { save.Best[i] = 0; save.Amber[i] = 0; }
                        save.Look = new Outfit(); preview = save.Look.Clone(); resetArm = 0; Sfx.Play("back");
                    }
                    break;
                case 8: if (act) { save.Write(); Go(LScreen.Title); } return;
            }
            if (optSel != 7) resetArm = 0;
            save.Write();
        }

        // =========================== render ===========================
        public void Render(Input inp)
        {
            Surface s = Fb;
            scene.Render(s, save.Look);
            if (dinoFrames == null || dinoFrames.Hash != save.Look.Hash()) dinoFrames = Art.Build(save.Look);
            switch (screen)
            {
                case LScreen.Title: DrawTitle(s, inp); break;
                case LScreen.Levels: DrawLevels(s, inp); break;
                case LScreen.Wardrobe: DrawWard(s, inp); break;
                case LScreen.Options: DrawOpt(s, inp); break;
                case LScreen.Help: DrawHelp(s); break;
            }
            fx.Draw(s, t);
            DrawChrome(s, inp);
            if (wipe > 0)
            {
                int lvl = wipe > 8 ? (16 - wipe) * 2 : wipe * 2;
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (G.Dith(x, y, lvl)) s.P[y * W + x] = G.Hex(0x05030a);
            }
            if (inp != null) inp.Click = false;
        }

        void DrawChrome(Surface s, Input inp)
        {
            bool hc = Ui.Hit(inp, W - 14, 2, 12, 10), hm = Ui.Hit(inp, W - 28, 2, 12, 10);
            G.Rect(s, W - 28, 2, 12, 10, hm ? G.Hex(0x3a2a6a) : G.Hex(0x120c20)); G.RectO(s, W - 28, 2, 12, 10, G.Hex(0x4a3a6a));
            G.HLine(s, W - 25, W - 20, 9, G.Hex(0xd8d0ec));
            G.Rect(s, W - 14, 2, 12, 10, hc ? G.Hex(0xb01840) : G.Hex(0x120c20)); G.RectO(s, W - 14, 2, 12, 10, G.Hex(0x4a3a6a));
            for (int i = 0; i < 6; i++) { G.Px(s, W - 11 + i, 4 + i, G.Hex(0xd8d0ec)); G.Px(s, W - 6 - i, 4 + i, G.Hex(0xd8d0ec)); }
            string am = save.TotalAmber() + "/60";
            int ax = W - 36 - Font.Measure(am, 1);
            Ui.Amber(s, ax - 8, 4, true);
            Font.TextO(s, am, ax, 4, G.Hex(0xffc93a), Ink, 1);
            G.RectO(s, 0, 0, W, H, G.Hex(0x1e1430));
        }

        // ---------------- logo ----------------
        static readonly string[] Letters = {
            "D#####..|##..##.|##...##|##...##|##...##|##...##|##...##|##..##.|#####..",
            "E#######|##.....|##.....|#####..|##.....|##.....|##.....|##.....|#######",
            "S.#####.|##...##|##.....|.####..|...####|.....##|.....##|##...##|.#####.",
            "K##...##|##..##.|##.##..|####...|###....|####...|##.##..|##..##.|##...##",
            "O.#####.|##...##|##...##|##...##|##...##|##...##|##...##|##...##|.#####.",
            "A..###..|.##.##.|##...##|##...##|#######|##...##|##...##|##...##|##...##",
            "U##...##|##...##|##...##|##...##|##...##|##...##|##...##|##...##|.#####.",
            "R######.|##...##|##...##|##...##|######.|##.##..|##..##.|##...##|##...##",
        };
        const int LogoX = 14, LogoY = 14;

        void BuildLogo()
        {
            string word = "DESKOSAUR";
            int sc = 4, lw = 7 * sc, lh = 9 * sc, gap = 5, pad = 4;
            int w = word.Length * lw + (word.Length - 1) * gap + pad * 2 + 6, h = lh + pad * 2 + 6;
            bool[,] m = new bool[w, h];
            int x = pad;
            foreach (char ch in word)
            {
                string def = null; foreach (string d in Letters) if (d[0] == ch) def = d;
                string[] rows = def.Substring(1).Split(new char[] { '|' });
                for (int ry = 0; ry < 9; ry++) for (int rx = 0; rx < 7; rx++)
                        if (rows[ry][rx] == '#')
                            for (int yy = 0; yy < sc; yy++) for (int xx = 0; xx < sc; xx++)
                                {
                                    // chamfer outer corners of each block for a chunkier, carved look
                                    bool corner = (xx == 0 || xx == sc - 1) && (yy == 0 || yy == sc - 1);
                                    if (corner)
                                    {
                                        int nx = rx + (xx == 0 ? -1 : 1), ny = ry + (yy == 0 ? -1 : 1);
                                        bool hasX = nx >= 0 && nx < 7 && rows[ry][nx] == '#', hasY = ny >= 0 && ny < 9 && rows[ny][rx] == '#';
                                        if (!hasX && !hasY) continue;
                                    }
                                    m[x + rx * sc + xx, pad + ry * sc + yy] = true;
                                }
                x += lw + gap;
            }
            logoMask = m;
            logo = new Sprite(w, h);
            int[] grad = { 0xfff6d8, 0xffe08a, 0xffc04a, 0xff9a3a, 0xff6a4a, 0xf0407a, 0xc0309a, 0x7a2aa8, 0x4a1e80 };
            Random r = new Random(9);
            for (int d = 6; d >= 1; d--)
                for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
                        if (m[xx, yy] && xx + d < w && yy + d < h) logo.Set(xx + d, yy + d, d > 3 ? G.Hex(0x0e0618) : (d > 1 ? G.Hex(0x2a0e3e) : G.Hex(0x4a1a5a)));
            for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
                {
                    if (m[xx, yy]) continue;
                    bool near = false;
                    for (int oy = -1; oy <= 1 && !near; oy++) for (int ox = -1; ox <= 1; ox++)
                        {
                            int qx = xx + ox, qy = yy + oy;
                            if (qx >= 0 && qy >= 0 && qx < w && qy < h && m[qx, qy]) { near = true; break; }
                        }
                    if (near) logo.Set(xx, yy, Ink);
                }
            for (int yy = 0; yy < h; yy++) for (int xx = 0; xx < w; xx++)
                {
                    if (!m[xx, yy]) continue;
                    float u = (yy - pad) / (float)lh; if (u < 0) u = 0; if (u > 0.999f) u = 0.999f;
                    float gi = u * (grad.Length - 1);
                    int i0 = (int)gi; float fr = gi - i0;
                    int c = G.Hex(grad[i0]);
                    if (fr > 0.5f && G.Dith(xx, yy, (int)((fr - 0.5f) * 32))) c = G.Hex(grad[Math.Min(grad.Length - 1, i0 + 1)]);
                    if (yy - pad == lh * 5 / 9 || yy - pad == lh * 5 / 9 + 1) c = G.Mix(c, G.Hex(0x200830), 0.75f);   // chrome cut
                    if (!m[xx, yy - 1]) c = G.Hex(0xffffff);
                    else if (!m[xx - 1, yy] && u < 0.5f) c = G.Mix(c, G.Hex(0xffffff), 0.5f);
                    else if (!m[xx + 1, yy]) c = G.Mix(c, G.Hex(0x200830), 0.35f);
                    logo.Set(xx, yy, c);
                }
            // grime: scratches and chipped paint
            for (int k = 0; k < 26; k++)
            {
                int sx = r.Next(w), sy = r.Next(h), len = 3 + r.Next(8);
                for (int i = 0; i < len; i++)
                {
                    int px = sx + i, py = sy + i / 3;
                    if (px < w && py < h && m[px, py] && m[Math.Min(w - 1, px + 1), py]) logo.Set(px, py, G.Mix(logo.Get(px, py), G.Hex(0x1a0828), 0.55f));
                }
            }
            for (int k = 0; k < 90; k++)
            {
                int px = r.Next(w), py = r.Next(h);
                if (m[px, py]) logo.Set(px, py, G.Mix(logo.Get(px, py), r.Next(2) == 0 ? G.Hex(0x2a0a38) : G.Hex(0xffffff), 0.35f));
            }
        }

        void DrawLogo(Surface s)
        {
            int x = LogoX, y = LogoY;
            // pulsing neon halo behind the logo
            int halo = 2 + (int)((Math.Sin(t * 0.05f) * 0.5 + 0.5) * 2);
            if ((t % 260) > 246 && (t & 2) == 0) halo = 0;
            for (int yy = -4; yy < logo.H + 4; yy++)
                for (int xx = -4; xx < logo.W + 4; xx++)
                {
                    int qx = xx, qy = yy;
                    bool near = false;
                    for (int k = -3; k <= 3 && !near; k += 3) for (int j = -3; j <= 3; j += 3)
                        {
                            int ax = qx + k, ay = qy + j;
                            if (ax >= 0 && ay >= 0 && ax < logo.W && ay < logo.H && logoMask[ax, ay]) { near = true; break; }
                        }
                    if (near && G.Dith(x + xx, y + yy, halo)) { int b = G.GetPx(s, x + xx, y + yy); G.Px(s, x + xx, y + yy, G.Mix(b, G.Hex(0xc0308a), 0.5f)); }
                }
            G.Blit(s, logo, x, y, false);
            // glint sweeping across
            int gx = (t * 3) % (logo.W + 260) - 40;
            for (int yy = 0; yy < logo.H; yy++)
                for (int k = 0; k < 4; k++)
                {
                    int xx = gx + k - yy / 2;
                    if (xx < 0 || xx >= logo.W || !logoMask[xx, yy]) continue;
                    G.Px(s, x + xx, y + yy, k == 0 || k == 3 ? G.Mix(logo.Get(xx, yy), G.Hex(0xffffff), 0.6f) : G.Hex(0xffffff));
                }
            // rain drips off the bottom of the letters
            if (t % 23 == 0)
            {
                for (int tries = 0; tries < 20; tries++)
                {
                    int lx = rng.Next(logo.W);
                    for (int yy = logo.H - 1; yy > 0; yy--) if (logoMask[lx, yy]) { fx.Spawn(PT.Spark, x + lx, y + yy + 2, 0, 0.3f, 50, G.Hex(0x8a9ac8), 1, 0.12f); tries = 99; break; }
                }
            }
            // little dino strolling along the top of the logo
            int ax2, ay2; int f = (t / 6) & 3;
            float walk = (t * 0.55f) % (logo.W + 60);
            int dir = ((int)(t * 0.55f) / (logo.W + 60)) % 2 == 0 ? 1 : -1;
            float wx = dir > 0 ? walk - 30 : logo.W + 30 - walk;
            if (wx > 4 && wx < logo.W - 4)
            {
                int top = 0; int ix = (int)wx;
                for (int yy = 0; yy < logo.H; yy++) if (logoMask[Math.Max(0, Math.Min(logo.W - 1, ix)), yy]) { top = yy; break; }
                if (top == 0) top = 4;
                Sprite sp = dinoFrames.Get(f == 0 ? Pose.Run0 : f == 1 ? Pose.Run1 : f == 2 ? Pose.Run2 : Pose.Run3, (t / 4) & 1, out ax2, out ay2);
                bool flip = dir < 0; int axi = flip ? sp.W - 1 - ax2 : ax2;
                G.Blit(s, sp, x + ix - axi, y + top - ay2 - 1, flip);
            }
        }

        // ---------------- screens ----------------
        void Shade(Surface s, int x, int y, int w, int h, int lvl)
        {
            for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
                for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
                    if (G.Dith(xx, yy, lvl)) s.P[yy * W + xx] = G.Hex(0x05030a);
        }

        void DrawTitle(Surface s, Input inp)
        {
            DrawLogo(s);
            Shade(s, MenuX - 18, MenuY - 14, 160, MainItems.Length * MenuStep + 18, 8);
            G.VLine(s, MenuX - 18, MenuY - 14, MenuY + MainItems.Length * MenuStep + 3, G.Hex(0x3a1a4a));
            for (int i = 0; i < MainItems.Length; i++)
            {
                int y = MenuY + i * MenuStep;
                bool on = sel == i;
                string label = MainItems[i];
                if (on)
                {
                    bool buzz = (t % 97) < 3;
                    int glow = buzz ? G.Hex(0x5a1a48) : G.Hex(0xff3aa8);
                    for (int oy = -2; oy <= 2; oy++) for (int ox = -2; ox <= 2; ox++)
                            if ((ox != 0 || oy != 0) && Math.Abs(ox) + Math.Abs(oy) <= 3) Font.Text(s, label, MenuX + ox, y + oy, G.Hex(0x3a0a2e), 2);
                    Font.Text(s, label, MenuX + 1, y + 1, Ink, 2);
                    Font.Text(s, label, MenuX, y, buzz ? G.Hex(0x8a3a70) : G.Hex(0xffd8f0), 2);
                    // neon underline + chevron
                    int lw = Font.Measure(label, 2);
                    G.HLine(s, MenuX, MenuX + lw, y + 16, glow);
                    int bob = (t / 8) & 1;
                    Font.Text(s, ">", MenuX - 13 + bob, y + 1, glow, 2);
                }
                else
                {
                    Font.Text(s, label, MenuX + 1, y + 1, Ink, 2);
                    Font.Text(s, label, MenuX, y, G.Hex(0x7a6e9a), 2);
                }
            }
            Font.TextO(s, "ARROWS + ENTER  OR  MOUSE", 14, H - 12, G.Hex(0x5a5078), Ink, 1);
            Font.TextO(s, "V1.2", 196, H - 12, G.Hex(0x3a3058), Ink, 1);
        }

        void DrawLevels(Surface s, Input inp)
        {
            Shade(s, 0, 0, W, H, 10);
            Font.CenteredO(s, "SELECT LEVEL", W / 2, 10, G.Hex(0xffffff), Ink, 2);
            int[] wc = { 0x3ee0c8, 0x9ae6ff, 0xffc828, 0xff4ab8 };
            for (int r = 0; r < 4; r++)
            {
                int y = 30 + r * 52;
                Font.TextO(s, "WORLD " + (r + 1), 16, y + 12, G.Hex(wc[r]), Ink, 1);
                Font.TextO(s, Levels.WorldNames[r], 16, y + 23, G.Hex(0xe8e0f8), Ink, 1);
            }
            for (int i = 0; i < 20; i++)
            {
                int x, y; CardPos(i, out x, out y);
                bool locked = i >= save.Unlocked, on = levelSel == i;
                int border = on ? (((t / 8) & 1) == 0 ? G.Hex(0xffffff) : G.Hex(wc[i / 5])) : G.Hex(0x34284e);
                G.Rect(s, x + 2, y + 2, 62, 44, Ink);
                G.Rect(s, x, y, 62, 44, locked ? G.Hex(0x0e0a18) : (on ? G.Hex(0x2a1e48) : G.Hex(0x181230)));
                G.RectD(s, x + 1, y + 1, 60, 14, G.Hex(0x2a2048), locked ? 0 : 6);
                G.RectO(s, x, y, 62, 44, border);
                if (locked) { Font.Centered(s, "&", x + 31, y + 18, G.Hex(0x3a3058), 1); continue; }
                string num = (i + 1).ToString("00");
                Font.Centered(s, num, x + 32, y + 7, Ink, 3);
                Font.Centered(s, num, x + 31, y + 6, save.Best[i] > 0 ? G.Hex(wc[i / 5]) : G.Hex(0xf4ecff), 3);
                for (int a = 0; a < 3; a++) Ui.Amber(s, x + 18 + a * 10, y + 32, (save.Amber[i] & (1 << a)) != 0);
                if (save.Best[i] > 0) { G.Px(s, x + 55, y + 5, G.Hex(0x4aff8a)); G.Px(s, x + 56, y + 6, G.Hex(0x4aff8a)); G.Px(s, x + 57, y + 5, G.Hex(0x4aff8a)); G.Px(s, x + 58, y + 4, G.Hex(0x4aff8a)); G.Px(s, x + 59, y + 3, G.Hex(0x4aff8a)); }
            }
            LevelDef d = Levels.All[levelSel];
            string info = levelSel < save.Unlocked ? ("LV" + (levelSel + 1).ToString("00") + "  " + d.Name + "   BEST " + SaveData.Time(save.Best[levelSel])) : "LOCKED - BEAT THE LEVEL BEFORE IT";
            Font.CenteredO(s, info, W / 2 + 30, H - 18, G.Hex(0xffd23a), Ink, 1);
            Ui.Button(s, inp, "BACK", 14, H - 22, 50, false, t);
        }

        void DrawWard(Surface s, Input inp)
        {
            Shade(s, 0, 0, W, H, 9);
            Font.CenteredO(s, "WARDROBE", W / 2, 10, G.Hex(0xffffff), Ink, 2);
            int cx = 124, floorY = 214;
            for (int yy = 34; yy < floorY; yy++)
            {
                int half = 14 + (yy - 34) / 2;
                for (int xx = cx - half; xx <= cx + half; xx++) if (G.Dith(xx, yy, 3)) { int b = G.GetPx(s, xx, yy); G.Px(s, xx, yy, G.Mix(b, G.Hex(0x6a4a8a), 0.4f)); }
            }
            for (int yy = -6; yy <= 6; yy++) for (int xx = -70; xx <= 70; xx++) if (xx * xx / 4900f + yy * yy / 36f <= 1) G.Px(s, cx + xx, floorY + yy, (yy < 0) ? G.Hex(0x2e2250) : G.Hex(0x1a1430));
            G.HLine(s, cx - 56, cx + 56, floorY - 5, G.Hex(0xff4ab8));
            if (previewFrames == null || previewFrames.Hash != preview.Hash()) previewFrames = Art.Build(preview);
            int cyc = (t / 120) % 3; Pose pose;
            if (cyc == 0) pose = (t % 90) < 6 ? Pose.Blink : (((t / 30) & 1) == 0 ? Pose.Idle0 : Pose.Idle1);
            else if (cyc == 1) { int f = (t / 6) & 3; pose = f == 0 ? Pose.Run0 : f == 1 ? Pose.Run1 : f == 2 ? Pose.Run2 : Pose.Run3; }
            else pose = Pose.Happy;
            int ax, ay; Sprite sp = previewFrames.Get(pose, (t / 4) & 1, out ax, out ay);
            int hop = cyc == 2 ? (int)Math.Abs(Math.Sin(t * 0.12f) * 10) : 0;
            G.BlitScaled(s, sp, cx - ax * 4 - 2, floorY - 5 - ay * 4 - 4 - hop, 4, false);
            Font.CenteredO(s, Art.Dinos[preview.Char].Name, cx, 228, G.Hex(0xffd23a), Ink, 2);
            string[] cats = { "DINO", "HAT", "GLASSES", "SHIRT", "SHOES" };
            for (int c = 0; c < 5; c++)
            {
                int y = WardY + c * WardStep;
                bool on = wardCat == c;
                int v = GetCat(preview, c);
                string name = c == 0 ? Art.Dinos[v].Name : Cat(c)[v].Name;
                bool locked = Locked(c, v);
                Font.TextO(s, cats[c], WardX + 2, y, on ? G.Hex(0xff4ab8) : G.Hex(0x8a80a8), Ink, 1);
                int cnt = CatCount(c);
                string idx = (v + 1) + "/" + cnt;
                Font.TextO(s, idx, WardX + WardW - Font.Measure(idx, 1), y, G.Hex(0x5a5078), Ink, 1);
                G.Rect(s, WardX, y + 10, WardW, 16, on ? G.Hex(0x2a1e48) : G.Hex(0x161028)); G.RectO(s, WardX, y + 10, WardW, 16, on ? G.Hex(0xff4ab8) : G.Hex(0x34284e));
                int wob = on && ((t / 10) & 1) == 0 ? 1 : 0;
                Font.Text(s, "<", WardX + 5 - wob, y + 15, G.Hex(0x3ee0c8), 1);
                Font.Text(s, ">", WardX + WardW - 9 + wob, y + 15, G.Hex(0x3ee0c8), 1);
                if (locked)
                {
                    Font.Centered(s, "& " + name, WardX + WardW / 2, y + 15, G.Hex(0x6a6088), 1);
                    Font.TextO(s, "NEED " + Cat(c)[v].Cost + "$", WardX + 70, y, G.Hex(0xffb030), Ink, 1);
                }
                else Font.Centered(s, name, WardX + WardW / 2, y + 15, G.Hex(0xffffff), 1);
            }
            Ui.Button(s, inp, "DONE", 312, 222, 100, wardCat == 5, t);
            Font.CenteredO(s, "AMBER $ UNLOCKS NEW GEAR", 362, 246, G.Hex(0x8a80a8), Ink, 1);
        }

        void DrawOpt(Surface s, Input inp)
        {
            Shade(s, 0, 0, W, H, 9);
            Font.CenteredO(s, "OPTIONS", W / 2, 10, G.Hex(0xffffff), Ink, 2);
            Ui.Box(s, OptX - 10, OptY - 10, OptW + 20, OptNames.Length * OptStep + 14, G.Hex(0x3ee0c8));
            for (int i = 0; i < OptNames.Length; i++)
            {
                int y = OptY + i * OptStep;
                bool on = optSel == i;
                if (on) G.Rect(s, OptX - 4, y - 4, OptW + 8, 15, G.Hex(0x2a1e48));
                string label = OptNames[i];
                if (i == 7 && resetArm == 1) label = "REALLY RESET? PRESS AGAIN";
                Font.Text(s, label, OptX + 4, y, on ? G.Hex(0xffffff) : G.Hex(0xb0a8d0), 1);
                string v = "";
                switch (i)
                {
                    case 0: v = save.DeskOn ? "ON" : "OFF"; break;
                    case 1: v = save.ShowEdges ? "ON" : "OFF"; break;
                    case 2: v = save.Sound ? "ON" : "OFF"; break;
                    case 3: v = save.Music ? "ON" : "OFF"; break;
                    case 5: v = save.Wasd ? "ON" : "OFF"; break;
                    case 6: v = save.Crt ? "ON" : "OFF"; break;
                }
                int vx = OptX + OptW - 70;
                if (i == 4) { for (int k = 0; k < 10; k++) G.Rect(s, vx + k * 7, y, 5, 7, k < save.Volume ? G.Hex(0x3ee0c8) : G.Hex(0x34284e)); }
                else if (v.Length > 0)
                {
                    int col = v == "OFF" ? G.Hex(0xff4a6a) : G.Hex(0x4aff8a);
                    Font.Text(s, "<", vx + 6, y, on ? G.Hex(0x3ee0c8) : G.Hex(0x34284e), 1);
                    Font.Centered(s, v, vx + 34, y, col, 1);
                    Font.Text(s, ">", vx + 58, y, on ? G.Hex(0x3ee0c8) : G.Hex(0x34284e), 1);
                }
            }
            string[] help = {
                "LET THE DINO JUMP ON WINDOWS, ICONS AND TEXT BOXES",
                "GLOWING LINES SHOW WHERE YOUR DESKTOP IS JUMPABLE",
                "", "", "", "WASD ALSO MOVES THE DINO WHILE PLAYING",
                "RETRO TV LINES ON THIS LAUNCHER", "WIPES LEVELS, TIMES AND AMBER", "" };
            Font.CenteredO(s, help[optSel], W / 2, H - 18, G.Hex(0xffd23a), Ink, 1);
        }

        void DrawHelp(Surface s)
        {
            Shade(s, 0, 0, W, H, 10);
            Ui.Box(s, 50, 22, 380, 226, G.Hex(0xffd23a));
            Font.Centered(s, "HOW TO PLAY", W / 2, 32, G.Hex(0xffffff), 2);
            string[] lines = {
                "< > / A D  .........  RUN",
                "SPACE / UP / W  ....  JUMP (AGAIN IN AIR!)",
                "DOWN / S  ..........  DROP THROUGH PLANKS",
                "ESC  ...............  PAUSE + FREE KEYBOARD",
                "R  .................  RESTART LEVEL",
                "",
                "YOUR DESKTOP IS PART OF THE LEVEL! THE DINO CAN",
                "LAND ON WINDOW TITLE BARS, DESKTOP ICONS, BUTTONS",
                "AND TEXT BOXES. DRAG A WINDOW UNDER THE DINO TO",
                "BUILD A BRIDGE - IT RIDES ALONG!",
                "",
                "JUMP INTO THE BLACK HOLE TO WIN. GRAB 3 AMBER PER",
                "LEVEL TO UNLOCK NEW GEAR IN THE WARDROBE.",
            };
            for (int i = 0; i < lines.Length; i++)
                Font.Text(s, lines[i], 72, 58 + i * 12, i < 5 ? G.Hex(0x3ee0c8) : G.Hex(0xf4ecff), 1);
            Font.Centered(s, "PRESS ANY KEY", W / 2, 230, ((t / 20) & 1) == 0 ? G.Hex(0xffd23a) : G.Hex(0x8a7a30), 1);
        }
    }
}
