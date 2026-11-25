using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Deskosaur
{
    static class Native
    {
        // ---- windows / gdi ----
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptX, ptY; }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr h, int idx);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GetWindowLongPtr64(IntPtr h, int idx);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr h, int idx, int v);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr h, int idx, IntPtr v);
        public static int GetExStyle(IntPtr h) { return IntPtr.Size == 8 ? (int)(long)GetWindowLongPtr64(h, -20) : GetWindowLong32(h, -20); }
        public static void SetExStyle(IntPtr h, int v) { if (IntPtr.Size == 8) SetWindowLongPtr64(h, -20, new IntPtr(v)); else SetWindowLong32(h, -20, v); }
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint crKey, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG msg, IntPtr h, uint min, uint max, uint remove);
        [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] public static extern int StretchDIBits(IntPtr dc, int xd, int yd, int wd, int hd, int xs, int ys, int ws, int hs, int[] bits, ref BITMAPINFOHEADER bmi, uint usage, uint rop);

        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);

        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

        // DPI
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int v);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        public static void MakeDpiAware()
        {
            try { if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return; } catch { }
            try { if (SetProcessDpiAwareness(2) == 0) return; } catch { }
            try { SetProcessDPIAware(); } catch { }
        }

        public const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000,
                         WS_EX_NOACTIVATE = 0x8000000, WS_EX_APPWINDOW = 0x40000;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        // Present a 32bpp framebuffer scaled with nearest neighbour
        public static void Present(IntPtr hwnd, int[] px, int w, int h, int dw, int dh)
        {
            IntPtr dc = GetDC(hwnd);
            if (dc == IntPtr.Zero) return;
            try
            {
                BITMAPINFOHEADER bi = new BITMAPINFOHEADER();
                bi.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
                SetStretchBltMode(dc, 3);
                StretchDIBits(dc, 0, 0, dw, dh, 0, 0, w, h, px, ref bi, 0, 0x00CC0020);
            }
            finally { ReleaseDC(hwnd, dc); }
        }

        public static string ClassOf(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(128);
            GetClassName(h, sb, 128);
            return sb.ToString();
        }
    }
}
