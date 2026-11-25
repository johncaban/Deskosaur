using System;

namespace Deskosaur
{
    public static class Ui
    {
        public static readonly int Ink = G.Hex(0x120c1c);
        public static readonly int Panel = G.Hex(0x17122a);
        public static readonly int Panel2 = G.Hex(0x221a3c);
        public static readonly int Neon = G.Hex(0x3ee0c8);
        public static readonly int Pink = G.Hex(0xff4ab8);
        public static readonly int Gold = G.Hex(0xffc928);
        public static readonly int Text = G.Hex(0xf4ecff);
        public static readonly int Dim = G.Hex(0x8a80a8);

        public static void Box(Surface s, int x, int y, int w, int h, int border)
        {
            G.Rect(s, x + 2, y + 2, w, h, Ink);            // drop shadow
            G.Rect(s, x, y, w, h, Panel);
            G.RectD(s, x + 1, y + 1, w - 2, h / 3, Panel2, 8);
            G.RectO(s, x - 1, y - 1, w + 2, h + 2, Ink);
            G.RectO(s, x, y, w, h, border);
            // corner studs
            G.Px(s, x + 2, y + 2, border); G.Px(s, x + w - 3, y + 2, border); G.Px(s, x + 2, y + h - 3, border); G.Px(s, x + w - 3, y + h - 3, border);
        }

        public static bool Hit(Input inp, int x, int y, int w, int h)
        {
            return inp != null && inp.MouseX >= x && inp.MouseX < x + w && inp.MouseY >= y && inp.MouseY < y + h;
        }

        // Pill button. Returns true when clicked.
        public static bool Button(Surface s, Input inp, string label, int x, int y, int w, bool selected, int t)
        {
            int h = 13;
            bool hover = Hit(inp, x, y, w, h);
            bool on = selected || hover;
            int bg = on ? G.Hex(0x3a2a6a) : G.Hex(0x241c40);
            int border = on ? (((t / 8) & 1) == 0 ? Pink : G.Hex(0xff8ad6)) : G.Hex(0x5a4a8a);
            G.Rect(s, x + 1, y + 2, w, h, Ink);
            G.Rect(s, x, y, w, h, bg);
            G.HLine(s, x + 1, x + w - 2, y + 1, on ? G.Hex(0x5a46a0) : G.Hex(0x30264e));
            G.RectO(s, x, y, w, h, border);
            G.Px(s, x, y, Ink); G.Px(s, x + w - 1, y, Ink); G.Px(s, x, y + h - 1, Ink); G.Px(s, x + w - 1, y + h - 1, Ink);
            Font.Centered(s, label, x + w / 2 + 1, y + 4, Ink, 1);
            Font.Centered(s, label, x + w / 2, y + 3, on ? G.Hex(0xffffff) : Text, 1);
            if (selected) { Font.Text(s, ">", x - 7 + ((t / 10) & 1), y + 3, Pink, 1); }
            return hover && inp.Click;
        }

        public static void Amber(Surface s, int x, int y, bool full)
        {
            int k = Ink;
            int a = full ? G.Hex(0xffb030) : G.Hex(0x4e4470), b = full ? G.Hex(0xd07010) : G.Hex(0x3a3258), w = full ? G.Hex(0xfff0a0) : G.Hex(0x6a6090);
            string[] art = { ".kkk.", "kawak", "kaaab", "kamab", "kaabk", ".kbk.", "..k.." };
            for (int j = 0; j < art.Length; j++)
                for (int i = 0; i < 5; i++)
                {
                    char c = art[j][i];
                    if (c == '.') continue;
                    int col = c == 'k' ? k : c == 'a' ? a : c == 'b' ? b : c == 'w' ? w : (full ? G.Hex(0x3a2010) : a);
                    G.Px(s, x + i, y + j, col);
                }
        }

        public static void Gem(Surface s, int x, int y)
        {
            int k = G.Hex(0x0a2a4a), c = G.Hex(0x4ae0ff), w = G.Hex(0xd8fcff), d = G.Hex(0x2a90c8);
            G.Px(s, x + 2, y, k);
            G.Px(s, x + 1, y + 1, k); G.Px(s, x + 2, y + 1, w); G.Px(s, x + 3, y + 1, k);
            G.Px(s, x, y + 2, k); G.Px(s, x + 1, y + 2, w); G.Px(s, x + 2, y + 2, c); G.Px(s, x + 3, y + 2, d); G.Px(s, x + 4, y + 2, k);
            G.Px(s, x + 1, y + 3, k); G.Px(s, x + 2, y + 3, d); G.Px(s, x + 3, y + 3, k);
            G.Px(s, x + 2, y + 4, k);
        }
    }
}
