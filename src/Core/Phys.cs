using System;

namespace Deskosaur
{
    public struct InputState
    {
        public int Dir;      // -1, 0, 1
        public bool Jump;    // held
        public bool Down;    // held
    }

    public struct PState
    {
        public float X, Y, VX, VY;
        public bool OnGround, OnOneWay, JumpHeldPrev, CanCut, Skid, Dead, Won, UpWind, SideWind;
        public int GroundKind, RideId, GroundTX, GroundTY;
        public int Coyote, JumpBuf, AirJumps, DropTimer, PortalCD, Facing;
        public int Events, SpringId;
        public float LandVY;
    }

    public static class Phys
    {
        public const float W = 10, H = 14;
        public const float Grav = 0.28f, MaxFall = 6f, JumpV = -5.3f, DJumpV = -4.7f, SpringV = -8.2f;

        public const int GK_NORMAL = 0, GK_ICE = 1, GK_CONVL = 2, GK_CONVR = 3, GK_MOVER = 4, GK_DESK = 5, GK_SPRING = 6, GK_CRUMBLE = 7;
        public const int EV_JUMP = 1, EV_DJUMP = 2, EV_LAND = 4, EV_SPRING = 8, EV_ORB = 16, EV_PORTAL = 32, EV_DEAD = 64, EV_WIN = 128;

        public static PState Spawn(float x, float y)
        {
            PState p = new PState();
            p.X = x; p.Y = y; p.Facing = 1; p.AirJumps = 1; p.RideId = -1;
            return p;
        }

        static float Approach(float v, float t, float r)
        {
            if (v < t) { v += r; if (v > t) v = t; }
            else if (v > t) { v -= r; if (v < t) v = t; }
            return v;
        }

        public static void Step(ref PState p, InputState inp, World w)
        {
            p.Events = 0;
            if (p.Dead || p.Won) return;
            if (p.Coyote > 0) p.Coyote--;
            if (p.JumpBuf > 0) p.JumpBuf--;
            if (p.DropTimer > 0) p.DropTimer--;
            if (p.PortalCD > 0) p.PortalCD--;
            bool jumpPressed = inp.Jump && !p.JumpHeldPrev;
            p.JumpHeldPrev = inp.Jump;
            if (jumpPressed) p.JumpBuf = 7;

            float windShift, windY; bool upWind, sideWind;
            w.WindAt(p.X, p.Y, out windShift, out windY, out upWind, out sideWind);
            p.UpWind = upWind; p.SideWind = sideWind;

            // ---------- horizontal ----------
            int surf = p.OnGround ? p.GroundKind : -1;
            float acc, dec, turn, max = 2.2f;
            if (surf < 0) { acc = 0.20f; dec = 0.04f; turn = 0.30f; }
            else if (surf == GK_ICE) { acc = 0.07f; dec = 0.02f; turn = 0.085f; max = 2.8f; }
            else { acc = 0.32f; dec = 0.40f; turn = 0.55f; }
            int dir = inp.Dir;
            float target = dir * max + windShift;
            float rate;
            if (dir == 0) rate = sideWind ? Math.Max(dec, 0.06f) : dec;
            else if (p.VX * dir < -0.01f) rate = turn;
            else if (Math.Abs(p.VX) > Math.Abs(target) && Math.Sign(p.VX) == Math.Sign(target)) rate = surf < 0 ? 0.03f : 0.12f;
            else rate = acc;
            p.VX = Approach(p.VX, target, rate);
            if (p.VX > 5.5f) p.VX = 5.5f; if (p.VX < -5.5f) p.VX = -5.5f;
            p.Skid = p.OnGround && dir != 0 && p.VX * dir < -0.6f;
            if (dir != 0) p.Facing = dir;

            // ---------- jumping ----------
            bool jumpedNow = false;
            if (p.JumpBuf > 0)
            {
                if (p.OnGround || p.Coyote > 0)
                {
                    p.VY = JumpV; p.OnGround = false; p.Coyote = 0; p.JumpBuf = 0; p.CanCut = true;
                    p.Events |= EV_JUMP; jumpedNow = true;
                }
                else if (p.AirJumps > 0)
                {
                    p.VY = DJumpV; p.AirJumps--; p.JumpBuf = 0; p.CanCut = true;
                    p.Events |= EV_DJUMP; jumpedNow = true;
                }
            }
            if (!inp.Jump && p.CanCut && p.VY < -2f) { p.VY = -2f; p.CanCut = false; }
            if (p.VY >= 0) p.CanCut = false;

            // ---------- gravity / wind ----------
            p.VY += Grav + windY;
            if (upWind) { if (p.VY < -4.2f) p.VY = -4.2f; p.AirJumps = 1; p.CanCut = false; }
            if (p.VY > MaxFall) p.VY = MaxFall;

            // ---------- drop through one-way ----------
            if (inp.Down && p.OnGround && p.OnOneWay && p.GroundKind != GK_SPRING && !jumpedNow)
            {
                p.DropTimer = 12; p.OnGround = false; p.Y += 2;
            }

            // ---------- carry (conveyors / movers / desktop windows) ----------
            float cx = 0, cy = 0;
            if (p.OnGround && !jumpedNow)
            {
                if (p.GroundKind == GK_CONVL) cx = -1.2f;
                else if (p.GroundKind == GK_CONVR) cx = 1.2f;
                else if (p.GroundKind == GK_MOVER && p.RideId >= 0 && p.RideId < w.Movers.Count)
                {
                    Mover m = w.Movers[p.RideId]; cx = m.X - m.PX; cy = m.Y - m.PY;
                }
                else if (p.GroundKind == GK_DESK)
                {
                    DeskPlat d = w.FindDesk(p.RideId);
                    if (d != null) { cx = d.DX; cy = d.DY; }
                }
            }

            bool wasGround = p.OnGround;
            float vyBefore = p.VY;
            p.Y += cy;
            MoveX(ref p, p.VX + cx, w);
            p.OnGround = false; p.OnOneWay = false;
            MoveY(ref p, w);

            if (p.X < 0) { p.X = 0; if (p.VX < 0) p.VX = 0; }
            if (p.X > w.SW - W) { p.X = w.SW - W; if (p.VX > 0) p.VX = 0; }
            if (p.Y < -120) { p.Y = -120; if (p.VY < 0) p.VY = 0; }

            if (p.OnGround)
            {
                if (!wasGround) { p.Events |= EV_LAND; p.LandVY = vyBefore; }
                p.AirJumps = 1;
                if (p.GroundKind == GK_SPRING)
                {
                    p.VY = SpringV; p.OnGround = false; p.CanCut = false; p.AirJumps = 1;
                    p.Events |= EV_SPRING; p.SpringId = p.RideId;
                }
                else if (p.GroundKind == GK_CRUMBLE) w.TouchCrumble(p.GroundTX, p.GroundTY);
            }
            else if (wasGround && !jumpedNow && p.VY >= 0) p.Coyote = 6;

            // ---------- interactions ----------
            if (w.Hazard(p.X, p.Y) || p.Y > w.FloorY + 40) { p.Dead = true; p.Events |= EV_DEAD; return; }
            if (p.AirJumps == 0 && w.TryOrb(p.X, p.Y)) { p.AirJumps = 1; p.Events |= EV_ORB; }
            if (p.PortalCD == 0)
            {
                float nx, ny; Portal used;
                if (w.TryPortal(p.X, p.Y, out nx, out ny, out used))
                {
                    p.X = nx - W / 2; p.Y = ny - H / 2; p.PortalCD = 40; p.Events |= EV_PORTAL;
                    p.OnGround = false;
                }
            }
            float dx = p.X + W / 2 - w.ExitX, dy = p.Y + H / 2 - w.ExitY;
            if (dx * dx + dy * dy < 13 * 13) { p.Won = true; p.Events |= EV_WIN; }
        }

        static void MoveX(ref PState p, float vx, World w)
        {
            int steps = (int)Math.Ceiling(Math.Abs(vx) / 4f);
            if (steps < 1) steps = 1;
            float st = vx / steps;
            for (int i = 0; i < steps; i++)
            {
                float nx = p.X + st;
                float edge = st > 0 ? nx + W - 0.001f : nx;
                bool hit = false;
                for (float yy = p.Y + 0.5f; ; yy += 8f)
                {
                    float y = Math.Min(yy, p.Y + H - 0.5f);
                    if (w.SolidPx(edge, y)) { hit = true; break; }
                    if (y >= p.Y + H - 0.5f) break;
                }
                if (hit)
                {
                    int tx = (int)Math.Floor((edge - w.OX) / World.T);
                    if (st > 0) p.X = w.OX + tx * World.T - W; else p.X = w.OX + (tx + 1) * World.T;
                    p.VX = 0;
                    return;
                }
                p.X = nx;
            }
        }

        static void MoveY(ref PState p, World w)
        {
            float vy = p.VY;
            int steps = (int)Math.Ceiling(Math.Abs(vy) / 4f);
            if (steps < 1) steps = 1;
            float st = vy / steps;
            for (int i = 0; i < steps; i++)
            {
                float prevBottom = p.Y + H;
                float ny = p.Y + st;
                float edge = st > 0 ? ny + H - 0.001f : ny;
                // solid tiles
                bool hit = false; char tileHit = '.';
                for (float xx = p.X + 0.5f; ; xx += 8f)
                {
                    float x = Math.Min(xx, p.X + W - 0.5f);
                    if (w.SolidPx(x, edge)) { hit = true; tileHit = w.TileAtPx(x, edge); if (tileHit != 'C') break; }
                    if (x >= p.X + W - 0.5f) break;
                }
                if (hit)
                {
                    int ty = (int)Math.Floor((edge - w.OY) / World.T);
                    if (st > 0)
                    {
                        p.Y = w.OY + ty * World.T - H; p.VY = 0; p.OnGround = true;
                        char g = w.TileAtPx(p.X + W / 2, edge);
                        if (!w.SolidPx(p.X + W / 2, edge)) g = tileHit;
                        p.GroundKind = g == '~' ? GK_ICE : g == '<' ? GK_CONVL : g == '>' ? GK_CONVR : g == 'C' ? GK_CRUMBLE : GK_NORMAL;
                        p.GroundTX = (int)Math.Floor((p.X + W / 2 - w.OX) / World.T);
                        p.GroundTY = ty;
                        if (g != 'C' && tileHit == 'C')
                        {
                            p.GroundKind = GK_CRUMBLE;
                            for (float xx = p.X + 0.5f; xx <= p.X + W; xx += 4f)
                                if (w.TileAtPx(xx, edge) == 'C') { p.GroundTX = (int)Math.Floor((xx - w.OX) / World.T); break; }
                        }
                        p.RideId = -1;
                    }
                    else { p.Y = w.OY + (ty + 1) * World.T; p.VY = 0; }
                    return;
                }
                // one-way platforms
                if (st >= 0 && p.DropTimer == 0)
                {
                    float newBottom = ny + H;
                    int best = -1; float bestY = float.MaxValue;
                    for (int k = 0; k < w.OneWays.Count; k++)
                    {
                        OneWay o = w.OneWays[k];
                        if (p.X + W <= o.X1 + 1 || p.X >= o.X2 - 1) continue;
                        if (prevBottom <= Math.Max(o.Y, o.PrevY) + 0.6f && newBottom >= o.Y && o.Y < bestY) { best = k; bestY = o.Y; }
                    }
                    if (best >= 0)
                    {
                        OneWay o = w.OneWays[best];
                        p.Y = o.Y - H; p.VY = 0; p.OnGround = true; p.OnOneWay = true;
                        p.GroundKind = o.Kind; p.RideId = o.Id;
                        return;
                    }
                }
                p.Y = ny;
            }
        }
    }
}
