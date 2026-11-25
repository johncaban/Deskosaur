using System;

namespace Deskosaur
{
    public static class Tiles
    {
        static readonly int K = G.Hex(0x120c1c);

        static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }

        static bool Solidish(World w, int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= World.Cols || ty >= World.Rows) return false;
            char c = w.Tiles[tx, ty];
            return c == '#';
        }

        public static void DrawStatic(Surface s, World w)
        {
            for (int ty = 0; ty < World.Rows; ty++)
                for (int tx = 0; tx < World.Cols; tx++)
                {
                    char c = w.Tiles[tx, ty];
                    int x = (int)w.TX(tx), y = (int)w.TY(ty);
                    switch (c)
                    {
                        case '#': Block(s, w, tx, ty, x, y); break;
                        case '~': Ice(s, w, tx, ty, x, y); break;
                        case '=': Plank(s, w, tx, ty, x, y, false); break;
                        case '-': Plank(s, w, tx, ty, x, y, true); break;
                        case '^': Spikes(s, x, y, false); break;
                        case 'v': Spikes(s, x, y, true); break;
                    }
                }
        }

        // ---------------- themed solid blocks ----------------
        static void Block(Surface s, World w, int tx, int ty, int x, int y)
        {
            int th = w.Theme;
            bool up = Solidish(w, tx, ty - 1) || (ty > 0 && (w.Tiles[tx, ty - 1] == '~'));
            bool dn = Solidish(w, tx, ty + 1), lf = Solidish(w, tx - 1, ty), rt = Solidish(w, tx + 1, ty);
            for (int j = 0; j < 16; j++)
                for (int i = 0; i < 16; i++)
                {
                    int ax = tx * 16 + i, ay = ty * 16 + j;
                    int c;
                    switch (th)
                    {
                        case 0:
                            {
                                int row = ay / 6, off = (row & 1) * 8;
                                bool mortar = (ay % 6 == 5) || ((ax + off) % 16 == 15);
                                uint h = Hash((ax + off) / 16, row);
                                int baseC = (h % 5 == 0) ? G.Hex(0x382c58) : (h % 7 == 0 ? G.Hex(0x2a2246) : G.Hex(0x30284e));
                                c = mortar ? G.Hex(0x1a1430) : ((ay % 6 == 0) ? G.Mul(baseC, 1.25f) : baseC);
                                break;
                            }
                        case 1:
                            {
                                int bx = (ax + ((ay / 8) & 1) * 4) % 8, by = ay % 8;
                                bool mortar = bx == 7 || by == 7;
                                uint h = Hash((ax + ((ay / 8) & 1) * 4) / 8, ay / 8);
                                int baseC = (h % 3 == 0) ? G.Hex(0x3c5a86) : G.Hex(0x34507a);
                                c = mortar ? G.Hex(0x1c2c4c) : (by == 0 || bx == 0 ? G.Mul(baseC, 1.2f) : baseC);
                                break;
                            }
                        case 2:
                            {
                                c = G.Hex(0x4e5566);
                                if (i == 1 || j == 1) c = G.Hex(0x6c7488);
                                if (i == 14 || j == 14) c = G.Hex(0x383d4a);
                                if ((i == 3 || i == 12) && (j == 3 || j == 12)) c = G.Hex(0xaab2c4);
                                if ((i == 3 || i == 12) && (j == 4 || j == 13)) c = G.Hex(0x2a2e38);
                                break;
                            }
                        default:
                            {
                                uint h = Hash(ax, ay);
                                c = (h % 9 == 0) ? G.Hex(0x1c1230) : G.Hex(0x281c40);
                                if (h % 97 == 0) c = G.Hex(0x8a5ae8);
                                else if (h % 211 == 0) c = G.Hex(0xff6ad0);
                                uint h2 = Hash(ax / 5, ay / 5);
                                if (h2 % 4 == 0 && c != G.Hex(0x8a5ae8)) c = G.Mul(c, 1.25f);
                                break;
                            }
                    }
                    G.Px(s, x + i, y + j, c);
                }
            if (!lf) G.VLine(s, x, y, y + 15, K);
            if (!rt) G.VLine(s, x + 15, y, y + 15, K);
            if (!dn) G.HLine(s, x, x + 15, y + 15, K);
            if (!up) Cap(s, th, tx, x, y, lf, rt);
        }

        static void Cap(Surface s, int th, int tx, int x, int y, bool lf, bool rt)
        {
            G.HLine(s, x, x + 15, y, K);
            for (int i = 0; i < 16; i++)
            {
                int ax = tx * 16 + i;
                if ((i == 0 && !lf) || (i == 15 && !rt)) continue;
                switch (th)
                {
                    case 0:
                        G.Px(s, x + i, y + 1, G.Hex(0x9ffff4)); G.Px(s, x + i, y + 2, G.Hex(0x3ee0c8)); G.Px(s, x + i, y + 3, G.Hex(0x1a8a84));
                        break;
                    case 1:
                        {
                            G.Px(s, x + i, y + 1, G.Hex(0xffffff)); G.Px(s, x + i, y + 2, G.Hex(0xf0f8ff)); G.Px(s, x + i, y + 3, G.Hex(0xc8e4f8));
                            uint h = Hash(ax, 7);
                            if (h % 3 == 0) G.Px(s, x + i, y + 4, G.Hex(0xc8e4f8));
                            if (h % 7 == 0) G.Px(s, x + i, y + 5, G.Hex(0xa8d0f0));
                            break;
                        }
                    case 2:
                        for (int j = 1; j <= 3; j++) G.Px(s, x + i, y + j, ((ax + j) / 3) % 2 == 0 ? G.Hex(0xffc828) : G.Hex(0x22222a));
                        break;
                    default:
                        G.Px(s, x + i, y + 1, G.Hex(0xffc0ee)); G.Px(s, x + i, y + 2, G.Hex(0xff4ab8)); G.Px(s, x + i, y + 3, G.Hex(0x9a2a7a));
                        break;
                }
            }
        }

        static void Ice(Surface s, World w, int tx, int ty, int x, int y)
        {
            bool up = ty > 0 && (w.Tiles[tx, ty - 1] == '~' || w.Tiles[tx, ty - 1] == '#');
            bool dn = ty < World.Rows - 1 && w.Tiles[tx, ty + 1] == '~';
            bool lf = tx > 0 && w.Tiles[tx - 1, ty] == '~';
            bool rt = tx < World.Cols - 1 && w.Tiles[tx + 1, ty] == '~';
            for (int j = 0; j < 16; j++)
                for (int i = 0; i < 16; i++)
                {
                    int ax = tx * 16 + i, ay = ty * 16 + j;
                    int c = G.Hex(0x86d6f6);
                    if ((ax - ay * 1) % 11 == 0 || (ax - ay) % 11 == 1) c = G.Hex(0xd4f4ff);
                    if (j > 11 && !dn) c = G.Hex(0x5cb0e0);
                    if (Hash(ax, ay) % 53 == 0) c = G.Hex(0xffffff);
                    G.Px(s, x + i, y + j, c);
                }
            if (!lf) G.VLine(s, x, y, y + 15, G.Hex(0x1e4a7a));
            if (!rt) G.VLine(s, x + 15, y, y + 15, G.Hex(0x1e4a7a));
            if (!dn) G.HLine(s, x, x + 15, y + 15, G.Hex(0x1e4a7a));
            if (!up)
            {
                G.HLine(s, x, x + 15, y, G.Hex(0x1e4a7a));
                G.HLine(s, x + (lf ? 0 : 1), x + (rt ? 15 : 14), y + 1, G.Hex(0xffffff));
                G.HLine(s, x + (lf ? 0 : 1), x + (rt ? 15 : 14), y + 2, G.Hex(0xe4fbff));
            }
        }

        static void Plank(Surface s, World w, int tx, int ty, int x, int y, bool ice)
        {
            int hi, body, dark;
            if (ice) { hi = G.Hex(0xffffff); body = G.Hex(0x9ae4ff); dark = G.Hex(0x4aa0d8); }
            else
                switch (w.Theme)
                {
                    case 0: hi = G.Hex(0xffa8e0); body = G.Hex(0xe0379c); dark = G.Hex(0x8a1a62); break;
                    case 1: hi = G.Hex(0xe6f0ff); body = G.Hex(0x8a9cc0); dark = G.Hex(0x4e5c80); break;
                    case 2: hi = G.Hex(0xffe27a); body = G.Hex(0xe0a020); dark = G.Hex(0x7a5210); break;
                    default: hi = G.Hex(0xd8b0ff); body = G.Hex(0x8a4ae0); dark = G.Hex(0x4a2280); break;
                }
            bool lf = tx > 0 && w.Tiles[tx - 1, ty] == w.Tiles[tx, ty];
            bool rt = tx < World.Cols - 1 && w.Tiles[tx + 1, ty] == w.Tiles[tx, ty];
            G.HLine(s, x, x + 15, y, K);
            G.HLine(s, x, x + 15, y + 1, hi);
            G.HLine(s, x, x + 15, y + 2, body);
            G.HLine(s, x, x + 15, y + 3, dark);
            G.HLine(s, x, x + 15, y + 4, K);
            if (!lf) { G.VLine(s, x, y, y + 4, K); }
            if (!rt) { G.VLine(s, x + 15, y, y + 4, K); }
            G.Px(s, x + 3, y + 2, dark); G.Px(s, x + 12, y + 2, dark);
            // little hanging brackets
            if (!ice) { G.Px(s, x + 3, y + 5, K); G.Px(s, x + 12, y + 5, K); G.Px(s, x + 3, y + 6, dark); G.Px(s, x + 12, y + 6, dark); }
            else { if ((tx & 1) == 0) { G.Px(s, x + 6, y + 5, G.Hex(0xc8f0ff)); G.Px(s, x + 6, y + 6, G.Hex(0xc8f0ff)); G.Px(s, x + 6, y + 7, G.Hex(0xe8faff)); } }
        }

        static Sprite spikeUp, spikeDown;
        static void Spikes(Surface s, int x, int y, bool down)
        {
            if (spikeUp == null)
            {
                string[] a = { "..k..", ".klk.", ".klk.", "kllmk", "kllmk", "klmmk", "kkkkk" };
                Func<char, int> pal = delegate (char c) { return c == 'k' ? K : c == 'l' ? G.Hex(0xf0f0fa) : G.Hex(0x9aa0bc); };
                spikeUp = G.Art(a, pal);
                string[] b = new string[a.Length]; for (int i = 0; i < a.Length; i++) b[i] = a[a.Length - 1 - i];
                spikeDown = G.Art(b, pal);
            }
            int dk = G.Hex(0x5a5e78);
            if (!down)
            {
                for (int k = 0; k < 3; k++) G.Blit(s, spikeUp, x + k * 5 + 1, y + 7, false);
                G.HLine(s, x, x + 15, y + 14, dk); G.HLine(s, x, x + 15, y + 15, K);
            }
            else
            {
                for (int k = 0; k < 3; k++) G.Blit(s, spikeDown, x + k * 5 + 1, y + 2, false);
                G.HLine(s, x, x + 15, y + 1, dk); G.HLine(s, x, x + 15, y, K);
            }
        }

        // ---------------- animated tiles ----------------
        public static void DrawDynamic(Surface s, World w, int t)
        {
            for (int ty = 0; ty < World.Rows; ty++)
                for (int tx = 0; tx < World.Cols; tx++)
                {
                    char c = w.Tiles[tx, ty];
                    int x = (int)w.TX(tx), y = (int)w.TY(ty);
                    if (c == '<' || c == '>') Conveyor(s, w, tx, ty, x, y, c == '>' ? 1 : -1, t);
                    else if (c == 'U' || c == 'L' || c == 'R') Fan(s, x, y, c, t);
                }
            foreach (Spring sp in w.Springs) SpringDraw(s, (int)w.TX(sp.TX), (int)w.TY(sp.TY), sp.Anim);
            foreach (Crumble cr in w.Crumbles) CrumbleDraw(s, w, cr, t);
            for (int i = 0; i < w.Movers.Count; i++) MoverDraw(s, w, w.Movers[i], t);
        }

        static void Conveyor(Surface s, World w, int tx, int ty, int x, int y, int dir, int t)
        {
            bool lf = tx > 0 && (w.Tiles[tx - 1, ty] == '<' || w.Tiles[tx - 1, ty] == '>');
            bool rt = tx < World.Cols - 1 && (w.Tiles[tx + 1, ty] == '<' || w.Tiles[tx + 1, ty] == '>');
            G.Rect(s, x, y, 16, 16, G.Hex(0x2c2c38));
            G.Rect(s, x, y + 5, 16, 1, G.Hex(0x1a1a22));
            int off = (t / 3) * dir;
            for (int i = 0; i < 16; i++)
            {
                int ax = tx * 16 + i - off;
                int m = ((ax % 8) + 8) % 8;
                int c = G.Hex(0x3e3e4c);
                // chevrons
                int my = dir > 0 ? m : 7 - m;
                for (int j = 1; j <= 4; j++)
                {
                    int cc = c;
                    int k = dir > 0 ? (m - Math.Abs(j - 2.5f) > 2 && m - Math.Abs(j - 2.5f) < 4.5 ? 1 : 0) : (m + Math.Abs(j - 2.5f) > 3 && m + Math.Abs(j - 2.5f) < 5.5 ? 1 : 0);
                    if (k == 1) cc = G.Hex(0xffc828);
                    G.Px(s, x + i, y + j, cc);
                }
            }
            // rollers
            for (int i = 0; i < 16; i += 4)
            {
                int rx = x + i + 2;
                G.Disc(s, rx, y + 10, 1, G.Hex(0x6a6e80));
                G.Px(s, rx + (((t / 4) * dir) & 1), y + 10, G.Hex(0xb0b4c4));
            }
            G.HLine(s, x, x + 15, y, K); G.HLine(s, x, x + 15, y + 15, K);
            if (!lf) G.VLine(s, x, y, y + 15, K);
            if (!rt) G.VLine(s, x + 15, y, y + 15, K);
        }

        static void Fan(Surface s, int x, int y, char c, int t)
        {
            G.Rect(s, x, y, 16, 16, G.Hex(0x3a3f4c));
            G.RectO(s, x, y, 16, 16, K);
            G.HLine(s, x + 1, x + 14, y + 1, G.Hex(0x5a6070));
            int cx = x + 8, cy = y + 8;
            G.Disc(s, cx, cy, 6, G.Hex(0x1c1e26));
            G.Ring(s, cx, cy, 6, G.Hex(0x8a92a4));
            float a0 = t * 0.45f;
            for (int b = 0; b < 3; b++)
            {
                float a = a0 + b * 2.094f;
                for (int r = 1; r <= 5; r++)
                {
                    int px = cx + (int)Math.Round(Math.Cos(a) * r), py = cy + (int)Math.Round(Math.Sin(a) * r);
                    G.Px(s, px, py, G.Hex(0xc8d0e0));
                    int px2 = cx + (int)Math.Round(Math.Cos(a + 0.35f) * r), py2 = cy + (int)Math.Round(Math.Sin(a + 0.35f) * r);
                    G.Px(s, px2, py2, G.Hex(0x7a8296));
                }
            }
            G.Disc(s, cx, cy, 1, G.Hex(0xffc828));
            // direction chevron on housing
            int arrow = G.Hex(0x3ee0c8);
            if (c == 'U') { G.Px(s, cx, y + 1, arrow); G.Px(s, cx - 1, y + 2, arrow); G.Px(s, cx + 1, y + 2, arrow); }
            else if (c == 'R') { G.Px(s, x + 14, cy, arrow); G.Px(s, x + 13, cy - 1, arrow); G.Px(s, x + 13, cy + 1, arrow); }
            else { G.Px(s, x + 1, cy, arrow); G.Px(s, x + 2, cy - 1, arrow); G.Px(s, x + 2, cy + 1, arrow); }
        }

        static void SpringDraw(Surface s, int x, int y, int anim)
        {
            // pad top normally at y+7
            int comp = 0;
            if (anim > 8) comp = -3; else if (anim > 4) comp = 2; else if (anim > 0) comp = -1;
            int padY = y + 7 + comp;
            int baseY = y + 13;
            G.Rect(s, x + 2, baseY, 12, 3, G.Hex(0x3a3f4c)); G.RectO(s, x + 2, baseY, 12, 3, K);
            int coilTop = padY + 3;
            for (int yy = coilTop; yy < baseY; yy++)
            {
                int phase = (yy - coilTop) % 2;
                G.HLine(s, x + 4, x + 11, yy, phase == 0 ? G.Hex(0xc0c6d4) : G.Hex(0x6a7084));
            }
            G.Rect(s, x + 1, padY, 14, 3, G.Hex(0xe83048)); G.RectO(s, x + 1, padY - 1, 14, 5, K);
            G.HLine(s, x + 2, x + 13, padY, G.Hex(0xff8a9a));
        }

        static void CrumbleDraw(Surface s, World w, Crumble c, int t)
        {
            if (c.State == 2 && c.T < 170) return;
            int x = (int)w.TX(c.TX), y = (int)w.TY(c.TY);
            int level = 16;
            if (c.State == 2) level = Math.Min(16, (c.T - 170) / 2);
            if (c.State == 1) { x += ((t >> 1) & 1) == 0 ? 1 : -1; if (c.T > 20) y += (c.T - 20) / 6; }
            for (int j = 0; j < 16; j++)
                for (int i = 0; i < 16; i++)
                {
                    if (level < 16 && !G.Dith(i, j, level)) continue;
                    int col = G.Hex(0xc89466);
                    if (j < 3) col = G.Hex(0xe8b888);
                    if (j > 12) col = G.Hex(0x9a6a44);
                    uint h = Hash(c.TX * 16 + i, c.TY * 16 + j);
                    if (h % 13 == 0) col = G.Hex(0xa87a50);
                    G.Px(s, x + i, y + j, col);
                }
            // cracks
            int ck = G.Hex(0x4a2c18);
            int[] crack = { 5, 2, 6, 3, 6, 4, 7, 5, 8, 6, 8, 7, 9, 8, 10, 9, 10, 10, 4, 9, 3, 10, 12, 4, 13, 5 };
            int n = c.State == 1 ? crack.Length : 14;
            for (int i = 0; i + 1 < n; i += 2) G.Px(s, x + crack[i], y + crack[i + 1], ck);
            G.RectO(s, x, y, 16, 16, K);
        }

        static void MoverDraw(Surface s, World w, Mover m, int t)
        {
            int x = (int)Math.Round(m.X), y = (int)Math.Round(m.Y), wd = (int)m.W;
            int body, hi, dk;
            switch (w.Theme)
            {
                case 0: body = G.Hex(0x3ec8b8); hi = G.Hex(0xa0fff0); dk = G.Hex(0x1a6a66); break;
                case 1: body = G.Hex(0x7aa0d0); hi = G.Hex(0xe0f0ff); dk = G.Hex(0x3a5a8a); break;
                case 2: body = G.Hex(0xe0a020); hi = G.Hex(0xffe27a); dk = G.Hex(0x7a5210); break;
                default: body = G.Hex(0xc050f0); hi = G.Hex(0xf0b8ff); dk = G.Hex(0x5a2288); break;
            }
            G.Rect(s, x, y, wd, 7, body);
            G.HLine(s, x + 1, x + wd - 2, y + 1, hi);
            G.HLine(s, x + 1, x + wd - 2, y + 5, dk);
            G.RectO(s, x, y - 1 + 1, wd, 7, K);
            // blinking lights
            for (int i = 6; i < wd - 4; i += 10)
                G.Px(s, x + i, y + 3, ((t / 20 + i) & 1) == 0 ? G.Hex(0xfff070) : G.Hex(0x8a7020));
            // thrusters
            G.Px(s, x + 3, y + 7, ((t >> 2) & 1) == 0 ? G.Hex(0xff9a30) : G.Hex(0xffe070));
            G.Px(s, x + wd - 4, y + 7, ((t >> 2) & 1) == 1 ? G.Hex(0xff9a30) : G.Hex(0xffe070));
        }
    }
}
