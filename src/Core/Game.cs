using System;
using System.Collections.Generic;

namespace Deskosaur
{
    public enum GameReq { None, Menu, Quit }
    enum GState { Play, Dead, Hatch, Win, Clear, Paused }

    public sealed class Game
    {
        public Surface Fb;
        Surface stat;
        public World W;
        PState p;
        SaveData save;
        Particles fx = new Particles();
        Art.FrameSet frames;
        public int Level;
        GState state, prevState;
        public GameReq Request;
        int t, levelTime, stateT;
        float respawnX, respawnY;
        int runMask, gems;
        int deaths;
        float runPhase; int blinkT = 120, idleT, landT, jumpT, spinT, introT;
        float winX, winY;
        int menuSel;
        bool newBest;
        int floorY;
        List<DeskPlat> pendingDesk;
        public bool AllCleared;

        public bool CapturingKeys { get { return state != GState.Paused; } }
        public bool WantsMouse { get { return state == GState.Paused || state == GState.Clear; } }

        public Game(int sw, int sh, int floorY, SaveData save)
        {
            Fb = new Surface(sw, sh);
            stat = new Surface(sw, sh);
            this.save = save;
            this.floorY = floorY;
        }

        public void Load(int idx)
        {
            Level = Math.Max(0, Math.Min(Levels.All.Length - 1, idx));
            W = new World(Levels.All[Level], Fb.W, Fb.H, floorY);
            W.UseDesk = save.DeskOn;
            if (pendingDesk != null) W.Desk = pendingDesk;
            G.Clear(stat, G.Key);
            Tiles.DrawStatic(stat, W);
            frames = Art.Build(save.Look);
            respawnX = W.SpawnX; respawnY = W.SpawnY;
            runMask = 0; gems = 0; deaths = 0;
            fx.Clear();
            levelTime = 0; introT = 0;
            Request = GameReq.None;
            SpawnPlayer(true);
            Sfx.Play("start");
        }

        void SpawnPlayer(bool first)
        {
            p = Phys.Spawn(respawnX, respawnY);
            state = GState.Hatch; stateT = 0;
            landT = jumpT = spinT = 0;
            if (!first) Sfx.Play("respawn");
        }

        // ---------- desktop platforms from the platform scanner ----------
        public void SetDesk(List<DeskPlat> list)
        {
            pendingDesk = list;
            if (W == null) return;
            if (!save.DeskOn) { W.Desk = new List<DeskPlat>(); return; }
            // keep glow values for continuity
            foreach (DeskPlat d in list)
            {
                DeskPlat old = W.FindDesk(d.Id);
                if (old != null) d.Glow = old.Glow;
            }
            W.Desk = list;
        }

        // ---------- main update ----------
        public void Tick(Input inp)
        {
            t++;
            stateT++;
            if (frames == null || frames.Hash != save.Look.Hash()) frames = Art.Build(save.Look);
            W.UseDesk = save.DeskOn;

            if (inp.P(Key.Back) || inp.P(Key.Pause))
            {
                if (state == GState.Paused) { state = prevState; Sfx.Play("back"); return; }
                else if (state != GState.Clear) { prevState = state; state = GState.Paused; stateT = 0; menuSel = 0; Sfx.Play("pause"); return; }
            }
            if (state == GState.Paused) { PauseInput(inp); return; }
            if (state == GState.Clear) { ClearInput(inp); fx.Update(); W.Tick(); return; }
            if (inp.P(Key.Restart) && state != GState.Win) { Sfx.Play("back"); Load(Level); return; }

            if (state == GState.Play || state == GState.Hatch) levelTime++;
            introT++;

            W.SetTime(t);
            W.PX = p.X; W.PY = p.Y;
            W.Tick();
            if (W.CrumbleFell != null)
            {
                Crumble c = W.CrumbleFell; W.CrumbleFell = null;
                for (int i = 0; i < 10; i++)
                    fx.Spawn(PT.Debris, W.TX(c.TX) + Particles.R(2, 14), W.TY(c.TY) + Particles.R(2, 14), Particles.R(-1, 1), Particles.R(-1, 0.5f), 40, G.Hex(0xc89466), 3, 0.2f);
                Sfx.Play("crumble");
            }
            AmbientFx();

            switch (state)
            {
                case GState.Hatch:
                    if (stateT > 26) { state = GState.Play; stateT = 0; for (int i = 0; i < 8; i++) fx.Spawn(PT.Spark, p.X + 5, p.Y + 8, Particles.R(-1.5f, 1.5f), Particles.R(-2.5f, -0.5f), 30, G.Hex(0xfff4dc), 2, 0.15f); }
                    break;
                case GState.Play: PlayTick(inp); break;
                case GState.Dead:
                    if (stateT > 40) SpawnPlayer(false);
                    break;
                case GState.Win: WinTick(); break;
            }
            foreach (DeskPlat d in W.Desk) { d.DX = 0; d.DY = 0; }
            fx.Update();
        }

        void PlayTick(Input inp)
        {
            InputState i = new InputState();
            i.Dir = (inp.D(Key.Left) ? -1 : 0) + (inp.D(Key.Right) ? 1 : 0);
            i.Jump = inp.D(Key.Jump) || inp.D(Key.Up);
            i.Down = inp.D(Key.Down);
            bool wasGround = p.OnGround;
            float oldVX = p.VX;
            Phys.Step(ref p, i, W);
            int ev = p.Events;

            // ---- animation & effects ----
            float fx0 = p.X + Phys.W / 2, fy0 = p.Y + Phys.H;
            if ((ev & Phys.EV_JUMP) != 0) { jumpT = 8; Puff(fx0, fy0, 3, 0.8f); Sfx.Play("jump"); }
            if ((ev & Phys.EV_DJUMP) != 0)
            {
                spinT = 18; jumpT = 6; Sfx.Play("djump");
                fx.Spawn(PT.Ring, fx0, fy0 - 2, 0, 0.5f, 14, G.Hex(0xffffff), 2, 0);
                for (int k = 0; k < 6; k++) fx.Spawn(PT.Dust, fx0 + Particles.R(-4, 4), fy0, Particles.R(-1.2f, 1.2f), Particles.R(0.3f, 1.2f), 18, G.Hex(0xe8e4f4), 1, 0);
            }
            if ((ev & Phys.EV_LAND) != 0)
            {
                float hard = Math.Min(1f, p.LandVY / 6f);
                landT = (int)(4 + hard * 6);
                if (p.GroundKind == Phys.GK_ICE) { for (int k = 0; k < 6; k++) fx.Spawn(PT.Ice, fx0 + Particles.R(-6, 6), fy0 - 1, Particles.R(-1.5f, 1.5f), Particles.R(-1.8f, -0.4f), 24, G.Hex(0xbff4ff), 1, 0.12f); }
                else Puff(fx0, fy0, (int)(2 + hard * 5), 1f + hard);
                if (hard > 0.3f) Sfx.Play("land");
                if (p.GroundKind == Phys.GK_DESK) { DeskPlat d = W.FindDesk(p.RideId); if (d != null) { d.Glow = 1f; for (int k = 0; k < 4; k++) fx.Spawn(PT.Glint, fx0 + Particles.R(-10, 10), fy0, 0, -0.2f, 18, DeskColor(d.Kind), 1, 0); } }
            }
            if ((ev & Phys.EV_SPRING) != 0)
            {
                if (p.SpringId >= 0 && p.SpringId < W.Springs.Count) W.Springs[p.SpringId].Anim = 12;
                jumpT = 10; Sfx.Play("spring");
                for (int k = 0; k < 8; k++) fx.Spawn(PT.Star, fx0 + Particles.R(-6, 6), fy0, Particles.R(-1.5f, 1.5f), Particles.R(-2.5f, -0.5f), 20, G.Hex(0xffd23a), 1, 0.1f);
            }
            if ((ev & Phys.EV_ORB) != 0 && W.LastOrb != null)
            {
                Sfx.Play("orb");
                for (int k = 0; k < 12; k++) { float a = k * 0.5236f; fx.Spawn(PT.Spark, W.LastOrb.X, W.LastOrb.Y, (float)Math.Cos(a) * 2, (float)Math.Sin(a) * 2, 20, G.Hex(0x6aff9a), 2, 0); }
                fx.Spawn(PT.Ring, W.LastOrb.X, W.LastOrb.Y, 0, 0, 14, G.Hex(0x6aff9a), 3, 0);
            }
            if ((ev & Phys.EV_PORTAL) != 0)
            {
                Sfx.Play("portal");
                for (int k = 0; k < 14; k++) fx.Spawn(PT.Star, p.X + 5 + Particles.R(-8, 8), p.Y + 7 + Particles.R(-8, 8), Particles.R(-1, 1), Particles.R(-1, 1), 22, G.Hex(0xc080ff), 1, 0);
            }
            // skid smoke / ice spray
            if (p.Skid && (t & 1) == 0)
            {
                float sx = fx0 + p.Facing * -2, sy = fy0 - 1;
                if (p.GroundKind == Phys.GK_ICE)
                {
                    fx.Spawn(PT.Ice, sx, sy, -p.Facing * Particles.R(0.5f, 2f), Particles.R(-1.5f, -0.3f), 26, (t & 2) == 0 ? G.Hex(0xffffff) : G.Hex(0x9ae6ff), 1, 0.1f);
                    if ((t & 3) == 0) fx.Spawn(PT.Glint, sx + Particles.R(-3, 3), sy - Particles.R(0, 4), 0, 0, 14, G.Hex(0x9ae6ff), 1, 0);
                }
                else fx.Spawn(PT.Dust, sx, sy - 1, -p.Facing * Particles.R(0.3f, 1.2f), Particles.R(-0.6f, -0.1f), 22, G.Hex(0xd8d0e4), 1, 0);
                if (stateT % 10 == 0) Sfx.Play("skid");
            }
            // decelerating hard (letting go at speed) also kicks up a bit of dust
            if (p.OnGround && i.Dir == 0 && Math.Abs(oldVX) > 1.4f && (t % 4) == 0)
            {
                if (p.GroundKind == Phys.GK_ICE) fx.Spawn(PT.Glint, fx0, fy0 - 1, 0, -0.2f, 12, G.Hex(0xd0f6ff), 1, 0);
                else fx.Spawn(PT.Dust, fx0 - Math.Sign(oldVX) * 3, fy0 - 1, -Math.Sign(oldVX) * 0.4f, -0.2f, 16, G.Hex(0xd8d0e4), 0, 0);
            }
            // ice running sparkles
            if (p.OnGround && p.GroundKind == Phys.GK_ICE && Math.Abs(p.VX) > 1.5f && (t % 6) == 0)
                fx.Spawn(PT.Glint, fx0 + Particles.R(-4, 4), fy0 - Particles.R(0, 3), 0, 0, 12, G.Hex(0xbff4ff), 1, 0);
            // footstep dust
            if (p.OnGround && p.GroundKind != Phys.GK_ICE && Math.Abs(p.VX) > 1.8f && (t % 10) == 0)
                fx.Spawn(PT.Dust, fx0 - p.Facing * 4, fy0 - 1, -p.Facing * 0.3f, -0.15f, 14, G.Hex(0xd8d0e4), 0, 0);
            // wind around the player
            if (p.UpWind && (t % 3) == 0) fx.Spawn(PT.Wind, p.X + Particles.R(-2, 12), p.Y + 16, 0, -3.2f, 10, G.Hex(0xe8f8ff), 5, 0);
            if (p.SideWind && (t % 3) == 0) fx.Spawn(PT.Wind, p.X + 5 + Particles.R(-10, 10), p.Y + Particles.R(0, 14), Math.Sign(p.VX + 0.01f) * 3.2f, 0, 10, G.Hex(0xe8f8ff), 5, 0);
            // rocket boots
            if (save.Look.Shoes == 5 && (Math.Abs(p.VX) > 0.5f || !p.OnGround) && (t & 1) == 0)
                fx.Spawn(PT.Flame, fx0 + Particles.R(-3, 3), fy0, Particles.R(-0.3f, 0.3f), Particles.R(0.5f, 1.2f), 12, 0, 1, 0);

            // run cycle
            if (p.OnGround) runPhase += Math.Abs(p.VX) * 0.11f;
            if (landT > 0) landT--; if (jumpT > 0) jumpT--; if (spinT > 0) spinT--;
            if (p.OnGround && Math.Abs(p.VX) < 0.2f) idleT++; else idleT = 0;
            if (--blinkT < 0) blinkT = 150 + Particles.Rng.Next(120);

            // desk glow decay
            foreach (DeskPlat d in W.Desk) if (d.Glow > 0) d.Glow -= 0.02f;

            // pickups
            float pcx = p.X + Phys.W / 2, pcy = p.Y + Phys.H / 2;
            foreach (Pickup pk in W.Pickups)
            {
                if (pk.Taken) continue;
                bool already = pk.Kind == 1 && ((save.Amber[Level] | runMask) & (1 << pk.Index)) != 0 && false;
                if (Math.Abs(pcx - pk.X) < 10 && Math.Abs(pcy - pk.Y) < 11 && !already)
                {
                    pk.Taken = true;
                    if (pk.Kind == 1)
                    {
                        runMask |= 1 << pk.Index; Sfx.Play("amber");
                        for (int k = 0; k < 16; k++) { float a = k * 0.3927f; fx.Spawn(PT.Star, pk.X, pk.Y, (float)Math.Cos(a) * 1.8f, (float)Math.Sin(a) * 1.8f, 26, G.Hex(0xffb030), 1, 0.02f); }
                        fx.Spawn(PT.Ring, pk.X, pk.Y, 0, 0, 16, G.Hex(0xffd070), 2, 0);
                    }
                    else
                    {
                        gems++; Sfx.Play("gem");
                        for (int k = 0; k < 6; k++) fx.Spawn(PT.Glint, pk.X + Particles.R(-5, 5), pk.Y + Particles.R(-5, 5), 0, -0.3f, 16, G.Hex(0x4ae0ff), 1, 0);
                    }
                }
            }
            // checkpoints
            foreach (Checkpoint k in W.Checkpoints)
            {
                if (k.On) continue;
                if (Math.Abs(pcx - k.X) < 10 && pcy > k.Y - 22 && pcy < k.Y + 2)
                {
                    foreach (Checkpoint o in W.Checkpoints) o.On = false;
                    k.On = true; respawnX = k.X - 5; respawnY = k.Y - Phys.H;
                    Sfx.Play("checkpoint");
                    for (int n = 0; n < 10; n++) fx.Spawn(PT.Star, k.X + 3, k.Y - 14, Particles.R(-1.5f, 1.5f), Particles.R(-2, 0), 24, G.Hex(0x6aff9a), 1, 0.08f);
                }
            }

            if (p.Dead)
            {
                state = GState.Dead; stateT = 0; deaths++;
                Sfx.Play("death");
                for (int k = 0; k < 10; k++) fx.Spawn(PT.Dust, pcx + Particles.R(-5, 5), pcy + Particles.R(-5, 5), Particles.R(-1.5f, 1.5f), Particles.R(-1.5f, 1), 30, G.Hex(0xe8e4f4), 2, 0);
                for (int k = 0; k < 8; k++) fx.Spawn(PT.Star, pcx, pcy, Particles.R(-2, 2), Particles.R(-3, 0), 30, G.Hex(0xffd23a), 1, 0.12f);
            }
            else if (p.Won)
            {
                state = GState.Win; stateT = 0; winX = pcx; winY = pcy;
                Sfx.Play("win");
            }
        }

        void Puff(float x, float y, int n, float spread)
        {
            for (int k = 0; k < n; k++)
                fx.Spawn(PT.Dust, x + Particles.R(-3, 3), y - 1, Particles.R(-1.2f, 1.2f) * spread, Particles.R(-0.5f, -0.1f), 18, G.Hex(0xe0d8ec), 1, 0);
        }

        void AmbientFx()
        {
            foreach (Wind w in W.Winds)
            {
                if ((t + w.TX * 3) % 4 != 0) continue;
                if (w.Dir == 0)
                    fx.Spawn(PT.Wind, w.X + Particles.R(1, w.W - 1), w.Y + w.H - Particles.R(0, 6), 0, -Particles.R(2.2f, 3.4f), (int)(w.H / 2.8f), G.Hex(0xe8f8ff), (int)Particles.R(3, 7), 0);
                else
                {
                    float d = w.Dir == 1 ? 1 : -1;
                    float sx = d > 0 ? w.X + Particles.R(0, 6) : w.X + w.W - Particles.R(0, 6);
                    fx.Spawn(PT.Wind, sx, w.Y + Particles.R(2, w.H - 2), d * Particles.R(2.2f, 3.4f), 0, (int)(w.W / 2.8f), G.Hex(0xe8f8ff), (int)Particles.R(3, 7), 0);
                }
            }
            // black hole drift stars
            if (t % 5 == 0)
            {
                float a = Particles.R(0, 6.283f), r = Particles.R(22, 34);
                fx.Spawn(PT.Star, W.ExitX + (float)Math.Cos(a) * r, W.ExitY + (float)Math.Sin(a) * r * 0.7f, 0, 0, 60, (t % 10 == 0) ? G.Hex(0xc080ff) : G.Hex(0xffa0e0), 0, 0);
            }
            fx.Attract(W.ExitX, W.ExitY, state == GState.Win ? 0.35f : 0.06f);
            // orb idle sparkles
            if (t % 12 == 0)
                foreach (Orb o in W.Orbs) if (o.Cool == 0) fx.Spawn(PT.Glint, o.X + Particles.R(-6, 6), o.Y + Particles.R(-7, 7), 0, -0.1f, 14, G.Hex(0x9affc0), 1, 0);
            if (t % 48 == 0)
                foreach (Pickup pk in W.Pickups) if (!pk.Taken && pk.Kind == 1) fx.Spawn(PT.Glint, pk.X + Particles.R(-4, 4), pk.Y + Particles.R(-5, 5), 0, 0, 14, G.Hex(0xfff0a0), 1, 0);
        }

        // ---------- win sequence: hold in the air, then spiral into the black hole ----------
        void WinTick()
        {
            float ex = W.ExitX, ey = W.ExitY;
            if (stateT < 50)
            {
                float u = stateT / 50f; u = u * u * (3 - 2 * u);
                float tx = ex, ty = ey - 20 + (float)Math.Sin(stateT * 0.2f) * 2;
                winX = winX + (tx - winX) * 0.12f; winY = winY + (ty - winY) * 0.12f;
                if (stateT % 9 == 0) fx.Spawn(PT.Heart, winX + Particles.R(-6, 6), winY - 8, 0, -0.6f, 34, G.Hex(0xff5a9a), 1, 0);
            }
            else if (stateT == 50) Sfx.Play("suck");
            if (stateT % 2 == 0 && stateT < 130)
            {
                float a = Particles.R(0, 6.283f), r = Particles.R(28, 46);
                fx.Spawn(PT.Star, ex + (float)Math.Cos(a) * r, ey + (float)Math.Sin(a) * r, 0, 0, 50, Particles.Rng.Next(2) == 0 ? G.Hex(0xffffff) : G.Hex(0xffb0f0), 1, 0);
            }
            if (stateT == 132)
            {
                Sfx.Play("clear");
                fx.Spawn(PT.Ring, ex, ey, 0, 0, 22, G.Hex(0xffffff), 6, 0);
                fx.Spawn(PT.Ring, ex, ey, 0, 0, 30, G.Hex(0xc080ff), 3, 0);
                for (int k = 0; k < 24; k++) { float a = k * 0.2618f; fx.Spawn(PT.Star, ex, ey, (float)Math.Cos(a) * 2.6f, (float)Math.Sin(a) * 2.6f, 34, k % 2 == 0 ? G.Hex(0xffd23a) : G.Hex(0xff8ad6), 1, 0.03f); }
            }
            if (stateT > 165) FinishLevel();
        }

        void FinishLevel()
        {
            state = GState.Clear; stateT = 0; menuSel = 0;
            int before = save.Best[Level];
            newBest = before == 0 || levelTime < before;
            if (newBest) save.Best[Level] = levelTime;
            save.Amber[Level] |= runMask;
            if (save.Unlocked < Level + 2) save.Unlocked = Math.Min(Levels.All.Length, Level + 2);
            AllCleared = true; for (int i = 0; i < save.Best.Length; i++) if (save.Best[i] == 0) AllCleared = false;
            save.Write();
        }

        void ClearInput(Input inp)
        {
            int n = Level < Levels.All.Length - 1 ? 3 : 2;
            if (inp.P(Key.Left)) { menuSel = (menuSel + n - 1) % n; Sfx.Play("click"); }
            if (inp.P(Key.Right)) { menuSel = (menuSel + 1) % n; Sfx.Play("click"); }
            if (inp.P(Key.Restart)) { Load(Level); return; }
            if (inp.P(Key.Back)) { Request = GameReq.Menu; return; }
            if (inp.P(Key.Confirm) || inp.P(Key.Jump)) ClearAction(n == 3 ? menuSel : menuSel + 1);
            if (clearClicked >= 0) { ClearAction(clearClicked); clearClicked = -1; }
        }
        int clearClicked = -1, pauseClicked = -1;

        void ClearAction(int a)
        {
            Sfx.Play("select");
            if (a == 0) Load(Level + 1);
            else if (a == 1) Load(Level);
            else Request = GameReq.Menu;
        }

        void PauseInput(Input inp)
        {
            if (inp.P(Key.Up)) { menuSel = (menuSel + 3) % 4; Sfx.Play("click"); }
            if (inp.P(Key.Down)) { menuSel = (menuSel + 1) % 4; Sfx.Play("click"); }
            if (inp.P(Key.Confirm) || inp.P(Key.Jump)) PauseAction(menuSel);
            if (pauseClicked >= 0) { PauseAction(pauseClicked); pauseClicked = -1; }
        }

        void PauseAction(int a)
        {
            Sfx.Play("select");
            if (a == 0) state = prevState;
            else if (a == 1) Load(Level);
            else if (a == 2) Request = GameReq.Menu;
            else Request = GameReq.Quit;
        }

        public string DebugState { get { return state + " t=" + stateT; } }
        public void DebugTeleport(float x, float y) { p.X = x; p.Y = y; p.VX = p.VY = 0; state = GState.Play; }

        public void ForcePause()
        {
            if (state != GState.Paused && state != GState.Clear) { prevState = state; state = GState.Paused; stateT = 0; menuSel = 0; }
        }

        // =====================================================================
        //                              RENDER
        // =====================================================================
        public void Render(Input inp)
        {
            Surface s = Fb;
            G.Copy(stat, s);
            DrawDesk(s);
            Tiles.DrawDynamic(s, W, t);
            DrawEntities(s);
            DrawHoleBack(s);
            DrawPlayer(s);
            DrawHoleFront(s);
            fx.Draw(s, t);
            DrawHud(s);
            if (state == GState.Paused) DrawPause(s, inp);
            if (state == GState.Clear) DrawClear(s, inp);
            if (inp != null) inp.Click = false;
        }

        static int DeskColor(int kind)
        {
            return kind == 0 ? G.Hex(0x3ee0c8) : kind == 1 ? G.Hex(0xffd23a) : G.Hex(0xff5ab8);
        }

        void DrawDesk(Surface s)
        {
            if (!save.DeskOn || !save.ShowEdges) return;
            float pcx = p.X + 5, pcy = p.Y + 7;
            foreach (DeskPlat d in W.Desk)
            {
                int y = (int)Math.Round(d.Y);
                if (y < 0 || y >= s.H) continue;
                int x1 = (int)Math.Round(d.X1), x2 = (int)Math.Round(d.X2) - 1;
                int c = DeskColor(d.Kind);
                float dx = pcx < d.X1 ? d.X1 - pcx : (pcx > d.X2 ? pcx - d.X2 : 0);
                float dist = Math.Max(dx, Math.Abs(pcy - d.Y));
                bool near = dist < 70 || d.Glow > 0;
                if (near)
                {
                    G.HLine(s, x1, x2, y - 1, c);
                    G.HLine(s, x1, x2, y, G.Hex(0x120c1c));
                    // shimmer
                    int sh = (t * 3) % Math.Max(1, (x2 - x1 + 40));
                    for (int k = 0; k < 6; k++) G.Px(s, x1 + sh - k, y - 1, G.Hex(0xffffff));
                    if (d.Glow > 0.3f) for (int x = x1; x <= x2; x++) if (G.Dith(x, y - 2, (int)(d.Glow * 10))) G.Px(s, x, y - 2, c);
                    G.Px(s, x1, y - 2, c); G.Px(s, x2, y - 2, c);
                }
                else if (d.Kind == 0 || dist < 200)
                {
                    for (int x = x1; x <= x2; x += 4) { G.Px(s, x, y - 1, c); G.Px(s, x + 1, y - 1, c); }
                }
                else
                {
                    G.Px(s, x1, y - 1, c); G.Px(s, x1 + 1, y - 1, c); G.Px(s, x2, y - 1, c); G.Px(s, x2 - 1, y - 1, c);
                }
            }
        }

        void DrawEntities(Surface s)
        {
            // orbs
            foreach (Orb o in W.Orbs)
            {
                int ox = (int)o.X, oy = (int)o.Y + (int)Math.Round(Math.Sin(t * 0.08f + o.X) * 1.5f);
                if (o.Cool > 0)
                {
                    int c = G.Hex(0x3a6a4a);
                    G.Px(s, ox, oy - 5, c); G.Px(s, ox, oy + 5, c); G.Px(s, ox - 4, oy, c); G.Px(s, ox + 4, oy, c);
                    G.Px(s, ox - 2, oy - 3, c); G.Px(s, ox + 2, oy - 3, c); G.Px(s, ox - 2, oy + 3, c); G.Px(s, ox + 2, oy + 3, c);
                    continue;
                }
                int glow = 6 + (int)(Math.Sin(t * 0.15f) * 3);
                G.DiscD(s, ox, oy, 8, G.Hex(0x2aff7a), glow);
                OrbGem(s, ox, oy);
            }
            // portals
            foreach (Portal pt in W.Portals)
            {
                PortalDraw(s, (int)pt.AX, (int)pt.AY, pt.Color, 0);
                PortalDraw(s, (int)pt.BX, (int)pt.BY, pt.Color, 3);
            }
            // checkpoints
            foreach (Checkpoint k in W.Checkpoints)
            {
                int x = (int)k.X, y = (int)k.Y;
                G.Rect(s, x - 3, y - 2, 7, 2, G.Hex(0x3a3f4c)); G.RectO(s, x - 4, y - 3, 9, 4, G.Hex(0x120c1c));
                G.VLine(s, x, y - 18, y - 3, G.Hex(0xd8d8e8)); G.VLine(s, x + 1, y - 18, y - 3, G.Hex(0x120c1c));
                G.Disc(s, x, y - 19, 1, k.On ? G.Hex(0xffd23a) : G.Hex(0x8a8aa0));
                int fc = k.On ? G.Hex(0x4aff8a) : G.Hex(0x6a6a84), fd = k.On ? G.Hex(0x1aa04a) : G.Hex(0x40405a);
                int wave = k.On ? ((t / 8) & 1) : 0;
                for (int j = 0; j < 6; j++)
                {
                    int len = 8 - Math.Abs(j - 2) * 2 - (j > 2 ? 1 : 0);
                    for (int i = 1; i <= len; i++)
                    {
                        int yy = y - 17 + j + ((i > 3 && wave == 1) ? 1 : 0);
                        G.Px(s, x + 1 + i, yy, i == len ? G.Hex(0x120c1c) : (j == 0 ? G.Hex(0x120c1c) : (j >= 4 ? fd : fc)));
                    }
                }
                if (k.On) { G.Px(s, x + 4, y - 15 + wave, G.Hex(0xffffff)); }
            }
            // pickups
            foreach (Pickup pk in W.Pickups)
            {
                if (pk.Taken) continue;
                int bob = (int)Math.Round(Math.Sin(t * 0.07f + pk.X * 0.1f) * 2);
                if (pk.Kind == 1)
                {
                    bool had = (save.Amber[Level] & (1 << pk.Index)) != 0;
                    // slow, steady "breathing" glow (about 4 s per cycle) instead of a fast blink
                    float breath = (float)(Math.Sin(t * 0.026f + pk.X * 0.05f) * 0.5 + 0.5);
                    G.DiscD(s, (int)pk.X, (int)pk.Y + bob, 6, G.Hex(0xffb030), 3 + (int)Math.Round(breath * 4)); // original glow, just slow
                    Ui.Amber(s, (int)pk.X - 2, (int)pk.Y - 3 + bob, true);
                }
                else Ui.Gem(s, (int)pk.X - 2, (int)pk.Y - 2 + bob);
            }
        }

        static Sprite orbSprite;
        static void OrbGem(Surface s, int x, int y)
        {
            if (orbSprite == null)
            {
                string[] a = { "....k....", "...kgk...", "..kgwgk..", ".kgwggdk.", "kgggggddk", ".kgggddk.", "..kgddk..", "...kdk...", "....k...." };
                orbSprite = G.Art(a, delegate (char c) { return c == 'k' ? G.Hex(0x0a3a1a) : c == 'g' ? G.Hex(0x4aff8a) : c == 'w' ? G.Hex(0xe8fff0) : G.Hex(0x1aa04a); });
            }
            G.Blit(s, orbSprite, x - 4, y - 4, false);
        }

        void PortalDraw(Surface s, int cx, int cy, int pair, int off)
        {
            int c1 = pair == 0 ? G.Hex(0xb060ff) : G.Hex(0xff9a30), c2 = pair == 0 ? G.Hex(0x40e0ff) : G.Hex(0xffe070);
            for (int y = -9; y <= 9; y++)
                for (int x = -6; x <= 6; x++)
                {
                    float d = (x * x) / 36f + (y * y) / 81f;
                    if (d > 1) continue;
                    float ang = (float)Math.Atan2(y, x) + d * 4 - t * 0.12f;
                    int lvl = (int)((Math.Sin(ang * 3) * 0.5 + 0.5) * 10);
                    G.Px(s, cx + x, cy + y, G.Dith(x + cx, y + cy, lvl) ? G.Mul(c1, 0.55f) : G.Hex(0x0c0618));
                }
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * 6.283f + t * 0.06f * (off == 0 ? 1 : -1);
                int x = cx + (int)Math.Round(Math.Cos(a) * 6.5f), y = cy + (int)Math.Round(Math.Sin(a) * 9.5f);
                G.Px(s, x, y, ((i + t / 4) % 4) < 2 ? c1 : c2);
                G.Px(s, x + (x > cx ? 1 : -1), y, G.Hex(0x120c1c));
            }
        }

        // ---------- black hole ----------
        static readonly int[] DiskCols = { 0xffffff, 0xfff0b0, 0xffc060, 0xff8a40, 0xff5a8a, 0xc050e0, 0x7a3ac8 };

        float HoleScale()
        {
            if (state == GState.Win) { if (stateT < 132) return 1f + Math.Min(1f, stateT / 60f) * 0.35f; return Math.Max(1f, 1.35f + (stateT - 132) * 0.02f - (stateT - 132) * 0.04f); }
            if (state == GState.Clear) return 1f + (float)Math.Sin(t * 0.05f) * 0.05f;
            return 1f + (float)Math.Sin(t * 0.05f) * 0.05f;
        }

        void Disk(Surface s, bool front)
        {
            float ex = W.ExitX, ey = W.ExitY, sc = HoleScale();
            float spin = state == GState.Win ? 0.05f + stateT * 0.0025f : 0.05f;
            float ph = t * spin;
            if (state == GState.Win) ph = t * 0.05f + stateT * stateT * 0.0012f;
            for (int ring = 0; ring < 7; ring++)
            {
                float r = (9 + ring * 1.5f) * sc;
                int n = 18 + ring * 4;
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * 6.283f + ph * (1.6f - ring * 0.15f) + ring;
                    float sy = (float)Math.Sin(a);
                    if ((sy > 0) != front) continue;
                    if (((i + ring) % 5) == 0) continue;
                    int x = (int)Math.Round(ex + Math.Cos(a) * r * 1.55f), y = (int)Math.Round(ey + sy * r * 0.42f);
                    G.Px(s, x, y, G.Hex(DiskCols[ring]));
                    if (ring < 3) G.Px(s, x + 1, y, G.Hex(DiskCols[ring + 1]));
                }
            }
        }

        void DrawHoleBack(Surface s)
        {
            float ex = W.ExitX, ey = W.ExitY, sc = HoleScale();
            int r = (int)(8 * sc);
            G.DiscD(s, (int)ex, (int)ey, r + 7, G.Hex(0x7a3ac8), 3);
            G.DiscD(s, (int)ex, (int)ey, r + 4, G.Hex(0xc050e0), 4);
            Disk(s, false);
            // photon ring + event horizon
            G.Disc(s, (int)ex, (int)ey, r + 1, G.Hex(0xffe8c0));
            G.Disc(s, (int)ex, (int)ey, r, G.Hex(0x05030a));
            G.Ring(s, (int)ex, (int)ey, r - 1, G.Hex(0x1a0a2a));
            // cute shine
            G.Px(s, (int)ex - r / 2, (int)ey - r / 2, G.Hex(0x3a2a5a));
        }

        void DrawHoleFront(Surface s)
        {
            Disk(s, true);
            // little "EXIT" sign the first few seconds
            if (introT < 360 && state == GState.Play && ((t / 20) & 1) == 0)
                Font.CenteredO(s, "EXIT", (int)W.ExitX, (int)W.ExitY - 28, G.Hex(0xffd23a), G.Hex(0x120c1c), 1);
        }

        // ---------- player ----------
        static Sprite egg; static int eggChar = -1;
        void DrawPlayer(Surface s)
        {
            if (state == GState.Dead) return;
            float bx = p.X + Phys.W / 2, by = p.Y + Phys.H;
            if (state == GState.Hatch)
            {
                DrawEgg(s, bx, by, stateT);
                return;
            }
            Pose pose; float sx = 1, sy = 1, ang = 0;
            int extra = ((t / 4) & 1);
            if (state == GState.Win || state == GState.Clear)
            {
                if (state == GState.Clear) return;
                pose = Pose.Happy;
                float px = winX, py = winY;
                if (stateT >= 50)
                {
                    float u = Math.Min(1f, (stateT - 50) / 80f);
                    float r = 20 * (float)Math.Pow(1 - u, 1.3);
                    float a = -1.5708f + u * u * 16f;
                    px = W.ExitX + (float)Math.Cos(a) * r * 1.3f;
                    py = W.ExitY + (float)Math.Sin(a) * r;
                    ang = u * u * 30f;
                    sx = sy = Math.Max(0, 1 - u);
                    sy *= 1 + u * 0.4f;
                    if (u >= 1) return;
                }
                else
                {
                    ang = (float)Math.Sin(stateT * 0.25f) * 0.25f;
                }
                int ax0, ay0; Sprite sp0 = frames.Get(pose, extra, out ax0, out ay0);
                bool fl0 = p.Facing < 0;
                float axf = fl0 ? sp0.W - 1 - ax0 + 0.5f : ax0 + 0.5f;
                G.BlitX(s, sp0, px, py, axf, sp0.H / 2f, sx, sy, ang, fl0);
                return;
            }
            if (p.OnGround)
            {
                if (p.Skid) pose = Pose.Skid;
                else if (Math.Abs(p.VX) > 0.25f || (p.GroundKind == Phys.GK_CONVL || p.GroundKind == Phys.GK_CONVR))
                {
                    int f = ((int)runPhase) & 3;
                    pose = f == 0 ? Pose.Run0 : f == 1 ? Pose.Run1 : f == 2 ? Pose.Run2 : Pose.Run3;
                    if (Math.Abs(p.VX) < 0.25f) pose = Pose.Idle0;
                }
                else pose = blinkT < 8 ? Pose.Blink : (((t / 30) & 1) == 0 ? Pose.Idle0 : Pose.Idle1);
            }
            else pose = p.VY < 0 ? Pose.Jump : Pose.Fall;

            if (landT > 0) { float k = landT / 10f; sx = 1 + 0.25f * k; sy = 1 - 0.22f * k; }
            if (jumpT > 0) { float k = jumpT / 10f; sx = 1 - 0.15f * k; sy = 1 + 0.18f * k; }
            if (spinT > 0) ang = p.Facing * (1 - spinT / 18f) * 6.283f;
            if (p.UpWind) ang = (float)Math.Sin(t * 0.3f) * 0.12f;

            int ax, ay; Sprite sp = frames.Get(pose, extra, out ax, out ay);
            bool flip = p.Facing < 0;
            int axi = flip ? sp.W - 1 - ax : ax;
            if (sx == 1 && sy == 1 && ang == 0)
                G.Blit(s, sp, (int)Math.Round(bx) - axi, (int)Math.Round(by) - ay - 1, flip);
            else if (spinT > 0)
                G.BlitX(s, sp, bx, by - sp.H / 2f, axi + 0.5f, sp.H / 2f, sx, sy, ang, flip);
            else
                G.BlitX(s, sp, bx, by, axi + 0.5f, ay + 1, sx, sy, ang, flip);
        }

        void DrawEgg(Surface s, float bx, float by, int st)
        {
            if (egg == null || eggChar != save.Look.Char)
            {
                eggChar = save.Look.Char;
                int spot = Art.Dinos[eggChar].Pal['b'];
                string[] a = { "...kkkk...", "..kwwwwk..", ".kwwwswwk.", ".kwswwwwk.", "kwwwwwswwk", "kwswwwwwwk", "kwwwwswwwk", "kwwwwwwwsk", ".kwwswwwk.", "..kkkkkk.." };
                egg = G.Art(a, delegate (char c) { return c == 'k' ? G.Hex(0x120c1c) : c == 'w' ? G.Hex(0xfff4dc) : spot; });
            }
            float wob = st < 20 ? (float)Math.Sin(st * 0.9f) * 0.25f * (st / 20f) : 0;
            float pop = st < 4 ? st / 4f : 1;
            G.BlitX(s, egg, bx, by, 5, 10, pop, pop, wob, false);
            if (st > 14)
            {
                int cx = (int)bx, cy = (int)by - 6;
                G.Px(s, cx - 2, cy, G.Hex(0x120c1c)); G.Px(s, cx - 1, cy + 1, G.Hex(0x120c1c)); G.Px(s, cx, cy, G.Hex(0x120c1c)); G.Px(s, cx + 1, cy + 1, G.Hex(0x120c1c)); G.Px(s, cx + 2, cy, G.Hex(0x120c1c));
            }
        }

        // ---------- HUD ----------
        void DrawHud(Surface s)
        {
            int cx = s.W / 2;
            string name = "LV" + (Level + 1).ToString("00") + " " + W.Def.Name;
            string time = SaveData.Time(levelTime);
            int wName = Font.Measure(name, 1), wTime = Font.Measure(time, 1);
            int w = wName + wTime + 3 * 8 + 46 + 30;
            int x = cx - w / 2, y = 3;
            G.Rect(s, x + 2, y + 2, w, 13, G.Hex(0x120c1c));
            G.Rect(s, x, y, w, 13, G.Hex(0x17122a));
            G.RectO(s, x, y, w, 13, G.Hex(0x3ee0c8));
            G.RectO(s, x - 1, y - 1, w + 2, 15, G.Hex(0x120c1c));
            int tx = x + 5;
            Font.Text(s, name, tx, y + 3, G.Hex(0xf4ecff), 1); tx += wName + 8;
            Font.Text(s, time, tx, y + 3, G.Hex(0xffd23a), 1); tx += wTime + 8;
            for (int i = 0; i < 3; i++)
            {
                bool got = ((runMask | save.Amber[Level]) & (1 << i)) != 0;
                bool now = (runMask & (1 << i)) != 0;
                if (i < W.AmberCount) Ui.Amber(s, tx + i * 8, y + 3, got && (now || ((t / 30) & 1) == 0 || true) && got);
            }
            tx += 26;
            Ui.Gem(s, tx, y + 4); tx += 8;
            Font.Text(s, gems + "/" + W.GemCount, tx, y + 3, G.Hex(0x4ae0ff), 1);
            Font.CenteredO(s, "ESC PAUSE  R RESTART", cx, y + 17, G.Hex(0x8a80a8), G.Hex(0x120c1c), 1);

            // intro banner
            if (introT < 200)
            {
                int slide = introT < 15 ? (15 - introT) * 4 : (introT > 180 ? (introT - 180) * 4 : 0);
                int by = 40 - slide;
                string big = W.Def.Name;
                int bw = Math.Max(Font.Measure(big, 2), Font.Measure(W.Def.Tip, 1)) + 24;
                Ui.Box(s, cx - bw / 2, by, bw, 42, G.Hex(0xff4ab8));
                Font.Centered(s, "LEVEL " + (Level + 1) + " - " + Levels.WorldNames[W.Theme], cx, by + 5, G.Hex(0x3ee0c8), 1);
                Font.Centered(s, big, cx + 1, by + 16, G.Hex(0x120c1c), 2);
                Font.Centered(s, big, cx, by + 15, G.Hex(0xffffff), 2);
                Font.Centered(s, W.Def.Tip, cx, by + 31, G.Hex(0xffd23a), 1);
            }
            if (deaths > 0 && state == GState.Dead)
                Font.CenteredO(s, "OOF!", (int)(p.X + 5), (int)p.Y - 12 - stateT / 3, G.Hex(0xffd23a), G.Hex(0x120c1c), 1);
        }

        void DrawPause(Surface s, Input inp)
        {
            int msgW = Font.Measure("KEYBOARD IS FREE WHILE PAUSED", 1);
            int w = Math.Max(150, msgW + 24), h = 108, x = s.W / 2 - w / 2, y = s.H / 2 - h / 2 - 20;
            Ui.Box(s, x, y, w, h, G.Hex(0x3ee0c8));
            Font.Centered(s, "PAUSED", s.W / 2 + 1, y + 8, G.Hex(0x120c1c), 2);
            Font.Centered(s, "PAUSED", s.W / 2, y + 7, G.Hex(0xffffff), 2);
            string[] labels = { "RESUME", "RESTART LEVEL", "MAIN MENU", "QUIT GAME" };
            for (int i = 0; i < 4; i++)
                if (Ui.Button(s, inp, labels[i], s.W / 2 - 50, y + 27 + i * 16, 100, menuSel == i, t)) pauseClicked = i;
            Font.Centered(s, "KEYBOARD IS FREE WHILE PAUSED", s.W / 2, y + h - 11, G.Hex(0x8a80a8), 1);
        }

        void DrawClear(Surface s, Input inp)
        {
            int w = 230, h = 110, x = s.W / 2 - w / 2, y = s.H / 2 - h / 2 - 30;
            Ui.Box(s, x, y, w, h, G.Hex(0xffd23a));
            string title = (Level == Levels.All.Length - 1) ? "YOU BEAT DESKOSAUR!" : "LEVEL CLEAR!";
            int[] rb = { 0xff4060, 0xff9a30, 0xffe040, 0x50d860, 0x40a0ff, 0xa060f0 };
            int tw = Font.Measure(title, 2), tx = s.W / 2 - tw / 2;
            for (int i = 0; i < title.Length; i++)
            {
                string ch = title[i].ToString();
                int bob = (int)Math.Round(Math.Sin(t * 0.15f + i * 0.6f) * 1.5f);
                Font.Text(s, ch, tx + 1, y + 9 + bob, G.Hex(0x120c1c), 2);
                Font.Text(s, ch, tx, y + 8 + bob, G.Hex(rb[(i + t / 6) % 6]), 2);
                tx += Font.Measure(ch, 2) + 2;
                if (title[i] == ' ') tx += 4;
            }
            Font.Text(s, "TIME", x + 20, y + 32, G.Hex(0x8a80a8), 1);
            Font.Text(s, SaveData.Time(levelTime), x + 20, y + 42, G.Hex(0xffffff), 1);
            if (newBest && (t / 15) % 2 == 0) Font.Text(s, "NEW BEST!", x + 20, y + 52, G.Hex(0xff4ab8), 1);
            else Font.Text(s, "BEST " + SaveData.Time(save.Best[Level]), x + 20, y + 52, G.Hex(0x8a80a8), 1);
            Font.Text(s, "AMBER", x + 100, y + 32, G.Hex(0x8a80a8), 1);
            for (int i = 0; i < W.AmberCount; i++) Ui.Amber(s, x + 100 + i * 9, y + 42, (save.Amber[Level] & (1 << i)) != 0);
            Font.Text(s, "GEMS", x + 160, y + 32, G.Hex(0x8a80a8), 1);
            Font.Text(s, gems + "/" + W.GemCount, x + 160, y + 42, G.Hex(0x4ae0ff), 1);
            Font.Text(s, "OOFS " + deaths, x + 160, y + 52, G.Hex(0x8a80a8), 1);
            bool last = Level >= Levels.All.Length - 1;
            if (!last)
            {
                if (Ui.Button(s, inp, "NEXT", x + 18, y + 72, 58, menuSel == 0, t)) clearClicked = 0;
                if (Ui.Button(s, inp, "RETRY", x + 86, y + 72, 58, menuSel == 1, t)) clearClicked = 1;
                if (Ui.Button(s, inp, "MENU", x + 154, y + 72, 58, menuSel == 2, t)) clearClicked = 2;
            }
            else
            {
                if (Ui.Button(s, inp, "RETRY", x + 40, y + 72, 66, menuSel == 0, t)) clearClicked = 1;
                if (Ui.Button(s, inp, "MENU", x + 124, y + 72, 66, menuSel == 1, t)) clearClicked = 2;
            }
            Font.Centered(s, "ENTER CONFIRM  R RETRY  ESC MENU", s.W / 2, y + h - 12, G.Hex(0x8a80a8), 1);
        }
    }
}
