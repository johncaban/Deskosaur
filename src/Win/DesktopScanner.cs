using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Deskosaur
{
    // Finds jumpable edges on the real desktop:
    //   * top edges of visible windows (with z-order occlusion)
    //   * desktop icons (files/shortcuts)
    //   * UI elements inside the top windows (text boxes, buttons, tabs, links, Explorer files...) via UI Automation
    sealed class DesktopScanner : IDisposable
    {
        struct Win { public IntPtr H; public Native.RECT R; }
        struct RectI { public int L, T, R, B; public RectI(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; } }
        sealed class Elems { public List<RectI> Rel = new List<RectI>(); public int W, H; public DateTime At; }

        readonly uint myPid;
        List<Win> wins = new List<Win>();
        Dictionary<IntPtr, Native.RECT> lastRects = new Dictionary<IntPtr, Native.RECT>();
        readonly object sync = new object();
        Dictionary<IntPtr, Elems> elems = new Dictionary<IntPtr, Elems>();
        List<RectI> icons = new List<RectI>();
        List<Win> uiaTargets = new List<Win>();
        Thread uiaThread; volatile bool run; volatile bool active;
        Uia uia;
        Native.EnumWindowsProc enumCb;
        List<Win> enumTmp = new List<Win>();

        public DesktopScanner()
        {
            myPid = (uint)Process.GetCurrentProcess().Id;
            enumCb = EnumCb;
        }

        public void Start()
        {
            active = true;
            if (uiaThread != null) return;
            run = true;
            uiaThread = new Thread(UiaLoop);
            uiaThread.IsBackground = true; uiaThread.Name = "uia"; uiaThread.Priority = ThreadPriority.BelowNormal;
            try { uiaThread.SetApartmentState(ApartmentState.MTA); } catch { }
            uiaThread.Start();
        }
        public void Pause() { active = false; }

        static readonly string[] SkipClass = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland" };

        bool EnumCb(IntPtr h, IntPtr lp)
        {
            try
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return true;
                uint pid; Native.GetWindowThreadProcessId(h, out pid);
                if (pid == myPid) return true;
                int ex = Native.GetExStyle(h);
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) return true;
                if ((ex & Native.WS_EX_TRANSPARENT) != 0 && (ex & Native.WS_EX_LAYERED) != 0) return true; // other click-through overlays
                int cloaked = 0;
                try { Native.DwmGetWindowAttribute(h, 14, out cloaked, 4); } catch { }
                if (cloaked != 0) return true;
                if (Native.GetWindowTextLength(h) == 0) return true;
                string cls = Native.ClassOf(h);
                foreach (string s in SkipClass) if (cls == s) return true;
                Native.RECT r;
                bool okR = false;
                try { okR = Native.DwmGetWindowAttribute(h, 9, out r, 16) == 0; } catch { r = new Native.RECT(); }
                if (!okR && !Native.GetWindowRect(h, out r)) return true;
                if (r.Right - r.Left < 60 || r.Bottom - r.Top < 30) return true;
                Win w = new Win(); w.H = h; w.R = r;
                enumTmp.Add(w);
            }
            catch { }
            return true;
        }

        // Called on the UI thread a few times per second. Returns platforms in game (low-res) coordinates.
        public List<DeskPlat> Scan(int screenX, int screenY, int screenW, int screenH, int scale, int floorScreenY)
        {
            enumTmp.Clear();
            try { Native.EnumWindows(enumCb, IntPtr.Zero); } catch { }
            wins = new List<Win>(enumTmp);
            lock (sync) { uiaTargets = wins.GetRange(0, Math.Min(4, wins.Count)); }

            List<DeskPlat> outp = new List<DeskPlat>();
            Dictionary<IntPtr, Native.RECT> now = new Dictionary<IntPtr, Native.RECT>();
            List<int[]> segs = new List<int[]>();
            for (int i = 0; i < wins.Count; i++)
            {
                Win w = wins[i];
                now[w.H] = w.R;
                float dx = 0, dy = 0;
                Native.RECT old;
                if (lastRects.TryGetValue(w.H, out old))
                {
                    if (old.Right - old.Left == w.R.Right - w.R.Left && old.Bottom - old.Top == w.R.Bottom - w.R.Top)
                    { dx = (w.R.Left - old.Left) / (float)scale; dy = (w.R.Top - old.Top) / (float)scale; }
                }
                int hid = unchecked((int)w.H.ToInt64());
                // window top edge
                AddSegs(outp, segs, w.R.Left, w.R.Right, w.R.Top, i, hid * 31, 0, dx, dy, screenX, screenY, screenW, scale, floorScreenY);
                // elements inside the window
                Elems e = null;
                lock (sync) { elems.TryGetValue(w.H, out e); }
                if (e != null && e.W == w.R.Right - w.R.Left && e.H == w.R.Bottom - w.R.Top)
                {
                    for (int k = 0; k < e.Rel.Count; k++)
                    {
                        RectI rr = e.Rel[k];
                        int l = w.R.Left + rr.L, rgt = w.R.Left + rr.R, top = w.R.Top + rr.T;
                        if (top < w.R.Top + 6 || top > w.R.Bottom - 6) continue;
                        l = Math.Max(l, w.R.Left); rgt = Math.Min(rgt, w.R.Right);
                        int id = unchecked(hid * 7919 + rr.L * 131 + rr.T * 17 + rr.R);
                        AddSegs(outp, segs, l, rgt, top, i, id, 2, dx, dy, screenX, screenY, screenW, scale, floorScreenY);
                    }
                }
            }
            lastRects = now;
            // desktop icons (occluded by every window)
            List<RectI> ic;
            lock (sync) { ic = new List<RectI>(icons); }
            for (int k = 0; k < ic.Count; k++)
                AddSegs(outp, segs, ic[k].L + 4, ic[k].R - 4, ic[k].T, wins.Count, 900000 + k, 1, 0, 0, screenX, screenY, screenW, scale, floorScreenY);
            return outp;
        }

        void AddSegs(List<DeskPlat> outp, List<int[]> segs, int l, int r, int y, int zIndex, int id, int kind, float dx, float dy,
                     int sx, int sy, int sw, int scale, int floorY)
        {
            if (y < sy + 2 || y >= floorY - 2) return;
            if (l < sx) l = sx; if (r > sx + sw) r = sx + sw;
            if (r - l < 10) return;
            segs.Clear(); segs.Add(new int[] { l, r });
            for (int j = 0; j < zIndex && j < wins.Count && segs.Count > 0; j++)
            {
                Native.RECT o = wins[j].R;
                if (y < o.Top || y >= o.Bottom) continue;
                for (int s = segs.Count - 1; s >= 0; s--)
                {
                    int a = segs[s][0], b = segs[s][1];
                    if (o.Right <= a || o.Left >= b) continue;
                    segs.RemoveAt(s);
                    if (o.Left > a) segs.Add(new int[] { a, o.Left });
                    if (o.Right < b) segs.Add(new int[] { o.Right, b });
                }
            }
            for (int s = 0; s < segs.Count; s++)
            {
                float x1 = (segs[s][0] - sx) / (float)scale, x2 = (segs[s][1] - sx) / (float)scale;
                if (x2 - x1 < 6) continue;
                DeskPlat d = new DeskPlat();
                d.X1 = x1; d.X2 = x2; d.Y = (y - sy) / (float)scale; d.Kind = kind; d.DX = dx; d.DY = dy;
                d.Id = unchecked(id * 13 + segs[s][0] / 64);
                if (kind == 0) d.Id = unchecked(id * 13 + s);
                outp.Add(d);
            }
        }

        // ---------------- UI Automation worker ----------------
        void UiaLoop()
        {
            uia = new Uia();
            bool ok = uia.Init();
            DateTime lastIcons = DateTime.MinValue;
            while (run)
            {
                Thread.Sleep(350);
                if (!active || !ok) continue;
                try
                {
                    if ((DateTime.Now - lastIcons).TotalSeconds > 3)
                    {
                        lastIcons = DateTime.Now;
                        List<RectI> found = DesktopIcons();
                        lock (sync) { icons = found; }
                    }
                    List<Win> targets;
                    lock (sync) { targets = new List<Win>(uiaTargets); }
                    foreach (Win w in targets)
                    {
                        if (!run || !active) break;
                        Elems prev; bool fresh = false;
                        lock (sync) { fresh = elems.TryGetValue(w.H, out prev) && (DateTime.Now - prev.At).TotalSeconds < 1.6 && prev.W == w.R.Right - w.R.Left && prev.H == w.R.Bottom - w.R.Top; }
                        if (fresh) continue;
                        Native.RECT before; Native.GetWindowRect(w.H, out before);
                        List<double[]> rs = uia.Find(w.H, false, 90);
                        Native.RECT cur;
                        try { if (Native.DwmGetWindowAttribute(w.H, 9, out cur, 16) != 0) Native.GetWindowRect(w.H, out cur); } catch { Native.GetWindowRect(w.H, out cur); }
                        Native.RECT after; Native.GetWindowRect(w.H, out after);
                        if (before.Left != after.Left || before.Top != after.Top) continue; // moved while scanning: try again later
                        Elems e = new Elems(); e.At = DateTime.Now; e.W = cur.Right - cur.Left; e.H = cur.Bottom - cur.Top;
                        foreach (double[] r in rs)
                        {
                            int l = (int)r[0] - cur.Left, t = (int)r[1] - cur.Top, rr = (int)(r[0] + r[2]) - cur.Left, b = (int)(r[1] + r[3]) - cur.Top;
                            if (r[2] < 28 || r[3] < 10) continue;
                            if (t < 8) continue;
                            e.Rel.Add(new RectI(l, t, rr, b));
                        }
                        lock (sync) { elems[w.H] = e; }
                    }
                    // forget windows that went away
                    lock (sync)
                    {
                        List<IntPtr> dead = new List<IntPtr>();
                        foreach (KeyValuePair<IntPtr, Elems> kv in elems) if ((DateTime.Now - kv.Value.At).TotalSeconds > 20) dead.Add(kv.Key);
                        foreach (IntPtr d in dead) elems.Remove(d);
                    }
                }
                catch { }
            }
        }

        List<RectI> DesktopIcons()
        {
            List<RectI> res = new List<RectI>();
            IntPtr lv = FindDesktopListView();
            if (lv == IntPtr.Zero || !Native.IsWindowVisible(lv)) return res;
            foreach (double[] r in uia.Find(lv, true, 200))
                if (r[2] > 20 && r[3] > 20) res.Add(new RectI((int)r[0], (int)r[1], (int)(r[0] + r[2]), (int)(r[1] + r[3])));
            return res;
        }

        static IntPtr FindDesktopListView()
        {
            IntPtr found = IntPtr.Zero;
            IntPtr progman = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            IntPtr view = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view == IntPtr.Zero)
            {
                IntPtr w = IntPtr.Zero;
                while ((w = Native.FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
                {
                    view = Native.FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (view != IntPtr.Zero) break;
                }
            }
            if (view != IntPtr.Zero) found = Native.FindWindowEx(view, IntPtr.Zero, "SysListView32", null);
            return found;
        }

        public void Dispose() { run = false; }

        // UI Automation accessed through reflection so the exe needs no extra references
        sealed class Uia
        {
            Type tAE, tCache;
            MethodInfo fromHandle, findAll, cacheAdd, cacheActivate;
            PropertyInfo pCached;
            object scopeDesc, scopeChildren, condInteresting, condListItem, propBounds, propOffscreen;
            PropertyInfo piBounds, piOff, rX, rY, rW, rH, rEmpty;

            public bool Init()
            {
                try
                {
                    Assembly c = Assembly.Load("UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    Assembly ty = Assembly.Load("UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                    tAE = c.GetType("System.Windows.Automation.AutomationElement", true);
                    Type tCond = c.GetType("System.Windows.Automation.Condition", true);
                    Type tProp = c.GetType("System.Windows.Automation.PropertyCondition", true);
                    Type tOr = c.GetType("System.Windows.Automation.OrCondition", true);
                    tCache = c.GetType("System.Windows.Automation.CacheRequest", true);
                    Type tScope = ty.GetType("System.Windows.Automation.TreeScope", true);
                    Type tCT = ty.GetType("System.Windows.Automation.ControlType", true);
                    Type tAP = ty.GetType("System.Windows.Automation.AutomationProperty", true);
                    object ctProp = tAE.GetField("ControlTypeProperty").GetValue(null);
                    propBounds = tAE.GetField("BoundingRectangleProperty").GetValue(null);
                    propOffscreen = tAE.GetField("IsOffscreenProperty").GetValue(null);
                    ConstructorInfo pc = tProp.GetConstructor(new Type[] { tAP, typeof(object) });
                    string[] names = { "Edit", "Button", "ComboBox", "CheckBox", "Image", "TabItem", "Document", "Hyperlink", "ListItem", "DataItem", "MenuItem", "Slider" };
                    Array arr = Array.CreateInstance(tCond, names.Length);
                    for (int i = 0; i < names.Length; i++) arr.SetValue(pc.Invoke(new object[] { ctProp, tCT.GetField(names[i]).GetValue(null) }), i);
                    condInteresting = tOr.GetConstructor(new Type[] { tCond.MakeArrayType() }).Invoke(new object[] { arr });
                    condListItem = pc.Invoke(new object[] { ctProp, tCT.GetField("ListItem").GetValue(null) });
                    scopeDesc = Enum.ToObject(tScope, 4); scopeChildren = Enum.ToObject(tScope, 2);
                    fromHandle = tAE.GetMethod("FromHandle", new Type[] { typeof(IntPtr) });
                    findAll = tAE.GetMethod("FindAll", new Type[] { tScope, tCond });
                    pCached = tAE.GetProperty("Cached");
                    cacheAdd = tCache.GetMethod("Add", new Type[] { tAP });
                    cacheActivate = tCache.GetMethod("Activate");
                    return true;
                }
                catch { return false; }
            }

            // returns list of {x, y, w, h} in screen pixels
            public List<double[]> Find(IntPtr hwnd, bool children, int max)
            {
                List<double[]> res = new List<double[]>();
                try
                {
                    object cr = Activator.CreateInstance(tCache);
                    cacheAdd.Invoke(cr, new object[] { propBounds });
                    cacheAdd.Invoke(cr, new object[] { propOffscreen });
                    IDisposable act = (IDisposable)cacheActivate.Invoke(cr, null);
                    try
                    {
                        object root = fromHandle.Invoke(null, new object[] { hwnd });
                        object coll = findAll.Invoke(root, new object[] { children ? scopeChildren : scopeDesc, children ? condListItem : condInteresting });
                        foreach (object el in (IEnumerable)coll)
                        {
                            try
                            {
                                object info = pCached.GetValue(el, null);
                                if (piBounds == null)
                                {
                                    piBounds = info.GetType().GetProperty("BoundingRectangle");
                                    piOff = info.GetType().GetProperty("IsOffscreen");
                                }
                                if ((bool)piOff.GetValue(info, null)) continue;
                                object rect = piBounds.GetValue(info, null);
                                if (rX == null)
                                {
                                    Type rt = rect.GetType();
                                    rX = rt.GetProperty("X"); rY = rt.GetProperty("Y"); rW = rt.GetProperty("Width"); rH = rt.GetProperty("Height"); rEmpty = rt.GetProperty("IsEmpty");
                                }
                                if ((bool)rEmpty.GetValue(rect, null)) continue;
                                double x = (double)rX.GetValue(rect, null), y = (double)rY.GetValue(rect, null), w = (double)rW.GetValue(rect, null), h = (double)rH.GetValue(rect, null);
                                if (double.IsInfinity(x) || double.IsNaN(x)) continue;
                                res.Add(new double[] { x, y, w, h });
                                if (res.Count >= max) break;
                            }
                            catch { }
                        }
                    }
                    finally { act.Dispose(); }
                }
                catch { }
                return res;
            }
        }
    }
}
