using System;

namespace Deskosaur
{
    // The launcher's bar regular: a big red spiny dino. Rendered procedurally at HALF resolution with
    // clean, hard-edged shading bands (no noise/dither), then drawn at 2x so it reads as chunky pixel art.
    public sealed class BigDino
    {
        public const int S = 2;                                  // pixel size when drawn
        public const int CW = 236, CH = 175, Ledge = 122;        // full-res canvas (screen pixels)
        public const int LW = CW / S, LH = CH / S, LLedge = Ledge / S;
        public Sprite[] Frames = new Sprite[6];                  // [bob 0/1] x [eye open/half/closed], low-res
        public Sprite Arm;

        static readonly int K = G.Hex(0x0a0408);                 // outline
        // vibrant red skin ramp: deep shadow -> hot highlight
        static readonly int[] Skin = { G.Hex(0x2a0614), G.Hex(0x560a1e), G.Hex(0x8e1024), G.Hex(0xc8182a), G.Hex(0xf02a30), G.Hex(0xff5a40), G.Hex(0xffa070) };
        static readonly int[] Belly = { G.Hex(0x3a1416), G.Hex(0x6a2a22), G.Hex(0xa45236), G.Hex(0xd8844e), G.Hex(0xf4b070), G.Hex(0xffd8a0) };
        static readonly int[] Spine = { G.Hex(0x0e0a12), G.Hex(0x221c2a), G.Hex(0x3a3446), G.Hex(0x5e5870), G.Hex(0x9a94ac) };
        static readonly int Teal = G.Hex(0x3ee0c8), TealD = G.Hex(0x1e8a86), Pink = G.Hex(0xff3aa0);

        float bobY;

        public BigDino()
        {
            for (int bob = 0; bob < 2; bob++)
                for (int eye = 0; eye < 3; eye++)
                    Frames[bob * 3 + eye] = Render(bob, eye);
            Arm = RenderArm();
        }

        // ---------------- SDF helpers (full-res coordinates) ----------------
        static float Ell(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return ((float)Math.Sqrt(dx * dx + dy * dy) - 1f) * Math.Min(rx, ry);
        }
        static float Cap(float x, float y, float ax, float ay, float bx, float by, float ra, float rb)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = (pax * bax + pay * bay) / (bax * bax + bay * bay);
            if (h < 0) h = 0; if (h > 1) h = 1;
            float dx = pax - bax * h, dy = pay - bay * h;
            return (float)Math.Sqrt(dx * dx + dy * dy) - (ra + (rb - ra) * h);
        }
        static float SMin(float a, float b, float k)
        {
            float h = Math.Max(k - Math.Abs(a - b), 0) / k;
            return Math.Min(a, b) - h * h * k * 0.25f;
        }

        float Shape(float x, float y)
        {
            float hy = y - bobY * S;
            float body = Ell(x, y, 162, 146, 60, 50);
            float shoulder = Ell(x, y, 130, 120, 31, 27);
            float neck = Cap(x, hy * 0.6f + y * 0.4f, 132, 112, 102, 80, 25, 21);
            float head = Ell(x, hy, 92, 66, 30, 21);
            float snout = Cap(x, hy, 84, 67, 34, 74, 14, 10);
            float jaw = Cap(x, hy, 90, 82, 40, 84, 10, 7);
            float brow = Ell(x, hy, 77, 54, 13, 6);
            float d = SMin(body, shoulder, 16);
            d = SMin(d, neck, 12);
            d = SMin(d, head, 10);
            d = SMin(d, snout, 8);
            d = SMin(d, jaw, 6);
            d = SMin(d, brow, 5);
            return d;
        }

        float Throat(float x, float y)
        {
            float hy = y - bobY * S;
            float t = Cap(x, hy * 0.5f + y * 0.5f, 98, 88, 116, 140, 9, 15);
            float j = Cap(x, hy, 44, 88, 86, 88, 4, 6);
            return Math.Min(t, hy > 84 ? j : 99);
        }

        // staggered scallop scales: short dark arcs along the bottom of each scale
        static bool Scale(int px, int py)
        {
            int row = py / 4, m = (px + (row & 1) * 3) % 6, r = py % 4;
            return (r == 3 && m >= 2 && m <= 3) || (r == 2 && (m == 1 || m == 4)) || (r == 1 && m == 0 && ((px / 6 + row) % 2 == 0));
        }

        static int Band(int[] ramp, float v)
        {
            int i = (int)Math.Floor(v * ramp.Length);
            if (i < 0) i = 0; if (i >= ramp.Length) i = ramp.Length - 1;
            return ramp[i];
        }

        // ---------------- body ----------------
        Sprite Render(int bob, int eye)
        {
            bobY = bob;
            Sprite s = new Sprite(LW, LH);
            float lx = -0.6f, ly = -0.68f, lz = 0.42f;
            for (int py = 0; py < LH; py++)
                for (int px = 0; px < LW; px++)
                {
                    float x = px * S + S * 0.5f, y = py * S + S * 0.5f;
                    float d = Shape(x, y);
                    if (d > 0) continue;
                    if (d > -S * 1.05f) { s.P[py * LW + px] = K; continue; }   // crisp 1-pixel outline
                    float gx = Shape(x + 1, y) - Shape(x - 1, y), gy = Shape(x, y + 1) - Shape(x, y - 1);
                    float gl = (float)Math.Sqrt(gx * gx + gy * gy) + 1e-4f; gx /= gl; gy /= gl;
                    float e = 1f + d / 12f; if (e < 0) e = 0;
                    float nx = gx * e, ny = gy * e, nz = (float)Math.Sqrt(Math.Max(0, 1 - e * e));
                    float diff = nx * lx + ny * ly + nz * lz; if (diff < 0) diff = 0;
                    float v = 0.06f + diff * 1.0f;
                    if (y > 112) v -= (y - 112) * 0.011f;                       // falls into shadow toward the bar
                    if (Cap(x, y - bobY * S, 96, 92, 120, 96, 3, 7) < 0) v -= 0.25f; // tucked under the jaw
                    bool bel = Throat(x, y) < 0;
                    int c = Band(bel ? Belly : Skin, v);
                    // scales: a sparse diagonal pattern, one shade darker, only on lit skin (keeps it clean)
                    if (!bel && v > 0.36f && v < 0.92f && Scale(px, py)) c = Band(Skin, v - 0.15f);
                    // belly plates
                    if (bel && py % 2 == 0 && Throat(x, y) < -3) c = Band(Belly, v - 0.17f);
                    // neon rim lights: teal from the bar on the right, pink bounce from the street lower-left
                    if (d > -S * 2.2f)
                    {
                        if (gx > 0.55f && gy < 0.5f) c = gx > 0.8f ? Teal : TealD;
                        else if (gx < -0.4f && gy > 0.3f && y < Ledge) c = Pink;
                    }
                    s.P[py * LW + px] = c;
                }
            Spines(s);
            Face(s, eye, bob);
            return s;
        }

        // big dorsal spines running from the back of the skull down the spine
        void Spines(Sprite s)
        {
            float[] xs = { 100, 112, 124, 136, 150, 165, 180, 195, 210, 224 };
            float[] len = { 22, 32, 40, 46, 48, 46, 42, 36, 30, 24 };
            for (int i = 0; i < xs.Length; i++)
            {
                float bx = xs[i], by = -1;
                for (float yy = 0; yy < Ledge; yy += 1) if (Shape(bx, yy) < -3f) { by = yy; break; }
                if (by < 0) continue;
                float gx = Shape(bx + 1, by) - Shape(bx - 1, by), gy = Shape(bx, by + 1) - Shape(bx, by - 1);
                float gl = (float)Math.Sqrt(gx * gx + gy * gy) + 1e-4f; gx /= gl; gy /= gl;
                // lean the spines backward a bit for a swept look
                float nx = gx * 0.85f + 0.35f, ny = gy * 0.85f - 0.1f; float nl = (float)Math.Sqrt(nx * nx + ny * ny); nx /= nl; ny /= nl;
                Spike(s, bx / S, (by + 3) / S, nx, ny, len[i] / S, (6 + len[i] * 0.2f) / S);
            }
        }

        void Spike(Sprite s, float bx, float by, float nx, float ny, float len, float wid)
        {
            float tx = -ny, ty = nx;
            int x0 = (int)(bx - len - wid) - 2, x1 = (int)(bx + len + wid) + 2, y0 = (int)(by - len - wid) - 2, y1 = (int)(by + len + wid) + 2;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || y < 0 || x >= LW || y >= LH) continue;
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float u = ((fx - bx) * nx + (fy - by) * ny) / len;
                    if (u < -0.15f || u > 1.02f) continue;
                    float v = (fx - bx) * tx + (fy - by) * ty;
                    float curve = u * u * len * 0.18f;                         // slight hook at the tip
                    v += curve;
                    float half = wid * (1 - u);
                    if (half < 0.35f || Math.Abs(v) > half) continue;
                    int cur = s.Get(x, y);
                    if (u < 0.1f && cur != 0 && cur != K) continue;            // don't paint over the body at the root
                    int c;
                    if (Math.Abs(v) > half - 1.0f || u > 0.93f) c = K;
                    else if (v < -half * 0.35f) c = Spine[3];                   // lit edge
                    else if (v < half * 0.2f) c = Spine[2];
                    else c = Spine[1];
                    if (u > 0.15f && u < 0.6f && v < -half * 0.5f && v > -half + 1.0f) c = Spine[4]; // highlight streak
                    s.Set(x, y, c);
                }
        }

        void P(Sprite s, int x, int y, int c) { s.Set(x, y, c); }

        void Face(Sprite s, int eye, int bob)
        {
            int b = bob;   // bob in low-res pixels
            // heavy brow shadow
            for (int x = 33; x <= 40; x++) P(s, x, 27 + b, Skin[1]);
            for (int x = 34; x <= 39; x++) P(s, x, 28 + b, Skin[2]);
            int ex = 35, ey = 29 + b;
            if (eye == 2)
            {
                for (int x = ex - 1; x <= ex + 3; x++) P(s, x, ey + 1, K);
            }
            else
            {
                int top = eye == 1 ? ey + 1 : ey;
                for (int y = top; y <= ey + 1; y++) for (int x = ex; x <= ex + 3; x++) P(s, x, y, G.Hex(0xffd23a));
                P(s, ex + 2, top, K); P(s, ex + 2, ey + 1, K);             // slit pupil
                if (eye == 0) P(s, ex, top, G.Hex(0xfff6d8));             // glint
                for (int x = ex - 1; x <= ex + 4; x++) P(s, x, top - 1, K); // lid
                P(s, ex - 1, ey + 1, K); P(s, ex + 4, ey + 1, K);
                for (int x = ex; x <= ex + 3; x++) P(s, x, ey + 2, Skin[1]);
            }
            // nostril
            P(s, 20, 34 + b, K); P(s, 21, 34 + b, K);
            // grin: lip line with chunky teeth
            for (int x = 15; x <= 48; x++)
            {
                float t = (x - 15) / 33f;
                int y = 39 + b - (t > 0.84f ? (int)((t - 0.84f) * 18) : 0);
                P(s, x, y, K);
                if (x >= 17 && x <= 41)
                {
                    int k = (x - 17) % 3;
                    if (k == 0) { P(s, x, y + 1, G.Hex(0xf6f0f4)); P(s, x, y + 2, G.Hex(0xc0b4c4)); }
                    else if (k == 1) P(s, x, y + 1, G.Hex(0xf6f0f4));
                    else P(s, x, y + 1, Skin[1]);
                }
            }
            // cheek crease, old scar, snout bumps
            for (int k = 0; k < 4; k++) P(s, 47 + k, 37 + b + k / 2, Skin[1]);
            for (int k = 0; k < 4; k++) { P(s, 29 + k, 31 + b - k / 2, Skin[1]); P(s, 29 + k, 32 + b - k / 2, Skin[5]); }
            P(s, 24, 35 + b, Skin[2]); P(s, 27, 34 + b, Skin[2]); P(s, 30, 35 + b, Skin[2]);
            // hot specular glints on snout and brow
            P(s, 22, 31 + b, Skin[6]); P(s, 23, 31 + b, Skin[6]); P(s, 37, 25 + b, Skin[6]); P(s, 38, 25 + b, Skin[6]);
        }

        // ---------------- arm resting on the bar ----------------
        Sprite RenderArm()
        {
            Sprite s = new Sprite(LW, LH);
            bobY = 0;
            for (int py = 48; py < 70; py++)
                for (int px = 20; px < 76; px++)
                {
                    float x = px * S + 1, y = py * S + 1;
                    float d = SMin(Cap(x, y, 132, 112, 104, 120, 12, 10), Cap(x, y, 104, 120, 66, Ledge - 2, 9, 7), 6);
                    float cl = Math.Min(Cap(x, y, 66, Ledge - 2, 54, Ledge + 1, 3.6f, 1.4f), Math.Min(Cap(x, y, 68, Ledge - 1, 58, Ledge + 3, 3.4f, 1.2f), Cap(x, y, 72, Ledge, 64, Ledge + 4, 3.2f, 1.2f)));
                    float dd = Math.Min(d, cl);
                    if (dd > 0) continue;
                    if (dd > -S * 1.05f) { s.Set(px, py, K); continue; }
                    if (cl < 0 && d > -2) { s.Set(px, py, x < 60 ? G.Hex(0xe8dcc0) : G.Hex(0xa89a80)); continue; }
                    float gy = Cap(x, y + 1, 104, 120, 66, Ledge - 2, 9, 7) - Cap(x, y - 1, 104, 120, 66, Ledge - 2, 9, 7);
                    float v = 0.5f - gy * 0.35f - (x > 110 ? (x - 110) * 0.008f : 0);
                    int c = Band(Skin, v);
                    if (v > 0.36f && Scale(px, py)) c = Band(Skin, v - 0.15f);
                    if (gy > 0.6f && d > -4) c = Skin[1];                       // underside in shadow
                    s.Set(px, py, c);
                }
            return s;
        }
    }
}
