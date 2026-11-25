using System;
using System.Collections.Generic;

namespace Deskosaur
{
    // The animated rainy-night rooftop bar behind the launcher menus.
    public sealed class LauncherScene
    {
        public const int W = 480, H = 270;
        const int Street = 226, LedgeY = 170, DinoX = 268, DinoY = LedgeY - BigDino.Ledge;
        Surface bg;
        byte[] cloud; const int CloudW = 960, CloudH = 120;
        Random rng = new Random(11);
        Particles fx = new Particles();
        int t;
        // rain
        const int NearN = 70, FarN = 150;
        float[] nx = new float[NearN], ny = new float[NearN], fxr = new float[FarN], fyr = new float[FarN];
        // ripples
        struct Ripple { public float X, Y; public int T; }
        List<Ripple> ripples = new List<Ripple>();
        // dino
        BigDino dino; int[] dinoTop;
        int blinkT = 120;
        // events
        int lightningT = -1, nextLightning = 400, thunderAt = -1;
        float carX = -999; int carDir = 1, nextCar = 300;
        int[] boltX = new int[14], boltY = new int[14];
        bool[] litWin = new bool[40];

        static readonly int Ink = G.Hex(0x07040c);

        public LauncherScene()
        {
            for (int i = 0; i < NearN; i++) { nx[i] = rng.Next(W + 80); ny[i] = rng.Next(H); }
            for (int i = 0; i < FarN; i++) { fxr[i] = rng.Next(W + 80); fyr[i] = rng.Next(H); }
            for (int i = 0; i < litWin.Length; i++) litWin[i] = rng.Next(3) == 0;
            BuildClouds();
            BuildBackground();
        }

        // ======================= static background =======================
        static uint Hash(int x, int y) { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h; }

        static float VNoise(float x, float y, int period)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            Func<int, int, float> v = delegate (int a, int b) { a = ((a % period) + period) % period; return (Hash(a, b) & 1023) / 1023f; };
            float a0 = v(xi, yi), a1 = v(xi + 1, yi), b0 = v(xi, yi + 1), b1 = v(xi + 1, yi + 1);
            return (a0 + (a1 - a0) * fx) + ((b0 + (b1 - b0) * fx) - (a0 + (a1 - a0) * fx)) * fy;
        }

        void BuildClouds()
        {
            cloud = new byte[CloudW * CloudH];
            for (int y = 0; y < CloudH; y++)
                for (int x = 0; x < CloudW; x++)
                {
                    float n = VNoise(x / 64f, y / 26f, CloudW / 64) * 0.55f + VNoise(x / 24f, y / 11f, CloudW / 24) * 0.3f + VNoise(x / 8f, y / 5f, CloudW / 8) * 0.15f;
                    float band = 1f - Math.Abs(y - 55) / 60f;
                    float v = (n - 0.42f) * 2.6f * band;
                    if (v < 0) v = 0; if (v > 1) v = 1;
                    cloud[y * CloudW + x] = (byte)(v * 16);
                }
        }

        void BuildBackground()
        {
            bg = new Surface(W, H);
            Surface s = bg;
            Random r = new Random(5);
            // ---- sky ----
            for (int y = 0; y < Street; y++)
            {
                float u = Math.Min(1f, y / 190f);
                int c1 = G.Mix(G.Hex(0x05030a), G.Hex(0x1e0f30), u), c2 = G.Mix(G.Hex(0x05030a), G.Hex(0x1e0f30), Math.Min(1, u + 0.05f));
                for (int x = 0; x < W; x++) s.P[y * W + x] = G.Dith(x, y, (int)((u * 20 % 1f) * 16)) ? c2 : c1;
            }
            // city glow on the horizon
            for (int y = 120; y < Street; y++)
                for (int x = 0; x < W; x++)
                    if (G.Dith(x, y, (y - 120) / 8)) s.P[y * W + x] = G.Mix(s.P[y * W + x], G.Hex(0x4a1a4a), 0.35f);
            for (int i = 0; i < 90; i++) { int x = r.Next(W), y = r.Next(110); G.Px(s, x, y, r.Next(5) == 0 ? G.Hex(0x9a8ac8) : G.Hex(0x3a2e5a)); }
            // ---- moon ----
            G.DiscD(s, 446, 38, 22, G.Hex(0x2a1640), 4);
            G.DiscD(s, 446, 38, 17, G.Hex(0x4a2a5a), 5);
            G.Disc(s, 446, 38, 12, G.Hex(0xe8c8d8));
            for (int y = -12; y <= 12; y++) for (int x = -12; x <= 12; x++)
                    if (x * x + y * y <= 156 && (x + 4) * (x + 4) + (y - 2) * (y - 2) > 60 && G.Dith(x, y, 6)) G.Px(s, 446 + x, 38 + y, G.Hex(0xc8a0bc));
            G.Disc(s, 442, 34, 2, G.Hex(0xc8a8c0)); G.Disc(s, 450, 42, 1, G.Hex(0xc8a8c0)); G.Px(s, 448, 31, G.Hex(0xc8a8c0));
            // ---- far skyline ----
            int x0 = 0;
            while (x0 < W)
            {
                int bw = 16 + r.Next(30), bh = 40 + r.Next(70), top = 190 - bh;
                G.Rect(s, x0, top, bw, Street - top, G.Hex(0x0d0918));
                for (int wy = top + 5; wy < 200; wy += 5)
                    for (int wx = x0 + 3; wx < x0 + bw - 2; wx += 4)
                        if (r.Next(9) == 0) G.Px(s, wx, wy, r.Next(3) == 0 ? G.Hex(0x3a2a18) : G.Hex(0x16303a));
                if (r.Next(3) == 0) { G.VLine(s, x0 + bw / 2, top - 12, top, G.Hex(0x0d0918)); }
                x0 += bw + r.Next(4);
            }
            // ---- mid skyline (closer, more detail) ----
            x0 = 180;
            while (x0 < 280)
            {
                int bw = 22 + r.Next(18), top = 120 + r.Next(30);
                G.Rect(s, x0, top, bw, Street - top, G.Hex(0x120c20));
                G.VLine(s, x0, top, Street, G.Hex(0x1c1430));
                for (int wy = top + 6; wy < 210; wy += 8)
                    for (int wx = x0 + 4; wx < x0 + bw - 4; wx += 6)
                    { G.Rect(s, wx, wy, 3, 4, G.Hex(0x0a0712)); if (r.Next(6) == 0) G.Rect(s, wx, wy, 3, 4, G.Hex(0x4a3418)); }
                // water tower
                if (r.Next(2) == 0) { int tx = x0 + bw / 2; G.Rect(s, tx - 5, top - 12, 10, 9, G.Hex(0x120c20)); G.VLine(s, tx - 4, top - 3, top, G.Hex(0x120c20)); G.VLine(s, tx + 3, top - 3, top, G.Hex(0x120c20)); G.Px(s, tx, top - 13, G.Hex(0x120c20)); }
                x0 += bw + 2;
            }
            LeftBuilding(s, r);
            Alley(s, r);
            BarBuilding(s, r);
            StreetLayer(s, r);
            // ---- vignette ----
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - W / 2f) / (W / 2f), dy = (y - H / 2f) / (H / 2f);
                    float v = dx * dx * 0.6f + dy * dy * 0.7f;
                    int lvl = (int)((v - 0.35f) * 14);
                    if (lvl > 0 && G.Dith(x, y, Math.Min(10, lvl))) s.P[y * W + x] = G.Mix(s.P[y * W + x], G.Hex(0x020104), 0.55f);
                }
        }

        void LeftBuilding(Surface s, Random r)
        {
            int bx1 = 196, top = 46;
            // brick wall
            for (int y = top; y < Street; y++)
                for (int x = 0; x < bx1; x++)
                {
                    int row = y / 4, off = (row & 1) * 4;
                    bool mortar = y % 4 == 3 || (x + off) % 8 == 7;
                    uint h = Hash((x + off) / 8, row);
                    int c = mortar ? G.Hex(0x0d0910) : (h % 5 == 0 ? G.Hex(0x24161e) : (h % 3 == 0 ? G.Hex(0x1a1018) : G.Hex(0x1f141c)));
                    if (Hash(x, y) % 11 == 0) c = G.Mix(c, G.Hex(0x000000), 0.4f);
                    s.P[y * W + x] = c;
                }
            // roofline + cornice
            G.Rect(s, 0, top - 4, bx1 + 2, 5, G.Hex(0x2a1c28)); G.HLine(s, 0, bx1 + 1, top - 4, G.Hex(0x4a3242)); G.HLine(s, 0, bx1 + 1, top, Ink);
            // windows grid
            int wi = 0;
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                {
                    int wx = 12 + col * 40, wy = top + 12 + row * 34;
                    G.Rect(s, wx - 2, wy - 2, 26, 22, G.Hex(0x0c080e));
                    G.Rect(s, wx, wy, 22, 18, G.Hex(0x080610));
                    G.Rect(s, wx - 3, wy + 19, 28, 2, G.Hex(0x3a2a34)); // sill
                    G.HLine(s, wx - 3, wx + 24, wy + 19, G.Hex(0x5a4250));
                    G.VLine(s, wx + 11, wy, wy + 17, G.Hex(0x0c080e));
                    // grime streak under the sill
                    int len = 4 + r.Next(14);
                    for (int k = 0; k < len; k++) for (int q = 0; q < 3; q++) if (G.Dith(wx + 4 + q * 7, wy + 21 + k, 10 - k / 2)) G.Px(s, wx + 4 + q * 7 + (k % 3 == 0 ? 1 : 0), wy + 21 + k, G.Hex(0x0e080c));
                    wi++;
                }
            // fire escape on the right side of the building
            int fe = G.Hex(0x2a2838), feH = G.Hex(0x4a4660);
            for (int lv = 0; lv < 4; lv++)
            {
                int py = top + 30 + lv * 34, px0 = 128, px1 = 194;
                G.Rect(s, px0, py, px1 - px0, 2, fe); G.HLine(s, px0, px1, py, feH);
                G.HLine(s, px0, px1, py - 8, fe);
                for (int x = px0; x <= px1; x += 5) G.VLine(s, x, py - 8, py, fe);
                // stairs
                if (lv < 3) { int sx0 = lv % 2 == 0 ? px0 + 6 : px1 - 26; for (int k = 0; k < 30; k++) { int xx = lv % 2 == 0 ? sx0 + k * 2 / 3 : sx0 + 20 - k * 2 / 3; G.Px(s, xx, py + 2 + k, fe); if (k % 4 == 0) G.HLine(s, xx - 2, xx + 2, py + 2 + k, fe); } }
            }
            G.VLine(s, 194, top + 20, top + 140, fe);
            // AC units
            int[] acx = { 30, 110, 70 }, acy = { 98, 66, 166 };
            for (int i = 0; i < 3; i++)
            {
                G.Rect(s, acx[i], acy[i], 16, 10, G.Hex(0x3a3444)); G.RectO(s, acx[i] - 1, acy[i] - 1, 18, 12, Ink);
                for (int k = 0; k < 6; k++) G.VLine(s, acx[i] + 2 + k * 2, acy[i] + 2, acy[i] + 7, G.Hex(0x22202a));
                G.HLine(s, acx[i], acx[i] + 15, acy[i], G.Hex(0x5a5468));
            }
            // shop shutter at street level + posters + graffiti
            G.Rect(s, 14, 196, 70, Street - 196, G.Hex(0x2a2a34));
            for (int y = 198; y < Street; y += 3) G.HLine(s, 14, 83, y, G.Hex(0x1a1a22));
            G.RectO(s, 13, 195, 72, Street - 194, Ink);
            int[] pc = { 0x6a2a3a, 0x2a4a5a, 0x6a5a2a, 0x4a2a5a };
            for (int i = 0; i < 4; i++)
            {
                int px = 92 + i * 22 + r.Next(4), py = 192 + r.Next(10);
                G.Rect(s, px, py, 16, 22, G.Mul(G.Hex(pc[i]), 0.8f));
                for (int k = 0; k < 5; k++) G.HLine(s, px + 2, px + 2 + r.Next(11), py + 3 + k * 3, G.Mul(G.Hex(pc[i]), 1.4f));
                for (int k = 0; k < 6; k++) G.Px(s, px + 16 - r.Next(4), py + 22 - r.Next(5), G.Hex(0x1f141c)); // torn corner
            }
            // graffiti scribble
            int gx = 24, gy = 172;
            for (int k = 0; k < 60; k++) { float a = k * 0.35f; G.Px(s, gx + k + (int)(Math.Sin(a) * 3), gy + (int)(Math.Cos(a * 1.3f) * 5), k % 9 < 6 ? G.Hex(0x7a2a6a) : G.Hex(0x2a7a7a)); }
            // rooftop antenna + satellite dish
            G.VLine(s, 150, 10, top - 4, G.Hex(0x1e1a28)); for (int k = 0; k < 4; k++) G.HLine(s, 146 + k, 154 - k, 14 + k * 6, G.Hex(0x1e1a28));
            G.Disc(s, 116, top - 12, 7, G.Hex(0x2a2636)); G.Disc(s, 117, top - 13, 5, G.Hex(0x3a3648)); G.Line(s, 116, top - 12, 110, top - 18, G.Hex(0x1e1a28));
            G.Rect(s, 112, top - 6, 8, 3, G.Hex(0x1e1a28));
            G.Rect(s, 30, top - 14, 22, 10, G.Hex(0x1a1422)); G.RectO(s, 29, top - 15, 24, 12, Ink);   // roof access hut
            // drainpipe
            G.Rect(s, 4, top, 3, Street - top, G.Hex(0x22202c)); for (int y = top; y < Street; y += 18) G.HLine(s, 3, 7, y, G.Hex(0x3a3648));
        }

        void Alley(Surface s, Random r)
        {
            for (int y = 60; y < Street; y++)
                for (int x = 196; x < 262; x++)
                    if (G.Dith(x, y, 6 + (y - 60) / 25)) s.P[y * W + x] = G.Mix(s.P[y * W + x], G.Hex(0x05030a), 0.6f);
            // dumpster
            G.Rect(s, 200, 204, 30, Street - 204, G.Hex(0x1e3a30)); G.Rect(s, 198, 202, 34, 4, G.Hex(0x2a4a3e)); G.RectO(s, 197, 201, 36, Street - 200, Ink);
            G.VLine(s, 210, 206, Street - 2, G.Hex(0x16302a)); G.VLine(s, 220, 206, Street - 2, G.Hex(0x16302a));
            // trash bags
            G.Disc(s, 236, 220, 5, G.Hex(0x141418)); G.Disc(s, 243, 222, 4, G.Hex(0x18181e)); G.Px(s, 234, 217, G.Hex(0x3a3a48)); G.Px(s, 242, 219, G.Hex(0x3a3a48));
            // vending machine (lit dynamically)
            G.Rect(s, 240, 180, 18, Street - 180, G.Hex(0x2a2a3a)); G.RectO(s, 239, 179, 20, Street - 178, Ink);
        }

        void BarBuilding(Surface s, Random r)
        {
            int x0 = 262;
            // facade
            for (int y = LedgeY + 10; y < Street; y++)
                for (int x = x0; x < W; x++)
                {
                    int c = G.Hex(0x0c1c22);
                    if (((x / 6 + y / 3) & 1) == 0 && Hash(x, y) % 4 == 0) c = G.Hex(0x10242c);
                    s.P[y * W + x] = c;
                }
            // big gridded windows
            for (int w = 0; w < 3; w++)
            {
                int wx = x0 + 12 + w * 70, wy = LedgeY + 18;
                G.Rect(s, wx, wy, 56, 22, G.Hex(0x0e2e36)); G.RectO(s, wx - 1, wy - 1, 58, 24, G.Hex(0x061014));
                for (int gx = 0; gx < 56; gx += 7) G.VLine(s, wx + gx, wy, wy + 21, G.Hex(0x0a1c22));
                for (int gy = 0; gy < 22; gy += 5) G.HLine(s, wx, wx + 55, wy + gy, G.Hex(0x0a1c22));
                G.Rect(s, wx - 2, wy + 22, 60, 2, G.Hex(0x1a3a40)); G.HLine(s, wx - 2, wx + 57, wy + 22, G.Hex(0x2a5a60));
            }
            // door
            G.Rect(s, x0 + 92, Street - 22, 18, 22, G.Hex(0x1a0e08)); G.RectO(s, x0 + 91, Street - 23, 20, 23, Ink);
            // gutter
            G.Rect(s, x0 + 2, LedgeY, 3, Street - LedgeY, G.Hex(0x1a2a30));
            // roof wall the dino leans on
            for (int y = LedgeY; y < LedgeY + 10; y++)
                for (int x = x0 - 4; x < W; x++)
                {
                    int row = (y - LedgeY) / 3, off = (row & 1) * 5;
                    bool mortar = (y - LedgeY) % 3 == 2 || (x + off) % 10 == 9;
                    int c = mortar ? G.Hex(0x110c1a) : (Hash((x + off) / 10, row) % 3 == 0 ? G.Hex(0x2a2034) : G.Hex(0x241c2e));
                    s.P[y * W + x] = c;
                }
            G.HLine(s, x0 - 4, W, LedgeY + 10, Ink);
            // rain-stained streaks running down the facade
            for (int x = x0; x < W; x += 3) { int len = (int)(Hash(x, 9) % 14); for (int k = 0; k < len; k++) if (G.Dith(x, LedgeY + 11 + k, 8 - k / 2)) G.Px(s, x, LedgeY + 11 + k, G.Hex(0x081418)); }
        }

        void StreetLayer(Surface s, Random r)
        {
            // sidewalk
            for (int y = Street; y < Street + 7; y++)
                for (int x = 0; x < W; x++) s.P[y * W + x] = (x % 18 == 0 || y == Street + 6) ? G.Hex(0x120c12) : G.Hex(0x221820);
            G.HLine(s, 0, W, Street, G.Hex(0x3a2a34));
            // road
            for (int y = Street + 7; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int c = G.Hex(0x0a0810);
                    uint h = Hash(x, y);
                    if (h % 13 == 0) c = G.Hex(0x100c16); else if (h % 41 == 0) c = G.Hex(0x18141f);
                    s.P[y * W + x] = c;
                }
            for (int x = 6; x < W; x += 30) G.Rect(s, x, 254, 16, 2, G.Hex(0x2a2630));
            for (int i = 0; i < 6; i++) for (int y = Street + 9; y < Street + 24; y++) for (int x = 330 + i * 12; x < 336 + i * 12; x++) if (Hash(x, y) % 4 != 0) G.Px(s, x, y, G.Hex(0x1e1a26));
            // puddles (darker, glossy)
            DrawPuddle(s, 120, 246, 28, 4); DrawPuddle(s, 300, 262, 36, 4); DrawPuddle(s, 430, 244, 22, 3);
            // manhole
            for (int y = -3; y <= 3; y++) for (int x = -9; x <= 9; x++) if (x * x / 81f + y * y / 9f <= 1) G.Px(s, 186 + x, 260 + y, (x + y) % 3 == 0 ? G.Hex(0x1e1a24) : G.Hex(0x141018));
            // hydrant
            int hx = 282, hy = 210;
            G.Rect(s, hx, hy + 3, 8, 13, G.Hex(0x8a1820)); G.Rect(s, hx - 1, hy, 10, 4, G.Hex(0xa82028)); G.Rect(s, hx - 3, hy + 7, 14, 3, G.Hex(0x6a1018));
            G.RectO(s, hx - 1, hy - 1, 10, 17, Ink); G.VLine(s, hx + 1, hy + 4, hy + 13, G.Hex(0xc8404a));
        }

        static void DrawPuddle(Surface s, int cx, int cy, int rx, int ry)
        {
            for (int y = -ry; y <= ry; y++) for (int x = -rx; x <= rx; x++)
                    if (x * x / (float)(rx * rx) + y * y / (float)(ry * ry) <= 1) G.Px(s, cx + x, cy + y, G.Hex(0x06050c));
        }

        // ======================= per-frame =======================
        public void Render(Surface s, Outfit look)
        {
            t++;
            if (dino == null) { dino = new BigDino(); ComputeDinoTop(); }
            G.Copy(bg, s);
            Sky(s);
            Signs(s);
            Reflections(s);
            Steam(s);
            Car(s, false);
            Cat(s);
            DinoAndBar(s);
            Car(s, true);
            Rain(s);
            fx.Update(); fx.Draw(s, t);
            Fog(s);
            Lightning(s);
            Grain(s);
        }

        void Sky(Surface s)
        {
            int off = (int)(t * 0.12f);
            int c1 = G.Hex(0x221636), c2 = G.Hex(0x34224a), c3 = G.Hex(0x4c3462);
            for (int y = 0; y < CloudH; y++)
            {
                int o = y * CloudW;
                for (int x = 0; x < W; x++)
                {
                    int lvl = cloud[o + ((x + off) % CloudW)];
                    if (lvl == 0) continue;
                    int bgc = s.P[y * W + x];
                    if (bgc != 0 && !IsSkyish(bgc)) continue;
                    int c = lvl > 11 ? c3 : (lvl > 6 ? c2 : c1);
                    if (G.Dith(x, y, Math.Min(16, lvl * 2))) s.P[y * W + x] = c;
                }
            }
            // searchlights
            for (int k = 0; k < 2; k++)
            {
                float ang = (float)Math.Sin(t * 0.006f + k * 2.2f) * 0.6f - 1.5708f;
                int ox = k == 0 ? 120 : 360, oy = 160;
                float dx = (float)Math.Cos(ang), dy = (float)Math.Sin(ang);
                for (int i = 10; i < 180; i++)
                {
                    float px = ox + dx * i, py = oy + dy * i;
                    int half = 1 + i / 22;
                    for (int w = -half; w <= half; w++)
                    {
                        int qx = (int)(px - dy * w), qy = (int)(py + dx * w);
                        if (qy < 0 || qy >= H || qx < 0 || qx >= W) continue;
                        int c = s.P[qy * W + qx];
                        if (!IsSkyish(c)) continue;
                        if (G.Dith(qx, qy, 3 - i / 70)) s.P[qy * W + qx] = G.Mix(c, G.Hex(0x9a8ac8), 0.35f);
                    }
                }
            }
            // blinking antenna lights
            if ((t / 40) % 2 == 0) { G.Px(s, 61, 82, G.Hex(0xff2040)); G.Px(s, 233, 104, G.Hex(0xff2040)); }
        }

        static bool IsSkyish(int c)
        {
            int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            return r < 60 && g < 40 && b < 80 && b >= r - 4 && (r + g + b) > 8;
        }

        void Signs(Surface s)
        {
            // vertical blade sign on the left building: cocktail glass icon + a dino head, pink neon with a dying tube
            bool dead = (t % 420) > 380 && ((t / 3) % 3 != 0);
            int pink = G.Hex(0xff3aa8), pinkD = G.Hex(0x4a1434), teal = G.Hex(0x3ee0c8), tealD = G.Hex(0x0e3a38);
            int sx = 203, sy = 84;
            G.Rect(s, sx, sy, 20, 64, G.Hex(0x0a0610)); G.RectO(s, sx - 1, sy - 1, 22, 66, G.Hex(0x2a2438));
            G.HLine(s, sx - 8, sx, sy + 6, G.Hex(0x3a3448)); G.HLine(s, sx - 8, sx, sy + 56, G.Hex(0x3a3448));
            int frame = dead ? pinkD : pink;
            G.RectO(s, sx + 2, sy + 2, 16, 60, frame);
            if (!dead) G.RectD(s, sx - 6, sy - 6, 32, 76, G.Hex(0x5a1a48), 3);
            G.RectO(s, sx + 2, sy + 2, 16, 60, frame);
            // cocktail glass
            int c = (t / 50) % 2 == 0 ? teal : tealD;
            for (int i = 0; i < 6; i++) { G.Px(s, sx + 4 + i, sy + 8 + i, c); G.Px(s, sx + 15 - i, sy + 8 + i, c); }
            G.HLine(s, sx + 4, sx + 15, sy + 8, c); G.VLine(s, sx + 9, sy + 14, sy + 19, c); G.VLine(s, sx + 10, sy + 14, sy + 19, c); G.HLine(s, sx + 6, sx + 13, sy + 20, c);
            G.Disc(s, sx + 13, sy + 9, 1, dead ? pinkD : pink);
            // dino head outline
            int dc = dead ? pinkD : pink;
            int[] head = { 4, 40, 5, 36, 8, 33, 12, 32, 15, 34, 16, 38, 15, 42, 11, 43, 11, 47, 8, 50, 5, 50 };
            for (int i = 0; i + 3 < head.Length; i += 2) G.Line(s, sx + head[i], sy + head[i + 1], sx + head[i + 2], sy + head[i + 3], dc);
            G.Px(s, sx + 11, sy + 36, dc);
            // pink neon tube along the bar's roof wall, washing light down the facade
            bool flick = (t % 300) > 286 && (t % 4) < 2;
            int trim = flick ? G.Hex(0x3a1030) : G.Hex(0xff4ab8);
            G.HLine(s, 258, W, LedgeY + 11, trim); G.HLine(s, 258, W, LedgeY + 12, flick ? G.Hex(0x200818) : G.Hex(0xff9ad8));
            if (!flick)
                for (int y = LedgeY + 13; y < LedgeY + 24; y++)
                    for (int x = 258; x < W; x++)
                        if (G.Dith(x, y, 8 - (y - LedgeY - 13))) { int b = G.GetPx(s, x, y); G.Px(s, x, y, G.Mix(b, G.Hex(0xff4ab8), 0.3f)); }
            Billboard(s);
            StringLights(s);
            // bar windows glow (slow breathing) + silhouettes
            for (int w = 0; w < 3; w++)
            {
                int wx = 274 + w * 70, wy = LedgeY + 18;
                float pulse = 0.5f + 0.5f * (float)Math.Sin(t * 0.02f + w);
                for (int y = wy + 1; y < wy + 22; y++) for (int x = wx + 1; x < wx + 56; x++)
                        if ((x - wx) % 7 != 0 && (y - wy) % 5 != 0 && G.Dith(x, y, 5 + (int)(pulse * 5))) G.Px(s, x, y, G.Hex(0x247a82));
                if (w == 1) { G.Disc(s, wx + 20, wy + 18, 4, G.Hex(0x06141a)); G.Rect(s, wx + 16, wy + 21, 9, 5, G.Hex(0x06141a)); }
            }
            // door light
            G.RectD(s, 355, Street - 21, 16, 21, G.Hex(0xffa040), 6);
            // vending machine
            int vx = 241, vy = 181;
            G.Rect(s, vx + 1, vy + 2, 10, 20, G.Hex(0xc8e8f0));
            for (int i = 0; i < 4; i++) for (int j = 0; j < 3; j++) G.Rect(s, vx + 2 + j * 3, vy + 4 + i * 5, 2, 3, (i + j + t / 90) % 3 == 0 ? G.Hex(0xff5a5a) : ((i + j) % 2 == 0 ? G.Hex(0x3a8ad0) : G.Hex(0xe8c040)));
            G.Rect(s, vx + 12, vy + 4, 4, 8, G.Hex(0x1a1a2a)); G.Px(s, vx + 13, vy + 6, (t / 30) % 2 == 0 ? G.Hex(0x40ff80) : G.Hex(0x105020));
            G.RectD(s, vx - 6, vy - 4, 30, 50, G.Hex(0x6aa8c0), 2);
            // street lamp
            bool lampOff = (t % 520) > 505 && (t % 3) == 0;
            int lx = 262;
            G.VLine(s, lx - 1, 118, Street, G.Hex(0x3a3648)); G.VLine(s, lx, 118, Street, G.Hex(0x22202c)); G.VLine(s, lx + 1, 118, Street, G.Hex(0x14121a));
            G.Rect(s, lx - 2, Street - 6, 5, 6, G.Hex(0x22202c));
            G.Line(s, lx, 118, lx - 10, 112, G.Hex(0x22202c)); G.Rect(s, lx - 16, 110, 10, 4, G.Hex(0x22202c));
            if (!lampOff)
            {
                G.HLine(s, lx - 15, lx - 8, 114, G.Hex(0xfff0b0));
                for (int y = 115; y < Street; y++)
                {
                    int half = (y - 115) / 3 + 2;
                    for (int x = lx - 11 - half; x <= lx - 11 + half; x++)
                        if (G.Dith(x, y, 2)) { int b = G.GetPx(s, x, y); G.Px(s, x, y, G.Mix(b, G.Hex(0xffe0a0), 0.22f)); }
                }
                G.DiscD(s, lx - 11, 114, 8, G.Hex(0xffe0a0), 3);
            }
        }

        void Billboard(Surface s)
        {
            int bx = 296, by = 50, bw = 70, bh = 30;
            G.VLine(s, bx + 12, by + bh, by + bh + 26, G.Hex(0x16121e)); G.VLine(s, bx + bw - 12, by + bh, by + bh + 26, G.Hex(0x16121e));
            for (int k = 0; k < 4; k++) G.Line(s, bx + 12, by + bh + k * 6, bx + bw - 12, by + bh + 6 + k * 6, G.Hex(0x16121e));
            G.Rect(s, bx - 2, by - 2, bw + 4, bh + 4, G.Hex(0x1a1624));
            bool glitch = (t % 360) > 340;
            for (int y = 0; y < bh; y++)
            {
                int shift = glitch && ((y + t) % 7 < 2) ? ((t * 7 + y) % 9) - 4 : 0;
                for (int x = 0; x < bw; x++)
                {
                    float u = y / (float)bh;
                    int c = G.Mix(G.Hex(0x2a0e4a), G.Hex(0xc0306a), u);
                    if ((y & 1) == 1) c = G.Mul(c, 0.8f);
                    // ringed planet
                    float px = x - 46 - shift, py = y - 14;
                    if (px * px + py * py < 64) c = G.Mix(G.Hex(0xffb070), G.Hex(0xff5a8a), (py + 8) / 16f);
                    float rx = px * 0.98f - py * 0.2f, ry = (py * 0.98f + px * 0.2f) * 3.2f;
                    float rr = rx * rx + ry * ry;
                    if (rr > 120 && rr < 190 && !(px * px + py * py < 64 && ry < 0)) c = G.Hex(0xffe0a0);
                    // tiny dino silhouette on a cliff
                    if (y > 22 && x < 30 + (int)(Math.Sin(x * 0.3) * 2)) c = G.Hex(0x14061e);
                    if (y > 15 && y < 23 && x > 10 && x < 22 && ((x - 10) * 0.6f + (22 - y)) < 9 && !(y < 18 && x < 15)) c = G.Hex(0x14061e);
                    int sx2 = bx + x + shift;
                    G.Px(s, sx2, by + y, c);
                }
            }
            if (glitch) for (int k = 0; k < 6; k++) G.HLine(s, bx + rng.Next(bw - 20), bx + rng.Next(bw), by + rng.Next(bh), G.Hex(0x3ee0c8));
            G.RectO(s, bx - 2, by - 2, bw + 4, bh + 4, Ink);
            for (int k = 0; k < 5; k++) G.Px(s, bx + 6 + k * 14, by - 3, (t / 20 + k) % 3 == 0 ? G.Hex(0xfff0b0) : G.Hex(0x4a4030));
            for (int y = by + bh + 2; y < by + bh + 10; y++) for (int x = bx; x < bx + bw; x++) if (G.Dith(x, y, 3 - (y - by - bh) / 3)) { int b = G.GetPx(s, x, y); if (IsSkyish(b)) G.Px(s, x, y, G.Mix(b, G.Hex(0xc0306a), 0.3f)); }
        }

        void StringLights(Surface s)
        {
            int x0 = 196, y0 = 116, x1 = 258, y1 = 120;
            for (int i = 0; i <= 62; i++)
            {
                float u = i / 62f;
                int x = x0 + i, y = (int)(y0 + (y1 - y0) * u + Math.Sin(u * Math.PI) * 9);
                G.Px(s, x, y, G.Hex(0x14101c));
                if (i % 6 == 3)
                {
                    bool on = ((t / 25 + i) % 7) != 0;
                    int c = (i / 6) % 3 == 0 ? G.Hex(0xffd070) : ((i / 6) % 3 == 1 ? G.Hex(0xff6a9a) : G.Hex(0x6af0e0));
                    G.Px(s, x, y + 1, on ? c : G.Mul(c, 0.3f)); G.Px(s, x, y + 2, on ? G.Mix(c, G.Hex(0xffffff), 0.5f) : G.Mul(c, 0.3f));
                    if (on) for (int k = 0; k < 8; k++) { int gx = x + (k % 3) - 1, gy = y + 1 + k / 3; if (G.Dith(gx, gy, 6)) { int b = G.GetPx(s, gx - 1, gy + 1); G.Px(s, gx - 1, gy + 1, G.Mix(b, c, 0.3f)); } }
                }
            }
        }

        void Reflections(Surface s)
        {
            // wobbly neon reflections on the wet road
            Refl(s, 213, 8, G.Hex(0xff3aa8), 0.5f);
            Refl(s, 249, 6, G.Hex(0x9ad8f0), 0.45f);
            Refl(s, 251, 4, G.Hex(0xffe0a0), 0.35f);
            for (int w = 0; w < 3; w++) Refl(s, 302 + w * 70, 22, G.Hex(0x1e6a72), 0.35f);
            Refl(s, 363, 6, G.Hex(0xffa040), 0.35f);
            Refl(s, 446, 7, G.Hex(0xc8a0bc), 0.18f);
            // ripples
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                Ripple rp = ripples[i]; rp.T++;
                if (rp.T > 18) { ripples.RemoveAt(i); continue; }
                ripples[i] = rp;
                int rx = 1 + rp.T / 3, ry = Math.Max(1, rx / 3);
                for (int a = 0; a < 16; a++)
                {
                    float an = a * 0.3927f;
                    G.Px(s, (int)(rp.X + Math.Cos(an) * rx), (int)(rp.Y + Math.Sin(an) * ry), rp.T < 9 ? G.Hex(0x4a4062) : G.Hex(0x2a2440));
                }
            }
        }

        void Refl(Surface s, int cx, int halfW, int col, float k)
        {
            for (int y = Street + 8; y < H; y++)
            {
                float fall = 1f - (y - Street - 8) / 46f;
                int wob = (int)Math.Round(Math.Sin(y * 0.8f + t * 0.15f) * 1.5f + Math.Sin(y * 0.23f - t * 0.05f));
                int lvl = (int)(fall * 9 * k * 2);
                for (int x = cx - halfW; x <= cx + halfW; x++)
                {
                    int edge = halfW - Math.Abs(x - cx);
                    if (edge < 1 && (y & 1) == 0) continue;
                    if ((y + t / 6) % 5 == 0) continue;
                    if (G.Dith(x + wob, y, lvl + 2)) { int b = G.GetPx(s, x + wob, y); G.Px(s, x + wob, y, G.Mix(b, col, 0.75f)); }
                }
            }
        }

        void Steam(Surface s)
        {
            if (t % 7 == 0) fx.Spawn(PT.Dust, 186 + Particles.R(-6, 6), 258, Particles.R(-0.15f, 0.15f), -Particles.R(0.25f, 0.5f), 90, G.Hex(0x4a4258), 2, -0.002f);
            if (t % 11 == 0) fx.Spawn(PT.Dust, 232 + Particles.R(-2, 2), 176, Particles.R(0.05f, 0.25f), -Particles.R(0.15f, 0.35f), 70, G.Hex(0x3a3448), 1, 0);
            // drips from AC units / gutters
            if (t % 37 == 0) fx.Spawn(PT.Spark, 38, 109, 0, 0.5f, 60, G.Hex(0x6a7aa8), 1, 0.15f);
            if (t % 53 == 0) fx.Spawn(PT.Spark, 118, 77, 0, 0.5f, 90, G.Hex(0x6a7aa8), 1, 0.15f);
            if (t % 29 == 0) fx.Spawn(PT.Spark, 265, LedgeY + 2, 0, 0.6f, 50, G.Hex(0x6a7aa8), 1, 0.15f);
        }

        void Cat(Surface s)
        {
            // little alley cat on the fire escape
            int cx = 150, cy = 110;
            int k = G.Hex(0x05030a);
            G.Rect(s, cx, cy - 5, 8, 5, k); G.Rect(s, cx + 6, cy - 9, 5, 5, k);
            G.Px(s, cx + 6, cy - 10, k); G.Px(s, cx + 10, cy - 10, k);
            int tail = (int)Math.Round(Math.Sin(t * 0.05f) * 2);
            G.Line(s, cx, cy - 2, cx - 4, cy - 6 + tail, k); G.Px(s, cx - 5, cy - 7 + tail, k);
            bool blink = (t % 230) < 7;
            if (!blink) { G.Px(s, cx + 7, cy - 7, G.Hex(0xe8e040)); G.Px(s, cx + 9, cy - 7, G.Hex(0xe8e040)); }
        }

        void ComputeDinoTop()
        {
            Sprite f = dino.Frames[0];
            dinoTop = new int[BigDino.CW];
            for (int x = 0; x < BigDino.CW; x++)
            {
                dinoTop[x] = -1;
                int lx = x / BigDino.S; if (lx >= f.W) continue;
                for (int y = 0; y < BigDino.LLedge; y++) if (f.P[y * f.W + lx] != 0) { dinoTop[x] = y * BigDino.S; break; }
            }
        }

        void DinoAndBar(Surface s)
        {
            int beat = (t / 42) % 2;
            if (--blinkT < 0) blinkT = 160 + rng.Next(200);
            int eye = blinkT < 5 ? 2 : (blinkT < 9 ? 1 : ((t / 600) % 3 == 1 ? 1 : 0));
            Sprite f = dino.Frames[beat * 3 + eye];
            // body at 2x (chunky pixels), clipped at the ledge
            int S2 = BigDino.S;
            for (int y = 0; y <= BigDino.LLedge; y++)
                for (int x = 0; x < f.W; x++)
                {
                    int c = f.P[y * f.W + x]; if (c == 0) continue;
                    G.Rect(s, DinoX + x * S2, DinoY + y * S2, S2, S2, c);
                }
            // ledge top drawn over the body, then the arm resting on it
            G.Rect(s, 258, LedgeY, W - 258, 3, G.Hex(0x2a2236));
            for (int x = 258; x < W; x++) if ((x + t / 10) % 23 < 3) G.Px(s, x, LedgeY, G.Hex(0x6a5a82));
            G.HLine(s, 258, W, LedgeY + 3, G.Hex(0x120e1c));
            G.BlitScaled(s, dino.Arm, DinoX, DinoY, BigDino.S, false);
            // the drink
            int gx = 286, gy = LedgeY - 17;
            G.RectD(s, gx - 6, gy - 8, 26, 26, G.Hex(0x2a8a8a), 3);
            for (int y = 0; y < 17; y++)
                for (int x = 0; x < 12; x++)
                {
                    int w2 = y > 13 ? 1 : 0;
                    if (x < w2 || x > 11 - w2) continue;
                    int c = y < 3 ? G.Hex(0xd8fff4) : (x < 3 ? G.Hex(0x7af0e0) : G.Hex(0x3ac8c0));
                    if (y > 4 && (x == 6 || y % 5 == 0) && x > 2 && x < 10) c = G.Hex(0x5ae0d8);
                    G.Px(s, gx + x, gy + y, c);
                }
            G.RectO(s, gx - 1, gy - 1, 14, 19, Ink);
            G.Rect(s, gx + 2, gy + 5, 3, 3, G.Hex(0xe8fff8)); G.Rect(s, gx + 6, gy + 9, 3, 3, G.Hex(0xe8fff8));   // ice cubes
            G.Line(s, gx + 8, gy - 8, gx + 6, gy + 6, G.Hex(0xb060ff)); G.Line(s, gx + 9, gy - 8, gx + 7, gy + 6, G.Hex(0x7a30c0));
            G.Disc(s, gx + 11, gy, 2, G.Hex(0x8ad040)); G.Px(s, gx + 11, gy, G.Hex(0xd8ff90));                    // lime
            if (t % 6 == 0) fx.Spawn(PT.Spark, gx + 2 + rng.Next(8), gy + 14, 0, -0.32f, 34, G.Hex(0xe8fff8), 1, 0);
            if (t % 19 == 0) fx.Spawn(PT.Glint, gx + rng.Next(12), gy - 3 - rng.Next(8), 0, -0.25f, 18, G.Hex(0xc8fff0), 1, 0);
            // breath fog from the nostrils now and then
            if (t % 140 < 30 && t % 5 == 0) fx.Spawn(PT.Dust, DinoX + 38, DinoY + 70 + beat, -0.35f, -0.08f, 40, G.Hex(0x3a3450), 1, 0);
        }

        void Car(Surface s, bool front)
        {
            if (front) return;
            if (carX < -900) { if (--nextCar <= 0) { carDir = rng.Next(2) == 0 ? 1 : -1; carX = carDir > 0 ? -90 : W + 90; } return; }
            carX += carDir * 3.2f;
            if (carX < -120 || carX > W + 120) { carX = -999; nextCar = 500 + rng.Next(900); return; }
            int x = (int)carX, y = 236;
            bool flip = carDir < 0;
            int body = G.Hex(0x1a1a2e), win = G.Hex(0x223a4a);
            // headlight beam
            int hx = flip ? x - 34 : x + 34;
            for (int k = 0; k < 70; k++)
            {
                int bx = hx + (flip ? -k : k);
                for (int yy = -k / 7; yy <= k / 7 + 2; yy++) if (G.Dith(bx, y + 6 + yy, 4 - k / 20)) { int b = G.GetPx(s, bx, y + 6 + yy); G.Px(s, bx, y + 6 + yy, G.Mix(b, G.Hex(0xfff0c0), 0.4f)); }
            }
            G.Rect(s, x - 34, y, 68, 12, body); G.Rect(s, x - 18, y - 9, 34, 10, body);
            G.Rect(s, x - 15, y - 7, 13, 7, win); G.Rect(s, x + 1, y - 7, 13, 7, win);
            G.HLine(s, x - 34, x + 33, y, G.Hex(0x3a3a5a)); G.HLine(s, x - 18, x + 15, y - 9, G.Hex(0x3a3a5a));
            G.RectO(s, x - 35, y - 1, 70, 14, Ink);
            G.Disc(s, x - 22, y + 12, 5, Ink); G.Disc(s, x + 22, y + 12, 5, Ink); G.Disc(s, x - 22, y + 12, 2, G.Hex(0x3a3a48)); G.Disc(s, x + 22, y + 12, 2, G.Hex(0x3a3a48));
            G.Rect(s, flip ? x - 35 : x + 31, y + 3, 4, 3, G.Hex(0xfff6d0));
            G.Rect(s, flip ? x + 32 : x - 35, y + 3, 3, 3, G.Hex(0xff2040));
            // red glow reflection behind
            for (int k = 0; k < 14; k++) { int rx = flip ? x + 36 + k : x - 38 - k; if (G.Dith(rx, y + 16, 5 - k / 3)) G.Px(s, rx, y + 16, G.Hex(0x8a1020)); }
        }

        void Rain(Surface s)
        {
            int far = G.Hex(0x2e3454), near = G.Hex(0x5a6a98), nearT = G.Hex(0x3a4670);
            for (int i = 0; i < FarN; i++)
            {
                fxr[i] -= 0.9f; fyr[i] += 3.6f;
                if (fyr[i] > H) { fyr[i] = -rng.Next(40); fxr[i] = rng.Next(W + 80); }
                int x = (int)fxr[i], y = (int)fyr[i];
                G.Px(s, x, y, far); G.Px(s, x + 1, y - 3, far);
            }
            for (int i = 0; i < NearN; i++)
            {
                nx[i] -= 1.6f; ny[i] += 7f;
                int x = (int)nx[i], y = (int)ny[i];
                int ground = Street + 4 + (i * 7) % 40;
                bool hit = false;
                int dxl = x - DinoX;
                if (dxl >= 0 && dxl < dinoTop.Length && dinoTop[dxl] >= 0 && y >= DinoY + dinoTop[dxl] && y - 7 < DinoY + dinoTop[dxl]) hit = true;
                if (!hit && x > 258 && y >= LedgeY && y - 7 < LedgeY) hit = true;
                if (!hit && y > ground) hit = true;
                if (hit)
                {
                    int hy = y;
                    if (y > Street) { if (rng.Next(3) == 0) { Ripple rp = new Ripple(); rp.X = x; rp.Y = y; rippleAdd(rp); } }
                    fx.Spawn(PT.Spark, x, hy - 1, Particles.R(-0.7f, 0.7f), -Particles.R(0.4f, 1.1f), 9, G.Hex(0x8a9ac8), 1, 0.2f);
                    ny[i] = -rng.Next(30); nx[i] = rng.Next(W + 80);
                    continue;
                }
                G.Px(s, x, y, near); G.Px(s, x, y - 1, near); G.Px(s, x + 1, y - 3, nearT); G.Px(s, x + 1, y - 4, nearT); G.Px(s, x + 2, y - 6, G.Hex(0x2a3458));
            }
        }
        void rippleAdd(Ripple r) { if (ripples.Count < 40) ripples.Add(r); }

        void Fog(Surface s)
        {
            for (int y = 196; y < 238; y++)
            {
                float band = 1f - Math.Abs(y - 220) / 20f;
                for (int x = 0; x < W; x++)
                {
                    float n = VNoiseFast(x + t * 0.3f, y);
                    int lvl = (int)(band * n * 4);
                    if (lvl > 0 && G.Dith(x, y, lvl)) { int b = s.P[y * W + x]; s.P[y * W + x] = G.Mix(b, G.Hex(0x3a3050), 0.5f); }
                }
            }
        }

        float VNoiseFast(float x, int y)
        {
            int xi = (int)x; float f = x - xi;
            float a = (Hash(xi / 40, y / 8) & 255) / 255f, b = (Hash(xi / 40 + 1, y / 8) & 255) / 255f;
            float u = (xi % 40 + f) / 40f;
            return a + (b - a) * u;
        }

        void Lightning(Surface s)
        {
            if (lightningT < 0)
            {
                if (--nextLightning <= 0)
                {
                    lightningT = 0; nextLightning = 500 + rng.Next(900);
                    int x = 330 + rng.Next(140), y = 0;
                    for (int i = 0; i < boltX.Length; i++) { boltX[i] = x; boltY[i] = y; x += rng.Next(15) - 7; y += 6 + rng.Next(7); }
                    thunderAt = 30 + rng.Next(40);
                }
            }
            else
            {
                lightningT++;
                int lvl = lightningT < 3 ? 7 : (lightningT < 6 ? 2 : (lightningT < 9 ? 5 : (lightningT < 16 ? 1 : 0)));
                if (lvl > 0)
                    for (int y = 0; y < 180; y++)
                        for (int x = 0; x < W; x++)
                            if (G.Dith(x, y, lvl) && IsSkyish(s.P[y * W + x])) s.P[y * W + x] = G.Hex(0x5a4a7a);
                if (lightningT < 5 || (lightningT > 6 && lightningT < 9))
                    for (int i = 0; i + 1 < boltX.Length; i++)
                    {
                        G.Line(s, boltX[i], boltY[i], boltX[i + 1], boltY[i + 1], G.Hex(0xf0e8ff));
                        G.Line(s, boltX[i] + 1, boltY[i], boltX[i + 1] + 1, boltY[i + 1], G.Hex(0x9a8ae0));
                    }
                if (lightningT == thunderAt) Sfx.Play("thunder");
                if (lightningT > 120) lightningT = -1;
            }
        }

        void Grain(Surface s)
        {
            for (int i = 0; i < 500; i++)
            {
                int x = rng.Next(W), y = rng.Next(H);
                if (x >= DinoX && y >= DinoY && y < LedgeY + 4 && x - DinoX < dinoTop.Length && dinoTop[x - DinoX] >= 0 && y >= DinoY + dinoTop[x - DinoX] - 2) continue;
                int c = s.P[y * W + x];
                s.P[y * W + x] = (i & 1) == 0 ? G.Mul(c, 1.22f) : G.Mul(c, 0.78f);
            }
        }
    }
}
