using System;
using System.Collections.Generic;

namespace Deskosaur
{
    public enum Pose { Idle0, Idle1, Blink, Run0, Run1, Run2, Run3, Jump, Fall, Skid, Happy, Count }

    public sealed class Outfit
    {
        public int Char, Hat, Glasses, Shirt, Shoes;
        public Outfit Clone() { return (Outfit)MemberwiseClone(); }
        public int Hash() { return Char | (Hat << 4) | (Glasses << 8) | (Shirt << 12) | (Shoes << 16); }
    }

    public sealed class DinoDef
    {
        public string Name;
        public string[] Body;
        public Dictionary<char, int> Pal = new Dictionary<char, int>();
        public int[] LegX;      // x of each leg pair (far leg column); near leg = x + 4
        public int HatX, HatY;  // head top center (hat bottom sits at HatY+1)
        public int EyeX, EyeY;  // eye top-left (2x2)
        public int FootX;       // horizontal center of feet
        public int LegShift;    // quadrupeds: phase-offset back legs
    }

    public sealed class Item
    {
        public string Name; public int Cost;
        public Item(string n, int c) { Name = n; Cost = c; }
    }

    public static class Art
    {
        public static readonly int Outline = G.Hex(0x1c1428);
        public static readonly int EyeC = G.Hex(0x120c1c);
        public static readonly int White = G.Hex(0xffffff);
        public static readonly int Blush = G.Hex(0xff7aa2);

        public static readonly DinoDef[] Dinos;

        public static readonly Item[] Hats = {
            new Item("NONE",0), new Item("BALL CAP",0), new Item("BEANIE",0), new Item("COWBOY",0),
            new Item("PARTY HAT",0), new Item("PROPELLER",6), new Item("TOP HAT",12), new Item("CROWN",24) };
        public static readonly Item[] Glasses = {
            new Item("NONE",0), new Item("SHADES",0), new Item("NERD",0), new Item("3D SPECS",0),
            new Item("AVIATORS",0), new Item("HEART SHADES",9), new Item("STAR SHADES",15), new Item("PIXEL SHADES",21) };
        public static readonly Item[] Shirts = {
            new Item("NONE",0), new Item("RED TEE",0), new Item("SAILOR",0), new Item("HOODIE",0),
            new Item("HAWAIIAN",0), new Item("TUXEDO",0), new Item("CAMO",3), new Item("GOLD JERSEY",18), new Item("RAINBOW",30) };
        public static readonly Item[] Shoes = {
            new Item("NONE",0), new Item("RED KICKS",0), new Item("WHITE KICKS",0), new Item("BOOTS",0),
            new Item("BUNNY SLIPPERS",0), new Item("ROCKET BOOTS",36), new Item("GOLD KICKS",45) };

        static Art()
        {
            Dinos = new DinoDef[4];

            DinoDef rex = new DinoDef();
            rex.Name = "REX";
            rex.Body = new string[] {
                "..........kkkkkkkk....",
                ".........khhhhhhhhk...",
                "........khbbbbbbbbbk..",
                "........kbbbbbbbwebbk.",
                "........kbbbbbbbeebbbk",
                "........kbbbpbbbbbbbbk",
                "........kbbbbbbbbbbbbk",
                "........kbbbbbkkkkkkk.",
                "........kbbbbbklllk...",
                "........kbbbbbbkkk....",
                ".......kBBBBLLLLk.....",
                ".k....kBBBBLLLLLLkkk..",
                "kbk..kBBBBBLLLLLLkbbk.",
                "kbbkkBBBBBBLLLLLLkkk..",
                ".kbbbBBBBBBLLLLLLk....",
                "..kdddDDDDDMMMMMMk....",
                "...kkkkkkkkkkkkkkk....",
            };
            Pal(rex, 0xff8c32, 0xd8621e, 0xffb562, 0xffe8b8, 0xf2c98c, 0xffd27a, 0xc98a34);
            rex.LegX = new int[] { 8 }; rex.HatX = 14; rex.HatY = 0; rex.EyeX = 16; rex.EyeY = 3; rex.FootX = 11;
            Dinos[0] = rex;

            DinoDef bronto = new DinoDef();
            bronto.Name = "BRONTO";
            bronto.Body = new string[] {
                ".............kkkkk.....",
                "............khhhhhkk...",
                "............kbbbbwebk..",
                "............kbbbbeebbk.",
                "............kbbpbbbbbk.",
                "............kbbbbbkkk..",
                "............kbbbllk....",
                "............kbbbllk....",
                "............kbbbllk....",
                ".....kkkkkkkkbbbllk....",
                "...kkhhhhhhhhBBBLLk....",
                "..khbbbabbbbbBBBLLLk...",
                ".kbbbbbbbbabbBBBLLLk...",
                "kbbbabbbbbbbbBBBLLLk...",
                "kdbbbbbbbbbbbBBBLLLk...",
                ".kdddddddddddDDDMMMk...",
                "..kkkkkkkkkkkkkkkkkk...",
            };
            Pal(bronto, 0x36cfa8, 0x1f9e80, 0x7eeccc, 0xfff0c2, 0xecd59a, 0x8af2d6, 0x1f9e80);
            bronto.LegX = new int[] { 4, 12 }; bronto.HatX = 15; bronto.HatY = 0; bronto.EyeX = 17; bronto.EyeY = 2; bronto.FootX = 11; bronto.LegShift = 2;
            Dinos[1] = bronto;

            DinoDef steg = new DinoDef();
            steg.Name = "STEGGY";
            steg.Body = new string[] {
                ".........k...k...........",
                "........kak.kak.k........",
                "......k.kaakkaakkak......",
                ".....kakkaaakaaakaak.....",
                ".....kaakkkkkkkkkkaak....",
                "....kkkkhhhhhhhhhhkkk....",
                "...khhhbbbbbbbbbbbbbhk...",
                "..kbbbbbbbabbbbbbabbbkkk.",
                ".kbbbabbbbbbbbbbbbbbkhhhk",
                "kckbbbbbbbbbbbbbbbbbkbwek",
                "kcckbbbbBBBBBBBBbbbbbbeek",
                ".kkbbbbbBBBBBBBBbbbbpbbbk",
                "...kbbbbBBBBBBBBbbbbkkkk.",
                "...kdddDDDDDDDDDdddk.....",
                "....kkkkkkkkkkkkkkkk.....",
            };
            Pal(steg, 0x9a6cdc, 0x6e46ac, 0xc4a0f2, 0xe2cff8, 0xc9b0ea, 0xf5b25e, 0xc97b2e);
            steg.LegX = new int[] { 4, 13 }; steg.HatX = 22; steg.HatY = 8; steg.EyeX = 22; steg.EyeY = 9; steg.FootX = 11; steg.LegShift = 2;
            Dinos[2] = steg;

            DinoDef trike = new DinoDef();
            trike.Name = "TRIKE";
            trike.Body = new string[] {
                "...............k.k.k.....",
                "..............kakakak....",
                ".............kaacacaak...",
                "............kaacccccak...",
                "............kacbbbbbbkk..",
                "............kacbbbbbbbbk.",
                "....kkkkkkkkkacbbbbwebbktk",
                "...khhhhhhhhkacbbbbeebbtk.",
                "..khbbbbabbbbkcbbpbbbbbbk.",
                ".kbbbbbbbbbbbbkbbbbbbbbbbtk",
                "kbbbabbbbbbBBBBkbbbbbbkkk..",
                "kdbbbbbbbbbBBBBBkkkkkkk....",
                ".kddddddddDDDDDDk..........",
                "..kkkkkkkkkkkkkkk..........",
            };
            Pal(trike, 0x5aa6f2, 0x3a78c6, 0x9ccdff, 0xd8ecff, 0xb4d4f4, 0xf2c46a, 0xc98a34);
            trike.Pal['t'] = G.Hex(0xfff3d6);
            trike.LegX = new int[] { 3, 11 }; trike.HatX = 19; trike.HatY = 3; trike.EyeX = 19; trike.EyeY = 6; trike.FootX = 10; trike.LegShift = 2;
            Dinos[3] = trike;
        }

        static void Pal(DinoDef d, int b, int dk, int h, int l, int m, int a, int c)
        {
            d.Pal['k'] = Outline; d.Pal['e'] = EyeC; d.Pal['w'] = White; d.Pal['p'] = Blush;
            d.Pal['b'] = G.Hex(b); d.Pal['d'] = G.Hex(dk); d.Pal['h'] = G.Hex(h);
            d.Pal['l'] = G.Hex(l); d.Pal['m'] = G.Hex(m); d.Pal['a'] = G.Hex(a); d.Pal['c'] = G.Hex(c);
            d.Pal['t'] = White;
        }

        // ---------- shirts: returns color for torso-marked pixel ----------
        static uint Hash(int x, int y, int s)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        static int ShirtColor(DinoDef d, int shirt, char mark, int x, int y, int topRow)
        {
            char low = char.ToLowerInvariant(mark);
            bool shade = (low == 'd' || low == 'm');
            bool belly = (low == 'l' || low == 'm');
            int ry = y - topRow;
            switch (shirt)
            {
                case 1: return shade ? G.Hex(0xb02838) : (belly ? G.Hex(0xf04858) : G.Hex(0xe03040));
                case 2: { int c = (ry % 2 == 0) ? G.Hex(0xf4f4ff) : G.Hex(0x2a3a8c); if (ry == 0) c = G.Hex(0x2a3a8c); return shade ? G.Mul(c, 0.75f) : c; }
                case 3: { int c = belly ? G.Hex(0x9aa0b0) : G.Hex(0x7a8090); if (shade) c = G.Hex(0x5a6070); if (belly && ry == 3) c = G.Hex(0x5a6070); return c; }
                case 4:
                    {
                        uint h = Hash(x, y, 7);
                        int c = G.Hex(0x22b8c8);
                        if (h % 7 == 0) c = G.Hex(0xff6fa8); else if (h % 7 == 1) c = G.Hex(0xffe066); else if (h % 7 == 2) c = G.Hex(0xffffff);
                        return shade ? G.Mul(c, 0.75f) : c;
                    }
                case 5: { if (belly) return (ry == 0) ? G.Hex(0xd02040) : G.Hex(0xf4f4f4); return shade ? G.Hex(0x101018) : G.Hex(0x22222e); }
                case 6:
                    {
                        uint h = Hash(x / 2, y / 2, 3) % 3;
                        int c = h == 0 ? G.Hex(0x5d7a3a) : (h == 1 ? G.Hex(0x7d9a4a) : G.Hex(0x4a5a2a));
                        return shade ? G.Mul(c, 0.8f) : c;
                    }
                case 7: { int c = (ry == 1 || ry == 2) ? G.Hex(0xffc928) : G.Hex(0x18181c); if (belly && ry >= 3 && (x % 4 == 0)) c = G.Hex(0xffc928); return shade ? G.Mul(c, 0.7f) : c; }
                case 8:
                    {
                        int[] rb = { 0xff4060, 0xff9a30, 0xffe040, 0x50d860, 0x40a0ff, 0xa060f0 };
                        int c = G.Hex(rb[Math.Abs(ry) % 6]);
                        return shade ? G.Mul(c, 0.8f) : c;
                    }
            }
            return d.Pal[low];
        }

        // ---------- legs ----------
        struct LegP { public int Dx, Lift; public LegP(int dx, int l) { Dx = dx; Lift = l; } }

        static void LegParams(Pose p, int phaseShift, out LegP near, out LegP far)
        {
            int rp = -1;
            switch (p)
            {
                case Pose.Run0: rp = 0; break;
                case Pose.Run1: rp = 1; break;
                case Pose.Run2: rp = 2; break;
                case Pose.Run3: rp = 3; break;
            }
            if (rp >= 0)
            {
                rp = (rp + phaseShift) & 3;
                switch (rp)
                {
                    case 0: near = new LegP(2, 0); far = new LegP(-2, 0); return;
                    case 1: near = new LegP(0, 0); far = new LegP(0, 2); return;
                    case 2: near = new LegP(-2, 0); far = new LegP(2, 0); return;
                    default: near = new LegP(0, 2); far = new LegP(0, 0); return;
                }
            }
            switch (p)
            {
                case Pose.Jump: near = new LegP(1, 2); far = new LegP(-1, 2); return;
                case Pose.Fall: near = new LegP(1, 0); far = new LegP(-1, 1); return;
                case Pose.Skid: near = new LegP(2, 0); far = new LegP(3, 0); return;
            }
            near = new LegP(0, 0); far = new LegP(0, 0);
        }

        static void ShoeColors(int shoes, out int main, out int sole, out int shine)
        {
            switch (shoes)
            {
                case 1: main = G.Hex(0xe8283c); sole = G.Hex(0xffffff); shine = G.Hex(0xff8a96); return;
                case 2: main = G.Hex(0xf2f2f8); sole = G.Hex(0x3a6ae0); shine = G.Hex(0xffffff); return;
                case 3: main = G.Hex(0x7a4a26); sole = G.Hex(0x3a2210); shine = G.Hex(0xa8703c); return;
                case 4: main = G.Hex(0xffb8d8); sole = G.Hex(0xffffff); shine = G.Hex(0xffffff); return;
                case 5: main = G.Hex(0x8890a0); sole = G.Hex(0xff7a20); shine = G.Hex(0xd0d8e8); return;
                case 6: main = G.Hex(0xffc928); sole = G.Hex(0xb07a10); shine = G.Hex(0xfff2a0); return;
            }
            main = sole = shine = 0;
        }

        static void DrawLeg(Sprite s, int x, int top, int lift, int fill, int shoes, int toe)
        {
            int k = Outline;
            int shin = 2 - lift; if (shin < 0) shin = 0;
            int y = top;
            for (int i = 0; i < shin; i++, y++) { s.Set(x, y, k); s.Set(x + 1, y, fill); s.Set(x + 2, y, k); }
            if (shoes == 0)
            {
                s.Set(x, y, k); s.Set(x + 1, y, fill); s.Set(x + 2, y, toe); s.Set(x + 3, y, k); y++;
                for (int i = 0; i < 4; i++) s.Set(x + i, y, k);
            }
            else
            {
                int main, sole, shine; ShoeColors(shoes, out main, out sole, out shine);
                if (shoes == 3 && shin > 0) { s.Set(x + 1, y - 1, main); }
                int w = shoes == 4 ? 5 : 4;
                int x0 = shoes == 4 ? x - 1 : x;
                s.Set(x0, y, k); for (int i = 1; i < w; i++) s.Set(x0 + i, y, main); s.Set(x0 + w, y, k);
                s.Set(x0 + 1, y, shine);
                if (shoes == 4) { s.Set(x0 + w - 1, y - 1, k); s.Set(x0 + w - 2, y - 1, White); }
                y++;
                s.Set(x0, y, k); for (int i = 1; i < w; i++) s.Set(x0 + i, y, sole); s.Set(x0 + w, y, k);
            }
        }

        // ---------- hats ----------
        static void Hat(Sprite s, int hat, int cx, int by, int frame)
        {
            if (hat == 0) return;
            string[] art = null; Dictionary<char, int> p = new Dictionary<char, int>();
            p['k'] = Outline; p['w'] = White;
            switch (hat)
            {
                case 1:
                    art = new string[] { "..kkkk...", ".krrrrk..", "krrwrrrkk.", "kRRRRRRRRk", ".kkkkkkkk." };
                    p['r'] = G.Hex(0xe8283c); p['R'] = G.Hex(0xb01c2c); break;
                case 2:
                    art = new string[] { "...kk...", "..kwwk..", ".kbbbbk.", "kbbbbbbk", "kBBBBBBk", "kkkkkkkk" };
                    p['b'] = G.Hex(0x3a8ce0); p['B'] = G.Hex(0x245ca8); break;
                case 3:
                    art = new string[] { "...kkkkk...", "..kbbbbbk..", "..kbbbbbk..", "..kBBBBBk..", "kkbbbbbbbkk", "kbbbbbbbbbk", ".kkkkkkkkk." };
                    p['b'] = G.Hex(0xa8743c); p['B'] = G.Hex(0x5a3a1a); break;
                case 4:
                    art = new string[] { "...y...", "..kpk..", "..kyk..", ".kpppk.", ".kyyyk.", "kpppppk", "kkkkkkk" };
                    p['p'] = G.Hex(0xff5aa8); p['y'] = G.Hex(0xffe066); break;
                case 5:
                    art = frame % 2 == 0
                        ? new string[] { "rrr.bbb", "...k...", "..kyk..", ".krrbbk", "krrrbbbk", "kkkkkkkk" }
                        : new string[] { ".bbbrr.", "...k...", "..kyk..", ".krrbbk", "krrrbbbk", "kkkkkkkk" };
                    p['r'] = G.Hex(0xe8283c); p['b'] = G.Hex(0x3a8ce0); p['y'] = G.Hex(0xffe066); break;
                case 6:
                    art = new string[] { ".kkkkkk.", ".kdddhk.", ".kdddhk.", ".kdddhk.", ".krrrrk.", "kkkkkkkk" };
                    p['d'] = G.Hex(0x22222e); p['h'] = G.Hex(0x4a4a5e); p['r'] = G.Hex(0xd02040); break;
                case 7:
                    art = new string[] { "k..k..k", "kyky.yk", "kyykyyk", "kyryjyk", "kYYYYYk", "kkkkkkk" };
                    art[1] = "kykykyk";
                    p['y'] = G.Hex(0xffd23a); p['Y'] = G.Hex(0xd89a18); p['r'] = G.Hex(0xe8283c); p['j'] = G.Hex(0x3ad0ff); break;
            }
            if (art == null) return;
            Sprite hs = G.Art(art, delegate (char c) { int v; return p.TryGetValue(c, out v) ? v : 0; });
            int x0 = cx - hs.W / 2, y0 = by - hs.H + 2;
            for (int y = 0; y < hs.H; y++) for (int x = 0; x < hs.W; x++) { int c = hs.P[y * hs.W + x]; if (c != 0) s.Set(x0 + x, y0 + y, c); }
        }

        static void GlassesDraw(Sprite s, int g, int ex, int ey)
        {
            int k = Outline;
            switch (g)
            {
                case 1: // shades
                    for (int x = ex - 1; x <= ex + 2; x++) for (int y = ey; y <= ey + 1; y++) s.Set(x, y, G.Hex(0x14141c));
                    s.Set(ex - 1, ey - 1, k); s.Set(ex + 2, ey - 1, k); for (int x = ex - 1; x <= ex + 2; x++) s.Set(x, ey - 1, k);
                    s.Set(ex, ey, G.Hex(0x7080a0));
                    s.Set(ex - 2, ey, k); s.Set(ex - 3, ey, k); break;
                case 2: // nerd round
                    for (int x = ex - 1; x <= ex + 2; x++) { s.Set(x, ey - 1, k); s.Set(x, ey + 2, k); }
                    s.Set(ex - 1, ey, k); s.Set(ex - 1, ey + 1, k); s.Set(ex + 2, ey, k); s.Set(ex + 2, ey + 1, k);
                    s.Set(ex, ey, White); s.Set(ex + 1, ey, EyeC); s.Set(ex, ey + 1, EyeC); s.Set(ex + 1, ey + 1, EyeC);
                    s.Set(ex - 2, ey, k); s.Set(ex - 3, ey, k); break;
                case 3: // 3D
                    for (int x = ex - 1; x <= ex + 1; x++) for (int y = ey; y <= ey + 1; y++) s.Set(x, y, G.Hex(0xe83040));
                    s.Set(ex + 2, ey, G.Hex(0x30a0ff)); s.Set(ex + 2, ey + 1, G.Hex(0x30a0ff));
                    for (int x = ex - 2; x <= ex + 3; x++) s.Set(x, ey - 1, White);
                    s.Set(ex - 3, ey, White); break;
                case 4: // aviators
                    for (int x = ex - 1; x <= ex + 2; x++) { s.Set(x, ey - 1, G.Hex(0xe0b040)); }
                    for (int x = ex - 1; x <= ex + 2; x++) for (int y = ey; y <= ey + 1; y++) s.Set(x, y, G.Hex(0x3a8070));
                    s.Set(ex, ey + 2, G.Hex(0x3a8070)); s.Set(ex + 1, ey + 2, G.Hex(0x3a8070));
                    s.Set(ex - 1, ey, G.Hex(0x9ae0d0));
                    s.Set(ex - 2, ey - 1, G.Hex(0xe0b040)); s.Set(ex - 3, ey - 1, G.Hex(0xe0b040)); break;
                case 5: // heart
                    {
                        int pk = G.Hex(0xff3a8a), hi = G.Hex(0xffa8cc);
                        s.Set(ex - 1, ey - 1, pk); s.Set(ex, ey - 1, pk); s.Set(ex + 2, ey - 1, pk); s.Set(ex + 3, ey - 1, pk);
                        for (int x = ex - 1; x <= ex + 3; x++) s.Set(x, ey, pk);
                        for (int x = ex; x <= ex + 2; x++) s.Set(x, ey + 1, pk);
                        s.Set(ex + 1, ey + 2, pk); s.Set(ex - 1, ey - 1, hi);
                        s.Set(ex - 2, ey, k); s.Set(ex - 3, ey, k); break;
                    }
                case 6: // star
                    {
                        int y1 = G.Hex(0xffd23a), y2 = G.Hex(0xfff3a0);
                        s.Set(ex + 1, ey - 2, y1);
                        for (int x = ex; x <= ex + 2; x++) s.Set(x, ey - 1, y1);
                        for (int x = ex - 1; x <= ex + 3; x++) s.Set(x, ey, y1);
                        for (int x = ex; x <= ex + 2; x++) s.Set(x, ey + 1, y1);
                        s.Set(ex - 1, ey + 2, y1); s.Set(ex + 3, ey + 2, y1);
                        s.Set(ex + 1, ey, y2);
                        s.Set(ex - 2, ey, k); s.Set(ex - 3, ey, k); break;
                    }
                case 7: // pixel deal-with-it
                    for (int x = ex - 3; x <= ex + 3; x++) s.Set(x, ey, k);
                    for (int x = ex - 1; x <= ex + 2; x++) s.Set(x, ey + 1, k);
                    s.Set(ex, ey + 2, k); s.Set(ex + 1, ey + 2, k);
                    s.Set(ex - 1, ey + 1, White); s.Set(ex, ey + 2, White); break;
            }
        }

        // ---------- compose a full frame ----------
        public const int PadTop = 9;

        public static Sprite Compose(Outfit o, Pose pose, int extraFrame, out int anchorX, out int anchorY)
        {
            DinoDef d = Dinos[o.Char];
            int bw = 0; foreach (string r in d.Body) bw = Math.Max(bw, r.Length);
            int bh = d.Body.Length;
            int W = bw + 6, H = PadTop + bh + 4;
            Sprite s = new Sprite(W, H);
            int ox = 3, oy = PadTop;
            int bob = (pose == Pose.Idle1 || pose == Pose.Run0 || pose == Pose.Run2) ? 1 : 0;
            int lean = pose == Pose.Skid ? -1 : 0;

            int bodyCol = d.Pal['b'], darkCol = d.Pal['d'];
            int legTop = oy + bh;
            // legs: far legs first (dark), then near legs
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < d.LegX.Length; i++)
                {
                    LegP near, far;
                    LegParams(pose, i == 0 ? d.LegShift : 0, out near, out far);
                    int lx = ox + d.LegX[i];
                    if (pass == 0) DrawLeg(s, lx + far.Dx, legTop - 1, far.Lift, darkCol, o.Shoes, darkCol);
                    else DrawLeg(s, lx + 4 + near.Dx, legTop - 1, near.Lift, bodyCol, o.Shoes, bodyCol);
                }

            // body
            int topTorso = 999;
            for (int y = 0; y < bh; y++) foreach (char ch in d.Body[y]) if (char.IsUpper(ch)) { topTorso = Math.Min(topTorso, y); break; }
            for (int y = 0; y < bh; y++)
            {
                string row = d.Body[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char ch = row[x];
                    if (ch == '.' || ch == ' ') continue;
                    int c;
                    if (char.IsUpper(ch)) c = o.Shirt == 0 ? d.Pal[char.ToLowerInvariant(ch)] : ShirtColor(d, o.Shirt, ch, x, y, topTorso);
                    else c = d.Pal[ch];
                    s.Set(ox + x + lean, oy + y + bob, c);
                }
            }
            // eyes
            int ex = ox + d.EyeX + lean, ey = oy + d.EyeY + bob;
            if (pose == Pose.Blink)
            {
                s.Set(ex, ey, bodyCol); s.Set(ex + 1, ey, bodyCol); s.Set(ex, ey + 1, Outline); s.Set(ex + 1, ey + 1, Outline);
            }
            else if (pose == Pose.Happy)
            {
                s.Set(ex, ey, bodyCol); s.Set(ex + 1, ey, bodyCol); s.Set(ex, ey + 1, bodyCol); s.Set(ex + 1, ey + 1, bodyCol);
                s.Set(ex - 1, ey + 1, Outline); s.Set(ex, ey, Outline); s.Set(ex + 1, ey, Outline); s.Set(ex + 2, ey + 1, Outline);
            }
            else if (pose == Pose.Skid)
            {
                s.Set(ex, ey, bodyCol); s.Set(ex + 1, ey, Outline);
            }
            if (o.Glasses != 0 && pose != Pose.Happy) GlassesDraw(s, o.Glasses, ex, ey);
            else if (o.Glasses != 0) GlassesDraw(s, o.Glasses, ex, ey);
            Hat(s, o.Hat, ox + d.HatX + lean, oy + d.HatY + bob, extraFrame);

            anchorX = ox + d.FootX + 2;
            anchorY = H - 1;
            return Trim(s, ref anchorX, ref anchorY);
        }

        static Sprite Trim(Sprite s, ref int ax, ref int ay)
        {
            int minY = s.H, maxY = -1;
            for (int y = 0; y < s.H; y++) for (int x = 0; x < s.W; x++) if (s.P[y * s.W + x] != 0) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxY < 0) return s;
            Sprite t = new Sprite(s.W, maxY - minY + 1);
            Array.Copy(s.P, minY * s.W, t.P, 0, t.P.Length);
            ay -= minY;
            if (ay > t.H - 1) ay = t.H - 1;
            return t;
        }

        // ---------- cached frame sets ----------
        public sealed class FrameSet
        {
            public Sprite[] F = new Sprite[(int)Pose.Count * 2];
            public int[] AX = new int[(int)Pose.Count * 2];
            public int[] AY = new int[(int)Pose.Count * 2];
            public int Hash;
            public Sprite Get(Pose p, int extra, out int ax, out int ay)
            {
                int i = (int)p * 2 + (extra & 1);
                ax = AX[i]; ay = AY[i]; return F[i];
            }
        }

        public static FrameSet Build(Outfit o)
        {
            FrameSet fs = new FrameSet();
            fs.Hash = o.Hash();
            for (int p = 0; p < (int)Pose.Count; p++)
                for (int e = 0; e < 2; e++)
                {
                    int ax, ay;
                    fs.F[p * 2 + e] = Compose(o, (Pose)p, e, out ax, out ay);
                    fs.AX[p * 2 + e] = ax; fs.AY[p * 2 + e] = ay;
                }
            return fs;
        }
    }
}
