using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Deskosaur
{
    // Solver bot: weighted A* over macro-actions using the REAL game physics (no desktop platforms).
    public static class Bot
    {
        struct Node { public PState S; public int Frame; public int Parent; public byte Act; }

        static readonly int[] ADir = { -1, 0, 1, -1, 0, 1, 0 };
        static readonly bool[] AJump = { false, false, false, true, true, true, false };
        static readonly bool[] ADown = { false, false, false, false, false, false, true };
        const int Macro = 4;
        public static float Weight = 2.0f;
        public static float BestH = 1e9f, BestX, BestY;
        public static List<byte> LastActions = new List<byte>();
        public static int ActDir(byte a) { return ADir[a]; }
        public static bool ActJump(byte a) { return AJump[a]; }
        public static bool ActDown(byte a) { return ADown[a]; }

        sealed class Heap
        {
            List<float> pr = new List<float>(); List<int> id = new List<int>();
            public int Count { get { return id.Count; } }
            public void Push(float p, int i)
            {
                pr.Add(p); id.Add(i); int c = id.Count - 1;
                while (c > 0) { int pa = (c - 1) / 2; if (pr[pa] <= pr[c]) break; Swap(c, pa); c = pa; }
            }
            public int Pop()
            {
                int r = id[0]; int last = id.Count - 1;
                pr[0] = pr[last]; id[0] = id[last]; pr.RemoveAt(last); id.RemoveAt(last);
                int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, rr = l + 1, m = c;
                    if (l < id.Count && pr[l] < pr[m]) m = l;
                    if (rr < id.Count && pr[rr] < pr[m]) m = rr;
                    if (m == c) break; Swap(c, m); c = m;
                }
                return r;
            }
            void Swap(int a, int b) { float t = pr[a]; pr[a] = pr[b]; pr[b] = t; int u = id[a]; id[a] = id[b]; id[b] = u; }
        }

        public static List<PState> Solve(World w, PState start, float gx, float gy, bool exitGoal, int maxNodes, out int expanded, out int frames)
        {
            bool movers = w.Movers.Count > 0;
            List<Node> nodes = new List<Node>();
            HashSet<long> seen = new HashSet<long>();
            Heap open = new Heap();
            Node n0 = new Node(); n0.S = start; n0.Frame = 0; n0.Parent = -1;
            nodes.Add(n0); open.Push(0, 0);
            expanded = 0; frames = 0;
            int goal = -1;
            while (open.Count > 0 && expanded < maxNodes)
            {
                int ci = open.Pop(); Node cur = nodes[ci]; expanded++;
                for (int a = 0; a < 7; a++)
                {
                    if (ADown[a] && !(cur.S.OnGround && cur.S.OnOneWay)) continue;
                    PState s = cur.S; int f = cur.Frame;
                    InputState inp = new InputState(); inp.Dir = ADir[a]; inp.Jump = AJump[a]; inp.Down = ADown[a];
                    bool dead = false, won = false, hit = false;
                    for (int k = 0; k < Macro; k++)
                    {
                        f++; w.SetTime(f);
                        Phys.Step(ref s, inp, w);
                        if (s.Dead) { dead = true; break; }
                        if (s.Won) { won = true; break; }
                        if (!exitGoal)
                        {
                            float dx = s.X + Phys.W / 2 - gx, dy = s.Y + Phys.H / 2 - gy;
                            if (Math.Abs(dx) < 10 && Math.Abs(dy) < 11) { hit = true; break; }
                        }
                    }
                    if (dead) continue;
                    if (won && !exitGoal) continue;
                    Node nn = new Node(); nn.S = s; nn.Frame = f; nn.Parent = ci; nn.Act = (byte)a;
                    if ((exitGoal && won) || hit) { nodes.Add(nn); goal = nodes.Count - 1; break; }
                    long key = Key(s, movers && NearMover(w, s) ? 1 + (f % 480) / 8 : 0);
                    if (!seen.Add(key)) continue;
                    nodes.Add(nn);
                    float hx = s.X + Phys.W / 2 - gx, hy = s.Y + Phys.H / 2 - gy;
                    float h = (float)Math.Sqrt(hx * hx + hy * hy) / 3f;
                    if (h < BestH) { BestH = h; BestX = s.X; BestY = s.Y; }
                    open.Push(f + Weight * h, nodes.Count - 1);
                }
                if (goal >= 0) break;
            }
            if (goal < 0) return null;
            // rebuild trajectory frame-by-frame
            List<int> chain = new List<int>();
            for (int i = goal; i >= 0; i = nodes[i].Parent) chain.Add(i);
            chain.Reverse();
            LastActions.Clear(); for (int c = 1; c < chain.Count; c++) LastActions.Add(nodes[chain[c]].Act);
            List<PState> traj = new List<PState>();
            PState st = start; int fr = 0; traj.Add(st);
            for (int c = 1; c < chain.Count; c++)
            {
                Node nd = nodes[chain[c]];
                InputState inp = new InputState(); inp.Dir = ADir[nd.Act]; inp.Jump = AJump[nd.Act]; inp.Down = ADown[nd.Act];
                for (int k = 0; k < Macro && fr < nd.Frame; k++) { fr++; w.SetTime(fr); Phys.Step(ref st, inp, w); traj.Add(st); }
            }
            frames = fr;
            return traj;
        }

        static bool NearMover(World w, PState s)
        {
            foreach (Mover m in w.Movers)
            {
                float x0 = m.X0 - 128, x1 = m.X0 + m.W + m.RX + 128, y0 = m.Y0 - 96, y1 = m.Y0 + m.RY + 48;
                if (s.X > x0 && s.X < x1 && s.Y > y0 && s.Y < y1) return true;
            }
            return false;
        }

        static long Key(PState s, int phase)
        {
            long qx = (long)Math.Max(0, Math.Min(1023, (int)(s.X / 2)));
            long qy = (long)Math.Max(0, Math.Min(1023, (int)((s.Y + 200) / 2)));
            long qvx = Math.Max(0, Math.Min(31, (int)Math.Round(s.VX * 2) + 15));
            long qvy = Math.Max(0, Math.Min(31, (int)Math.Round(s.VY) + 12));
            long k = qx;
            k = k * 1024 + qy; k = k * 32 + qvx; k = k * 32 + qvy;
            k = k * 2 + (s.AirJumps > 0 ? 1 : 0);
            k = k * 2 + (s.OnGround ? 1 : 0);
            k = k * 2 + (s.JumpHeldPrev ? 1 : 0);
            k = k * 2 + (s.CanCut ? 1 : 0);
            k = k * 2 + (s.Coyote > 0 ? 1 : 0);
            k = k * 64 + phase;
            return k;
        }
    }

    public static class BotMain
    {
        public static int MaxNodes = 1500000;
        public static int Main(string[] args)
        {
            int only = args.Length > 0 ? int.Parse(args[0]) : -1;
            if (args.Length > 2) Bot.Weight = float.Parse(args[2]);
            if (Environment.GetEnvironmentVariable("BOTN") != null) MaxNodes = int.Parse(Environment.GetEnvironmentVariable("BOTN"));
            bool ambers = args.Length > 1 && args[1] == "a";
            int fails = 0;
            for (int li = 0; li < Levels.All.Length; li++)
            {
                if (only >= 0 && li != only) continue;
                LevelDef def = Levels.All[li];
                foreach (string row in def.Map) if (row.Length > 40) Console.WriteLine("  !! row too long in " + def.Name + ": " + row.Length);
                World w = new World(def, 640, 360, 360);
                w.BotMode = true; w.UseDesk = false;
                PState s0 = Phys.Spawn(w.SpawnX, w.SpawnY);
                if (args.Length > 4) s0 = Phys.Spawn(w.TX(int.Parse(args[3])) + 3, w.TY(int.Parse(args[4])) + 2);
                Stopwatch sw = Stopwatch.StartNew();
                int exp, frames;
                List<PState> path = Bot.Solve(w, s0, w.ExitX, w.ExitY, true, MaxNodes, out exp, out frames);
                if (path == null) Console.WriteLine("   closest: tile " + (int)((Bot.BestX - w.OX) / 16) + "," + (int)((Bot.BestY - w.OY) / 16));
                Bot.BestH = 1e9f;
                string res = path != null ? ("OK  " + (frames / 60f).ToString("0.0") + "s") : "FAIL";
                if (path == null) fails++;
                Console.WriteLine(string.Format("L{0,2} {1,-16} {2,-10} nodes={3,8} amber={5} {4}ms", li + 1, def.Name, res, exp, sw.ElapsedMilliseconds, w.AmberCount));
                List<List<PState>> extra = new List<List<PState>>();
                if (ambers)
                {
                    foreach (Pickup p in w.Pickups)
                    {
                        if (p.Kind != 1) continue;
                        List<PState> ap = Bot.Solve(w, s0, p.X, p.Y, false, 800000, out exp, out frames);
                        Console.WriteLine("      amber " + p.Index + " @(" + (int)((p.X - w.OX) / 16) + "," + (int)((p.Y - w.OY) / 16) + "): " + (ap != null ? "reachable" : "NOT FOUND (" + exp + ")"));
                        if (ap != null)
                        {
                            extra.Add(ap);
                            PState last = ap[ap.Count - 1]; last.CanCut = false;
                            List<PState> back = Bot.Solve(w, last, w.ExitX, w.ExitY, true, 800000, out exp, out frames);
                            if (back == null) Console.WriteLine("        !! exit not reachable after this amber  last: x=" + last.X + " y=" + last.Y + " vx=" + last.VX + " vy=" + last.VY + " air=" + last.AirJumps + " gnd=" + last.OnGround + " dead=" + last.Dead + " won=" + last.Won);
                        }
                    }
                }
                Render(w, path, extra, "out/bot_L" + (li + 1).ToString("00") + ".ppm");
            }
            Console.WriteLine(fails == 0 ? "ALL LEVELS BEATABLE" : (fails + " level(s) failed"));
            return fails;
        }

        static void Render(World w, List<PState> path, List<List<PState>> extra, string file)
        {
            Surface s = new Surface(640, 360);
            G.Clear(s, G.Hex(0x202030));
            for (int y = 0; y < World.Rows; y++)
                for (int x = 0; x < World.Cols; x++)
                {
                    char c = w.Tiles[x, y]; int col = 0;
                    switch (c)
                    {
                        case '#': col = 0x707088; break; case '~': col = 0x90e0ff; break; case '=': col = 0xc09060; break;
                        case '-': col = 0xb0f0ff; break; case '<': case '>': col = 0x908050; break; case 'C': col = 0xa07050; break;
                        case '^': case 'v': col = 0xff3040; break; case 'U': case 'L': case 'R': col = 0x40c040; break; case 'S': col = 0xffd040; break;
                    }
                    if (col != 0)
                    {
                        int h = (c == '=' || c == '-') ? 4 : 16;
                        G.Rect(s, (int)w.TX(x), (int)w.TY(y) + (c == '^' ? 8 : 0), 16, c == '^' || c == 'v' ? 8 : h, G.Hex(col));
                    }
                }
            foreach (Wind wd in w.Winds) G.RectO(s, (int)wd.X, (int)wd.Y, (int)wd.W, (int)wd.H, G.Hex(0x40a040));
            foreach (Orb o in w.Orbs) G.Disc(s, (int)o.X, (int)o.Y, 4, G.Hex(0x40ff80));
            foreach (Pickup p in w.Pickups) G.Disc(s, (int)p.X, (int)p.Y, p.Kind == 1 ? 4 : 2, G.Hex(p.Kind == 1 ? 0xffa020 : 0x40e0ff));
            foreach (Portal p in w.Portals) { G.Ring(s, (int)p.AX, (int)p.AY, 6, G.Hex(0xc060ff)); G.Ring(s, (int)p.BX, (int)p.BY, 6, G.Hex(0xc060ff)); }
            foreach (Mover m in w.Movers) { G.RectO(s, (int)m.X0, (int)m.Y0, (int)(m.W + m.RX), (int)(4 + m.RY), G.Hex(0x806040)); }
            G.Ring(s, (int)w.ExitX, (int)w.ExitY, 9, G.Hex(0xffffff));
            G.Rect(s, (int)w.SpawnX, (int)w.SpawnY, 10, 14, G.Hex(0x40ff40));
            foreach (List<PState> ep in extra) foreach (PState p in ep) G.Px(s, (int)(p.X + 5), (int)(p.Y + 7), G.Hex(0xffa020));
            if (path != null) foreach (PState p in path) G.Px(s, (int)(p.X + 5), (int)(p.Y + 7), G.Hex(0xffffff));
            Img.Save(s, file, 2);
        }
    }
}
