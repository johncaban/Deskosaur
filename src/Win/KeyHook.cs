using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Deskosaur
{
    // Global low-level keyboard hook: lets the dino be controlled while other windows have focus,
    // and swallows game keys so they don't type into whatever app is focused.
    sealed class KeyHook : IDisposable
    {
        delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, LowLevelProc proc, IntPtr hMod, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);

        readonly LowLevelProc proc;
        IntPtr hook;
        public readonly bool[] Down = new bool[256];
        public Func<int, bool> Swallow;

        public KeyHook() { proc = Callback; }

        public bool Install()
        {
            if (hook != IntPtr.Zero) return true;
            for (int i = 0; i < 256; i++) Down[i] = false;
            IntPtr mod = IntPtr.Zero;
            try { mod = GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName); } catch { }
            hook = SetWindowsHookEx(13, proc, mod, 0);
            if (hook == IntPtr.Zero) hook = SetWindowsHookEx(13, proc, IntPtr.Zero, 0);
            return hook != IntPtr.Zero;
        }

        public void Uninstall()
        {
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            for (int i = 0; i < 256; i++) Down[i] = false;
        }

        IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vk = Marshal.ReadInt32(lParam) & 0xFF;
                int msg = wParam.ToInt32();
                bool down = msg == 0x100 || msg == 0x104;
                bool up = msg == 0x101 || msg == 0x105;
                if (down) Down[vk] = true; else if (up) Down[vk] = false;
                try { if (Swallow != null && Swallow(vk)) return new IntPtr(1); } catch { }
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose() { Uninstall(); }
    }

    // Minimal waveOut streaming (no dependencies). Pulls samples from Audio.Render on its own thread.
    sealed class WaveOut : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public short wFormatTag, nChannels; public int nSamplesPerSec, nAvgBytesPerSec; public short nBlockAlign, wBitsPerSample, cbSize; }
        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public int dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public int dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);

        const int Bufs = 4, Samples = 768;
        IntPtr dev;
        IntPtr[] hdr = new IntPtr[Bufs], data = new IntPtr[Bufs];
        Thread th; volatile bool run;
        int hdrSize, flagsOff;

        public bool Start()
        {
            try
            {
                WAVEFORMATEX f = new WAVEFORMATEX();
                f.wFormatTag = 1; f.nChannels = 1; f.nSamplesPerSec = Audio.Rate; f.wBitsPerSample = 16;
                f.nBlockAlign = 2; f.nAvgBytesPerSec = Audio.Rate * 2;
                if (waveOutOpen(out dev, -1, ref f, IntPtr.Zero, IntPtr.Zero, 0) != 0) return false;
                hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
                flagsOff = Marshal.OffsetOf(typeof(WAVEHDR), "dwFlags").ToInt32();
                for (int i = 0; i < Bufs; i++)
                {
                    data[i] = Marshal.AllocHGlobal(Samples * 2);
                    WAVEHDR h = new WAVEHDR(); h.lpData = data[i]; h.dwBufferLength = Samples * 2;
                    hdr[i] = Marshal.AllocHGlobal(hdrSize);
                    Marshal.StructureToPtr(h, hdr[i], false);
                    waveOutPrepareHeader(dev, hdr[i], hdrSize);
                }
                run = true;
                th = new Thread(Loop); th.IsBackground = true; th.Priority = ThreadPriority.AboveNormal; th.Name = "audio";
                th.Start();
                return true;
            }
            catch { return false; }
        }

        void Loop()
        {
            short[] tmp = new short[Samples];
            bool[] queued = new bool[Bufs];
            while (run)
            {
                for (int i = 0; i < Bufs; i++)
                {
                    int flags = Marshal.ReadInt32(hdr[i], flagsOff);
                    if (queued[i] && (flags & 1) == 0) continue; // still playing
                    try { Audio.Render(tmp, Samples); } catch { Array.Clear(tmp, 0, tmp.Length); }
                    Marshal.Copy(tmp, 0, data[i], Samples);
                    Marshal.WriteInt32(hdr[i], flagsOff, flags & ~1);
                    waveOutWrite(dev, hdr[i], hdrSize);
                    queued[i] = true;
                }
                Thread.Sleep(4);
            }
        }

        public void Dispose()
        {
            run = false;
            if (th != null) th.Join(200);
            if (dev != IntPtr.Zero)
            {
                waveOutReset(dev);
                for (int i = 0; i < Bufs; i++) { if (hdr[i] != IntPtr.Zero) { waveOutUnprepareHeader(dev, hdr[i], hdrSize); Marshal.FreeHGlobal(hdr[i]); } if (data[i] != IntPtr.Zero) Marshal.FreeHGlobal(data[i]); }
                waveOutClose(dev); dev = IntPtr.Zero;
            }
        }
    }
}
