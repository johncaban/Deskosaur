using System;

namespace Deskosaur
{
    public enum PT { Dust, Spark, Ice, Star, Wind, Heart, Ring, Debris, Flame, Glint }

    // Fixed-size particle pool: no per-frame allocations
    public sealed class Particles
    {
        const int N = 900;
        float[] x = new float[N], y = new float[N], vx = new float[N], vy = new float[N], grav = new float[N];
        int[] life = new int[N], max = new int[N], col = new int[N], size = new int[N];
        PT[] type = new PT[N];
        bool[] alive = new bool[N];
        int next;
        public static Random Rng = new Random(1234);

        public static float R(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }

        public void Spawn(PT t, float px, float py, float pvx, float pvy, int lf, int c, int sz, float g)
        {
            int i = next; next = (next + 1) % N;
            alive[i] = true; type[i] = t; x[i] = px; y[i] = py; vx[i] = pvx; vy[i] = pvy;
            life[i] = lf; max[i] = lf; col[i] = c; size[i] = sz; grav[i] = g;
        }

        public void Clear() { for (int i = 0; i < N; i++) alive[i] = false; }

        public void Update()
        {
            for (int i = 0; i < N; i++)
            {
                if (!alive[i]) continue;
                if (--life[i] <= 0) { alive[i] = false; continue; }
                x[i] += vx[i]; y[i] += vy[i]; vy[i] += grav[i];
                if (type[i] == PT.Dust) { vx[i] *= 0.9f; vy[i] *= 0.9f; }
                if (type[i] == PT.Heart) vx[i] = (float)Math.Sin(life[i] * 0.2f) * 0.4f;
            }
        }

        // Pull particles toward a point (black hole)
        public void Attract(float cx, float cy, float strength)
        {
            for (int i = 0; i < N; i++)
            {
                if (!alive[i] || type[i] != PT.Star) continue;
                float dx = cx - x[i], dy = cy - y[i];
                float d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
                vx[i] += dx / d * strength; vy[i] += dy / d * strength;
                if (d < 3) alive[i] = false;
            }
        }

        public void Draw(Surface s, int t)
        {
            int outline = G.Hex(0x120c1c);
            for (int i = 0; i < N; i++)
            {
                if (!alive[i]) continue;
                int px = (int)Math.Round(x[i]), py = (int)Math.Round(y[i]);
                float f = life[i] / (float)max[i]; // 1 -> 0
                switch (type[i])
                {
                    case PT.Dust:
                        {
                            int r = size[i] + (int)((1 - f) * 2.5f);
                            int lvl = (int)(4 + f * 12);
                            G.DiscD(s, px, py, r, col[i], lvl);
                            break;
                        }
                    case PT.Spark:
                    case PT.Debris:
                        if (size[i] <= 1) G.Px(s, px, py, col[i]);
                        else { G.Rect(s, px - 1, py - 1, size[i] + 1, size[i] + 1, outline); G.Rect(s, px, py, size[i] - 1, size[i] - 1, col[i]); }
                        break;
                    case PT.Ice:
                        G.Px(s, px, py, col[i]);
                        if (f > 0.5f) { G.Px(s, px + 1, py, G.Hex(0xffffff)); G.Px(s, px, py + 1, col[i]); }
                        break;
                    case PT.Star:
                    case PT.Glint:
                        {
                            int a = size[i];
                            if (type[i] == PT.Glint) a = f > 0.66f ? 1 : (f > 0.33f ? 2 : 1);
                            G.Px(s, px, py, G.Hex(0xffffff));
                            for (int k = 1; k <= a; k++)
                            {
                                G.Px(s, px + k, py, col[i]); G.Px(s, px - k, py, col[i]);
                                G.Px(s, px, py + k, col[i]); G.Px(s, px, py - k, col[i]);
                            }
                            break;
                        }
                    case PT.Wind:
                        {
                            int len = size[i];
                            float sp = (float)Math.Sqrt(vx[i] * vx[i] + vy[i] * vy[i]) + 0.001f;
                            float dx = vx[i] / sp, dy = vy[i] / sp;
                            for (int k = 0; k < len; k++)
                            {
                                int qx = (int)Math.Round(x[i] - dx * k), qy = (int)Math.Round(y[i] - dy * k);
                                if (G.Dith(qx, qy, (int)(6 + f * 10) - k)) G.Px(s, qx, qy, col[i]);
                            }
                            break;
                        }
                    case PT.Heart:
                        {
                            int c = col[i];
                            G.Px(s, px - 1, py, c); G.Px(s, px + 1, py, c);
                            G.Px(s, px - 2, py + 1, c); G.Px(s, px - 1, py + 1, c); G.Px(s, px, py + 1, c); G.Px(s, px + 1, py + 1, c); G.Px(s, px + 2, py + 1, c);
                            G.Px(s, px - 1, py + 2, c); G.Px(s, px, py + 2, c); G.Px(s, px + 1, py + 2, c);
                            G.Px(s, px, py + 3, c);
                            G.Px(s, px - 1, py, G.Hex(0xffd0e0));
                            break;
                        }
                    case PT.Ring:
                        {
                            int r = size[i] + (int)((1 - f) * 10);
                            if (f > 0.3f || ((t & 1) == 0)) G.Ring(s, px, py, r, col[i]);
                            break;
                        }
                    case PT.Flame:
                        {
                            int c = f > 0.66f ? G.Hex(0xfff070) : f > 0.33f ? G.Hex(0xff8a20) : G.Hex(0x8a8a9a);
                            G.Px(s, px, py, c); if (f > 0.5f) G.Px(s, px + 1, py, c);
                            break;
                        }
                }
            }
        }
    }
}
