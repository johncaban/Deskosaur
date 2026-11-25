// Deskosaur - tiny software renderer (C# 5 compatible so it builds with Windows' built-in csc too)
using System;

namespace Deskosaur
{
    public sealed class Sprite
    {
        public int W, H;
        public int[] P; // ARGB, 0 = transparent
        public Sprite(int w, int h) { W = w; H = h; P = new int[w * h]; }
        public int Get(int x, int y) { return (x < 0 || y < 0 || x >= W || y >= H) ? 0 : P[y * W + x]; }
        public void Set(int x, int y, int c) { if (x >= 0 && y >= 0 && x < W && y < H) P[y * W + x] = c; }
    }

    public sealed class Surface
    {
        public int W, H;
        public int[] P;
        public Surface(int w, int h) { W = w; H = h; P = new int[w * h]; }
    }

    public static class G
    {
        public const int Key = unchecked((int)0xFFFF00FF); // overlay transparency color key

        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        public static int Rgb(int r, int g, int b)
        {
            if (r == 255 && g == 0 && b == 255) g = 1; // never collide with color key
            return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
        }
        public static int Hex(int rgb) { return Rgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public static int Mix(int a, int b, float t)
        {
            if (t <= 0) return a; if (t >= 1) return b;
            int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
            int br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
            return Rgb((int)(ar + (br - ar) * t), (int)(ag + (bg - ag) * t), (int)(ab + (bb - ab) * t));
        }
        public static int Mul(int c, float f)
        {
            int r = (int)(((c >> 16) & 255) * f), g = (int)(((c >> 8) & 255) * f), b = (int)((c & 255) * f);
            if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;
            return Rgb(r, g, b);
        }

        public static bool Dith(int x, int y, int level) // level 0..16
        {
            return Bayer[((y & 3) << 2) | (x & 3)] < level;
        }

        public static void Clear(Surface s, int c)
        {
            int[] p = s.P; for (int i = 0; i < p.Length; i++) p[i] = c;
        }

        public static void Px(Surface s, int x, int y, int c)
        {
            if ((uint)x < (uint)s.W && (uint)y < (uint)s.H) s.P[y * s.W + x] = c;
        }
        public static int GetPx(Surface s, int x, int y)
        {
            if ((uint)x < (uint)s.W && (uint)y < (uint)s.H) return s.P[y * s.W + x];
            return 0;
        }

        public static void Rect(Surface s, int x, int y, int w, int h, int c)
        {
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(s.W, x + w), y1 = Math.Min(s.H, y + h);
            for (int yy = y0; yy < y1; yy++)
            {
                int o = yy * s.W;
                for (int xx = x0; xx < x1; xx++) s.P[o + xx] = c;
            }
        }

        public static void RectD(Surface s, int x, int y, int w, int h, int c, int level)
        {
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(s.W, x + w), y1 = Math.Min(s.H, y + h);
            for (int yy = y0; yy < y1; yy++)
            {
                int o = yy * s.W;
                for (int xx = x0; xx < x1; xx++) if (Dith(xx, yy, level)) s.P[o + xx] = c;
            }
        }

        public static void RectO(Surface s, int x, int y, int w, int h, int c)
        {
            HLine(s, x, x + w - 1, y, c); HLine(s, x, x + w - 1, y + h - 1, c);
            VLine(s, x, y, y + h - 1, c); VLine(s, x + w - 1, y, y + h - 1, c);
        }

        public static void HLine(Surface s, int x0, int x1, int y, int c)
        {
            if ((uint)y >= (uint)s.H) return;
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            if (x0 < 0) x0 = 0; if (x1 >= s.W) x1 = s.W - 1;
            int o = y * s.W;
            for (int x = x0; x <= x1; x++) s.P[o + x] = c;
        }
        public static void VLine(Surface s, int x, int y0, int y1, int c)
        {
            if ((uint)x >= (uint)s.W) return;
            if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
            if (y0 < 0) y0 = 0; if (y1 >= s.H) y1 = s.H - 1;
            for (int y = y0; y <= y1; y++) s.P[y * s.W + x] = c;
        }

        public static void Line(Surface s, int x0, int y0, int x1, int y1, int c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            for (int guard = 0; guard < 4000; guard++)
            {
                Px(s, x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public static void Disc(Surface s, int cx, int cy, int r, int c)
        {
            int rr = r * r + r;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= rr) Px(s, cx + x, cy + y, c);
        }
        public static void DiscD(Surface s, int cx, int cy, int r, int c, int level)
        {
            int rr = r * r + r;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= rr && Dith(cx + x, cy + y, level)) Px(s, cx + x, cy + y, c);
        }
        public static void Ring(Surface s, int cx, int cy, int r, int c)
        {
            int ro = r * r + r, ri = (r - 1) * (r - 1) + (r - 1);
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d = x * x + y * y;
                    if (d <= ro && d > ri) Px(s, cx + x, cy + y, c);
                }
        }

        public static void Blit(Surface s, Sprite sp, int x, int y, bool flip)
        {
            for (int yy = 0; yy < sp.H; yy++)
            {
                int dy = y + yy; if ((uint)dy >= (uint)s.H) continue;
                int so = yy * sp.W, d = dy * s.W;
                for (int xx = 0; xx < sp.W; xx++)
                {
                    int c = sp.P[so + (flip ? sp.W - 1 - xx : xx)];
                    if (c == 0) continue;
                    int dx = x + xx; if ((uint)dx >= (uint)s.W) continue;
                    s.P[d + dx] = c;
                }
            }
        }

        public static void BlitSolid(Surface s, Sprite sp, int x, int y, bool flip, int col)
        {
            for (int yy = 0; yy < sp.H; yy++)
                for (int xx = 0; xx < sp.W; xx++)
                    if (sp.P[yy * sp.W + (flip ? sp.W - 1 - xx : xx)] != 0) Px(s, x + xx, y + yy, col);
        }

        public static void BlitScaled(Surface s, Sprite sp, int x, int y, int k, bool flip)
        {
            for (int yy = 0; yy < sp.H; yy++)
                for (int xx = 0; xx < sp.W; xx++)
                {
                    int c = sp.P[yy * sp.W + (flip ? sp.W - 1 - xx : xx)];
                    if (c != 0) Rect(s, x + xx * k, y + yy * k, k, k, c);
                }
        }

        // Draw sprite with scale + rotation around anchor (ax,ay in sprite pixels) placed at (px,py).
        public static void BlitX(Surface s, Sprite sp, float px, float py, float ax, float ay, float scx, float scy, float ang, bool flip)
        {
            if (scx < 0.05f || scy < 0.05f) return;
            float ca = (float)Math.Cos(ang), sa = (float)Math.Sin(ang);
            float ext = (float)Math.Sqrt(sp.W * sp.W + sp.H * sp.H) * Math.Max(scx, scy) + 2;
            int x0 = (int)(px - ext), x1 = (int)(px + ext), y0 = (int)(py - ext), y1 = (int)(py + ext);
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0; if (x1 >= s.W) x1 = s.W - 1; if (y1 >= s.H) y1 = s.H - 1;
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - py;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - px;
                    float ux = (dx * ca + dy * sa) / scx + ax;
                    float uy = (-dx * sa + dy * ca) / scy + ay;
                    int ix = (int)Math.Floor(ux), iy = (int)Math.Floor(uy);
                    if (ix < 0 || iy < 0 || ix >= sp.W || iy >= sp.H) continue;
                    int c = sp.P[iy * sp.W + (flip ? sp.W - 1 - ix : ix)];
                    if (c != 0) s.P[y * s.W + x] = c;
                }
            }
        }

        public static void Copy(Surface src, Surface dst)
        {
            Array.Copy(src.P, dst.P, Math.Min(src.P.Length, dst.P.Length));
        }

        // Parse ascii art into sprite with a palette lookup (char -> color, '.' or ' ' transparent)
        public static Sprite Art(string[] rows, Func<char, int> pal)
        {
            int w = 0; foreach (string r in rows) w = Math.Max(w, r.Length);
            Sprite sp = new Sprite(w, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '.' || ch == ' ') continue;
                    sp.P[y * w + x] = pal(ch);
                }
            return sp;
        }
    }
}
