using System;
using System.IO;
using System.Text;

namespace Deskosaur
{
    public enum Key { Left, Right, Up, Down, Jump, Confirm, Back, Restart, Pause, Count }

    // Abstract input: platform layer fills Down[], game reads Pressed[] edges
    public sealed class Input
    {
        public bool[] Down = new bool[(int)Key.Count];
        bool[] prev = new bool[(int)Key.Count];
        public bool[] Pressed = new bool[(int)Key.Count];
        public int MouseX = -1, MouseY = -1;
        public bool Click, MouseDown;
        public int Wheel;
        public void Latch()
        {
            for (int i = 0; i < Down.Length; i++) { Pressed[i] = Down[i] && !prev[i]; prev[i] = Down[i]; }
        }
        public bool P(Key k) { return Pressed[(int)k]; }
        public bool D(Key k) { return Down[(int)k]; }
        public void Reset() { for (int i = 0; i < Down.Length; i++) { Down[i] = false; prev[i] = false; Pressed[i] = false; } Click = false; }
    }

    public static class Sfx
    {
        public static Action<string> Player;
        public static void Play(string name) { if (Player != null) Player(name); }
    }

    public sealed class SaveData
    {
        public int Unlocked = 1;                 // number of unlocked levels
        public int[] Best = new int[20];         // best time in frames (0 = none)
        public int[] Amber = new int[20];        // bitmask of collected ambers
        public Outfit Look = new Outfit();
        public bool DeskOn = true, ShowEdges = true, Sound = true, Music = true, Wasd = true, Crt = true;
        public int Volume = 7;                   // 0..10
        public int Scale = 0;                    // 0 = auto
        public string Path;

        public int TotalAmber()
        {
            int n = 0;
            foreach (int m in Amber) for (int b = 0; b < 3; b++) if ((m & (1 << b)) != 0) n++;
            return n;
        }

        public static SaveData Load(string path)
        {
            SaveData d = new SaveData(); d.Path = path;
            try
            {
                if (path == null || !File.Exists(path)) return d;
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('='); if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int iv; int.TryParse(v, out iv);
                    switch (k)
                    {
                        case "unlocked": d.Unlocked = Math.Max(1, Math.Min(20, iv)); break;
                        case "best": Arr(v, d.Best); break;
                        case "amber": Arr(v, d.Amber); break;
                        case "char": d.Look.Char = Clamp(iv, 4); break;
                        case "hat": d.Look.Hat = Clamp(iv, Art.Hats.Length); break;
                        case "glasses": d.Look.Glasses = Clamp(iv, Art.Glasses.Length); break;
                        case "shirt": d.Look.Shirt = Clamp(iv, Art.Shirts.Length); break;
                        case "shoes": d.Look.Shoes = Clamp(iv, Art.Shoes.Length); break;
                        case "desk": d.DeskOn = iv != 0; break;
                        case "edges": d.ShowEdges = iv != 0; break;
                        case "sound": d.Sound = iv != 0; break;
                        case "music": d.Music = iv != 0; break;
                        case "wasd": d.Wasd = iv != 0; break;
                        case "crt": d.Crt = iv != 0; break;
                        case "volume": d.Volume = Math.Max(0, Math.Min(10, iv)); break;
                        case "scale": d.Scale = Math.Max(0, Math.Min(6, iv)); break;
                    }
                }
            }
            catch { }
            return d;
        }

        static int Clamp(int v, int n) { return v < 0 || v >= n ? 0 : v; }
        static void Arr(string v, int[] a)
        {
            string[] p = v.Split(',');
            for (int i = 0; i < p.Length && i < a.Length; i++) { int x; if (int.TryParse(p[i], out x)) a[i] = x; }
        }

        public void Write()
        {
            if (Path == null) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# Deskosaur save");
                sb.AppendLine("unlocked=" + Unlocked);
                sb.AppendLine("best=" + string.Join(",", Array.ConvertAll(Best, delegate (int x) { return x.ToString(); })));
                sb.AppendLine("amber=" + string.Join(",", Array.ConvertAll(Amber, delegate (int x) { return x.ToString(); })));
                sb.AppendLine("char=" + Look.Char); sb.AppendLine("hat=" + Look.Hat); sb.AppendLine("glasses=" + Look.Glasses);
                sb.AppendLine("shirt=" + Look.Shirt); sb.AppendLine("shoes=" + Look.Shoes);
                sb.AppendLine("desk=" + (DeskOn ? 1 : 0)); sb.AppendLine("edges=" + (ShowEdges ? 1 : 0));
                sb.AppendLine("sound=" + (Sound ? 1 : 0)); sb.AppendLine("music=" + (Music ? 1 : 0));
                sb.AppendLine("wasd=" + (Wasd ? 1 : 0)); sb.AppendLine("crt=" + (Crt ? 1 : 0));
                sb.AppendLine("volume=" + Volume); sb.AppendLine("scale=" + Scale);
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path, sb.ToString());
            }
            catch { }
        }

        public static string Time(int frames)
        {
            if (frames <= 0) return "--:--.-";
            int tenths = frames / 6;
            int m = tenths / 600, sec = (tenths / 10) % 60, t = tenths % 10;
            return m.ToString("00") + ":" + sec.ToString("00") + "." + t;
        }
    }
}
