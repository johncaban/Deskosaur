using System;
using System.Collections.Generic;

namespace Deskosaur
{
    public sealed class LevelDef
    {
        public string Name, Tip;
        public int Theme;
        public string[] Map;
        public string[] Ents;
        public LevelDef(string name, int theme, string tip, string[] map, params string[] ents)
        { Name = name; Theme = theme; Tip = tip; Map = map; Ents = ents; }
    }

    public struct OneWay
    {
        public float X1, X2, Y, PrevY;
        public int Kind, Id;
    }

    public sealed class DeskPlat
    {
        public float X1, X2, Y, DX, DY;
        public int Id;      // stable id (window/element hash)
        public int Kind;    // 0 window top, 1 desktop icon, 2 element inside window
        public float Glow;
    }

    public sealed class Mover
    {
        public float X0, Y0, W, RX, RY, Period, Phase;
        public float X, Y, PX, PY;
        public void At(int frame)
        {
            float t = (frame + Phase) / Period;
            float off = (1f - (float)Math.Cos(t * Math.PI * 2)) * 0.5f;
            X = X0 + RX * off; Y = Y0 + RY * off;
            float t2 = (frame - 1 + Phase) / Period;
            float off2 = (1f - (float)Math.Cos(t2 * Math.PI * 2)) * 0.5f;
            PX = X0 + RX * off2; PY = Y0 + RY * off2;
        }
    }

    public sealed class Wind { public float X, Y, W, H; public int Dir; /*0 up 1 right 2 left*/ public int TX, TY; }
    public sealed class Orb { public float X, Y; public int Cool; }
    public sealed class Portal { public float AX, AY, BX, BY; public int Color; }
    public sealed class Crumble { public int TX, TY; public int State; /*0 solid 1 shaking 2 gone*/ public int T; }
    public sealed class Pickup { public float X, Y; public bool Taken; public int Kind; /*0 gem 1 amber*/ public int Index; }
    public sealed class Checkpoint { public float X, Y; public bool On; }
    public sealed class Spring { public int TX, TY; public int Anim; }

    public sealed class World
    {
        public const int T = 16, Cols = 40, Rows = 22;
        public char[,] Tiles = new char[Cols, Rows];
        public int OX, OY;        // level origin in low-res screen pixels
        public int SW, SH;        // low-res screen size
        public float FloorY;
        public LevelDef Def;
        public int Theme;
        public bool BotMode;
        public bool UseDesk = true;

        public float SpawnX, SpawnY, ExitX, ExitY;
        public List<Wind> Winds = new List<Wind>();
        public List<Orb> Orbs = new List<Orb>();
        public List<Portal> Portals = new List<Portal>();
        public List<Mover> Movers = new List<Mover>();
        public List<Crumble> Crumbles = new List<Crumble>();
        public List<Pickup> Pickups = new List<Pickup>();
        public List<Checkpoint> Checkpoints = new List<Checkpoint>();
        public List<Spring> Springs = new List<Spring>();
        public List<DeskPlat> Desk = new List<DeskPlat>();
        public List<OneWay> OneWays = new List<OneWay>();
        List<OneWay> staticOneWays = new List<OneWay>();
        public int Frame;
        public int GemCount, AmberCount;

        public World(LevelDef def, int sw, int sh, int floorY)
        {
            Def = def; Theme = def.Theme; SW = sw; SH = sh;
            OX = (sw - Cols * T) / 2; if (OX < 0) OX = 0;
            OY = floorY - Rows * T;
            FloorY = floorY;
            string[] map = def.Map;
            int pad = Rows - map.Length;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    int my = y - pad;
                    char c = '.';
                    if (my >= 0 && my < map.Length && x < map[my].Length) c = map[my][x];
                    Tiles[x, y] = c;
                }
            Build();
        }

        public float TX(int tx) { return OX + tx * T; }
        public float TY(int ty) { return OY + ty * T; }

        void Build()
        {
            int amberIdx = 0;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    char c = Tiles[x, y];
                    float px = TX(x), py = TY(y);
                    switch (c)
                    {
                        case 'P': SpawnX = px + 3; SpawnY = py + T - 14; Tiles[x, y] = '.'; break;
                        case 'X': ExitX = px + 8; ExitY = py + 4; Tiles[x, y] = '.'; break;
                        case 'O': { Orb o = new Orb(); o.X = px + 8; o.Y = py + 8; Orbs.Add(o); Tiles[x, y] = '.'; break; }
                        case 'o': { Pickup p = new Pickup(); p.X = px + 8; p.Y = py + 8; p.Kind = 1; p.Index = amberIdx++; Pickups.Add(p); AmberCount++; Tiles[x, y] = '.'; break; }
                        case '*': { Pickup p = new Pickup(); p.X = px + 8; p.Y = py + 8; p.Kind = 0; Pickups.Add(p); GemCount++; Tiles[x, y] = '.'; break; }
                        case 'K': { Checkpoint k = new Checkpoint(); k.X = px + 8; k.Y = py + T; Checkpoints.Add(k); Tiles[x, y] = '.'; break; }
                        case 'C': { Crumble cr = new Crumble(); cr.TX = x; cr.TY = y; Crumbles.Add(cr); break; }
                        case 'S': { Spring s = new Spring(); s.TX = x; s.TY = y; Springs.Add(s); break; }
                    }
                }
            // winds
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    char c = Tiles[x, y];
                    if (c == 'U')
                    {
                        int len = 0; while (len < 8 && y - len - 1 >= 0 && !SolidTile(x, y - len - 1)) len++;
                        Wind w = new Wind(); w.Dir = 0; w.TX = x; w.TY = y;
                        w.X = TX(x) + 1; w.W = T - 2; w.Y = TY(y - len); w.H = len * T; Winds.Add(w);
                    }
                    else if (c == 'R' || c == 'L')
                    {
                        int d = c == 'R' ? 1 : -1;
                        int len = 0; while (len < 10 && x + d * (len + 1) >= 0 && x + d * (len + 1) < Cols && !SolidTile(x + d * (len + 1), y)) len++;
                        Wind w = new Wind(); w.Dir = c == 'R' ? 1 : 2; w.TX = x; w.TY = y;
                        w.Y = TY(y) + 1; w.H = T - 2; w.W = len * T;
                        w.X = d > 0 ? TX(x + 1) : TX(x - len);
                        Winds.Add(w);
                    }
                }
            // static one-ways ('=' and '-' runs, springs)
            for (int y = 0; y < Rows; y++)
            {
                int x = 0;
                while (x < Cols)
                {
                    char c = Tiles[x, y];
                    if (c == '=' || c == '-')
                    {
                        int x0 = x; while (x < Cols && Tiles[x, y] == c) x++;
                        OneWay o = new OneWay(); o.X1 = TX(x0); o.X2 = TX(x); o.Y = TY(y); o.PrevY = o.Y; o.Kind = c == '-' ? Phys.GK_ICE : Phys.GK_NORMAL; o.Id = -1;
                        staticOneWays.Add(o);
                    }
                    else x++;
                }
            }
            for (int i = 0; i < Springs.Count; i++)
            {
                OneWay o = new OneWay(); o.X1 = TX(Springs[i].TX) + 2; o.X2 = TX(Springs[i].TX) + 14; o.Y = TY(Springs[i].TY) + 7; o.PrevY = o.Y; o.Kind = Phys.GK_SPRING; o.Id = i;
                staticOneWays.Add(o);
            }
            // entities
            if (Def.Ents != null)
                foreach (string e in Def.Ents)
                {
                    string[] p = e.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length == 0) continue;
                    if (p[0] == "MH" || p[0] == "MV")
                    {
                        Mover m = new Mover();
                        m.X0 = TX(int.Parse(p[1])); m.Y0 = TY(int.Parse(p[2]));
                        m.W = int.Parse(p[3]) * T;
                        float r = float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture) * T;
                        if (p[0] == "MH") m.RX = r; else m.RY = r;
                        m.Period = p.Length > 5 ? int.Parse(p[5]) : 240;
                        m.Phase = p.Length > 6 ? int.Parse(p[6]) : 0;
                        m.At(0);
                        Movers.Add(m);
                    }
                    else if (p[0] == "T")
                    {
                        Portal pt = new Portal();
                        pt.AX = TX(int.Parse(p[1])) + 8; pt.AY = TY(int.Parse(p[2])) + 8;
                        pt.BX = TX(int.Parse(p[3])) + 8; pt.BY = TY(int.Parse(p[4])) + 8;
                        pt.Color = Portals.Count;
                        Portals.Add(pt);
                    }
                }
            SetTime(0);
        }

        public bool SolidTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Cols || y >= Rows) return false;
            char c = Tiles[x, y];
            switch (c)
            {
                case '#': case '~': case '<': case '>': case 'U': case 'L': case 'R': return true;
                case 'C':
                    if (BotMode) return true;
                    for (int i = 0; i < Crumbles.Count; i++) if (Crumbles[i].TX == x && Crumbles[i].TY == y) return Crumbles[i].State < 2;
                    return true;
            }
            return false;
        }

        public bool SolidPx(float px, float py)
        {
            int tx = (int)Math.Floor((px - OX) / T), ty = (int)Math.Floor((py - OY) / T);
            return SolidTile(tx, ty);
        }

        public char TileAtPx(float px, float py)
        {
            int tx = (int)Math.Floor((px - OX) / T), ty = (int)Math.Floor((py - OY) / T);
            if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) return '.';
            return Tiles[tx, ty];
        }

        // Rebuild per-frame one-way list for current time
        public void SetTime(int frame)
        {
            Frame = frame;
            OneWays.Clear();
            OneWays.AddRange(staticOneWays);
            for (int i = 0; i < Movers.Count; i++)
            {
                Mover m = Movers[i]; m.At(frame);
                OneWay o = new OneWay(); o.X1 = m.X; o.X2 = m.X + m.W; o.Y = m.Y; o.PrevY = m.PY; o.Kind = Phys.GK_MOVER; o.Id = i;
                OneWays.Add(o);
            }
            if (UseDesk && !BotMode)
                for (int i = 0; i < Desk.Count; i++)
                {
                    DeskPlat d = Desk[i];
                    OneWay o = new OneWay(); o.X1 = d.X1; o.X2 = d.X2; o.Y = d.Y; o.PrevY = d.Y - d.DY; o.Kind = Phys.GK_DESK; o.Id = d.Id;
                    OneWays.Add(o);
                }
        }

        public void Tick()
        {
            for (int i = 0; i < Orbs.Count; i++) if (Orbs[i].Cool > 0) Orbs[i].Cool--;
            for (int i = 0; i < Springs.Count; i++) if (Springs[i].Anim > 0) Springs[i].Anim--;
            for (int i = 0; i < Crumbles.Count; i++)
            {
                Crumble c = Crumbles[i];
                if (c.State == 1) { c.T++; if (c.T > 32) { c.State = 2; c.T = 0; CrumbleFell = c; } }
                else if (c.State == 2) { c.T++; if (c.T > 200 && !PlayerOverlapsTile(c.TX, c.TY)) { c.State = 0; c.T = 0; } }
            }
        }
        public Crumble CrumbleFell;
        public float PX, PY; // player box for overlap test
        bool PlayerOverlapsTile(int tx, int ty)
        {
            float x = TX(tx), y = TY(ty);
            return PX + Phys.W > x && PX < x + T && PY + Phys.H > y && PY < y + T;
        }

        public void TouchCrumble(int tx, int ty)
        {
            if (BotMode) return;
            for (int i = 0; i < Crumbles.Count; i++)
                if (Crumbles[i].TX == tx && Crumbles[i].TY == ty && Crumbles[i].State == 0) { Crumbles[i].State = 1; Crumbles[i].T = 0; }
        }

        public void WindAt(float x, float y, out float shift, out float fy, out bool up, out bool side)
        {
            shift = 0; fy = 0; up = false; side = false;
            float cx = x + Phys.W / 2, cy = y + Phys.H / 2;
            for (int i = 0; i < Winds.Count; i++)
            {
                Wind w = Winds[i];
                if (w.Dir == 0)
                {
                    if (x + Phys.W - 3 > w.X && x + 3 < w.X + w.W && y + Phys.H > w.Y && y < w.Y + w.H)
                    {
                        float t = (cy - w.Y) / w.H; if (t < 0) t = 0; if (t > 1) t = 1;
                        fy = -0.62f * (0.3f + 0.7f * t);
                        up = true;
                    }
                }
                else
                {
                    if (x + Phys.W > w.X && x < w.X + w.W && cy > w.Y - 2 && cy < w.Y + w.H + 2)
                    {
                        shift = w.Dir == 1 ? 1.7f : -1.7f; side = true;
                    }
                }
            }
        }

        public bool Hazard(float x, float y)
        {
            float x0 = x + 2, x1 = x + Phys.W - 2, y0 = y + 3, y1 = y + Phys.H - 1;
            int tx0 = (int)Math.Floor((x0 - OX) / T), tx1 = (int)Math.Floor((x1 - OX) / T);
            int ty0 = (int)Math.Floor((y0 - OY) / T), ty1 = (int)Math.Floor((y1 - OY) / T);
            for (int ty = ty0; ty <= ty1; ty++)
                for (int tx = tx0; tx <= tx1; tx++)
                {
                    if (tx < 0 || ty < 0 || tx >= Cols || ty >= Rows) continue;
                    char c = Tiles[tx, ty];
                    if (c == '^')
                    {
                        float hy = TY(ty) + 9;
                        if (y1 > hy && x1 > TX(tx) + 2 && x0 < TX(tx) + 14) return true;
                    }
                    else if (c == 'v')
                    {
                        float hy = TY(ty) + 7;
                        if (y0 < hy && x1 > TX(tx) + 2 && x0 < TX(tx) + 14) return true;
                    }
                }
            return false;
        }

        public bool TryOrb(float x, float y)
        {
            float cx = x + Phys.W / 2, cy = y + Phys.H / 2;
            for (int i = 0; i < Orbs.Count; i++)
            {
                Orb o = Orbs[i];
                if (o.Cool > 0) continue;
                if (Math.Abs(cx - o.X) < 10 && Math.Abs(cy - o.Y) < 11)
                {
                    if (!BotMode) o.Cool = 150;
                    LastOrb = o;
                    return true;
                }
            }
            return false;
        }
        public Orb LastOrb;

        public bool TryPortal(float x, float y, out float nx, out float ny, out Portal used)
        {
            float cx = x + Phys.W / 2, cy = y + Phys.H / 2;
            for (int i = 0; i < Portals.Count; i++)
            {
                Portal p = Portals[i];
                if (Math.Abs(cx - p.AX) < 7 && Math.Abs(cy - p.AY) < 9) { nx = p.BX; ny = p.BY; used = p; return true; }
                if (Math.Abs(cx - p.BX) < 7 && Math.Abs(cy - p.BY) < 9) { nx = p.AX; ny = p.AY; used = p; return true; }
            }
            nx = ny = 0; used = null; return false;
        }

        public DeskPlat FindDesk(int id)
        {
            for (int i = 0; i < Desk.Count; i++) if (Desk[i].Id == id) return Desk[i];
            return null;
        }
    }
}
