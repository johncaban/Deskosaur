using System;
using System.Collections.Generic;

namespace Deskosaur
{
    // Proportional 5x7 pixel font. Special glyphs: '@' heart, '$' gem, '&' lock, '^' up, '|' down, '<' '>' arrows
    public static class Font
    {
        struct Glyph { public byte[] Rows; public int X0, W; }
        static readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
        public const int H = 7;

        static Font()
        {
            string[] defs = {
                "A.###.|#...#|#...#|#####|#...#|#...#|#...#",
                "B####.|#...#|#...#|####.|#...#|#...#|####.",
                "C.###.|#...#|#....|#....|#....|#...#|.###.",
                "D####.|#...#|#...#|#...#|#...#|#...#|####.",
                "E#####|#....|#....|####.|#....|#....|#####",
                "F#####|#....|#....|####.|#....|#....|#....",
                "G.###.|#...#|#....|#.###|#...#|#...#|.####",
                "H#...#|#...#|#...#|#####|#...#|#...#|#...#",
                "I.###.|..#..|..#..|..#..|..#..|..#..|.###.",
                "J..###|...#.|...#.|...#.|#..#.|#..#.|.##..",
                "K#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
                "L#....|#....|#....|#....|#....|#....|#####",
                "M#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
                "N#...#|##..#|#.#.#|#..##|#...#|#...#|#...#",
                "O.###.|#...#|#...#|#...#|#...#|#...#|.###.",
                "P####.|#...#|#...#|####.|#....|#....|#....",
                "Q.###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
                "R####.|#...#|#...#|####.|#.#..|#..#.|#...#",
                "S.####|#....|#....|.###.|....#|....#|####.",
                "T#####|..#..|..#..|..#..|..#..|..#..|..#..",
                "U#...#|#...#|#...#|#...#|#...#|#...#|.###.",
                "V#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
                "W#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
                "X#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
                "Y#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
                "Z#####|....#|...#.|..#..|.#...|#....|#####",
                "0.###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
                "1..#..|.##..|..#..|..#..|..#..|..#..|.###.",
                "2.###.|#...#|....#|...#.|..#..|.#...|#####",
                "3#####|...#.|..#..|...#.|....#|#...#|.###.",
                "4...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
                "5#####|#....|####.|....#|....#|#...#|.###.",
                "6..##.|.#...|#....|####.|#...#|#...#|.###.",
                "7#####|....#|...#.|..#..|.#...|.#...|.#...",
                "8.###.|#...#|#...#|.###.|#...#|#...#|.###.",
                "9.###.|#...#|#...#|.####|....#|...#.|.##..",
                "......|.....|.....|.....|.....|.##..|.##..",
                ",.....|.....|.....|.....|.##..|..#..|.#...",
                "!..#..|..#..|..#..|..#..|..#..|.....|..#..",
                "?.###.|#...#|....#|...#.|..#..|.....|..#..",
                ":.....|..#..|..#..|.....|..#..|..#..|.....",
                "-.....|.....|.....|####.|.....|.....|.....",
                "+.....|..#..|..#..|#####|..#..|..#..|.....",
                "/....#|...#.|...#.|..#..|.#...|.#...|#....",
                "'..#..|..#..|.....|.....|.....|.....|.....",
                "(...#.|..#..|.#...|.#...|.#...|..#..|...#.",
                ").#...|..#..|...#.|...#.|...#.|..#..|.#...",
                "%##..#|##..#|...#.|..#..|.#...|#..##|#..##",
                "<...#.|..##.|.###.|####.|.###.|..##.|...#.",
                ">.#...|.##..|.###.|.####|.###.|.##..|.#...",
                "=.....|.....|#####|.....|#####|.....|.....",
                "_.....|.....|.....|.....|.....|.....|#####",
                "*.....|#.#.#|.###.|#####|.###.|#.#.#|.....",
                "#.#.#.|#####|.#.#.|.#.#.|.#.#.|#####|.#.#.",
                "@.....|.#.#.|#####|#####|.###.|..#..|.....",
                "$..#..|.###.|#####|.###.|..#..|.....|.....",
                "&.###.|#...#|#...#|#####|##.##|##.##|#####",
                "^.....|..#..|.###.|#####|.....|.....|.....",
                "|.....|#####|.###.|..#..|.....|.....|.....",
            };
            foreach (string d in defs)
            {
                char ch = d[0];
                string[] rows = d.Substring(1).Split('|');
                byte[] bits = new byte[7];
                int minx = 5, maxx = -1;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 5; x++)
                        if (rows[y][x] == '#') { bits[y] |= (byte)(1 << x); if (x < minx) minx = x; if (x > maxx) maxx = x; }
                Glyph g = new Glyph();
                g.Rows = bits;
                if (maxx < 0) { g.X0 = 0; g.W = 3; } else { g.X0 = minx; g.W = maxx - minx + 1; }
                if (char.IsLetterOrDigit(ch) && ch != 'I' && ch != '1') { g.X0 = 0; g.W = 5; }
                glyphs[ch] = g;
            }
        }

        static bool TryGet(char c, out Glyph g)
        {
            c = char.ToUpperInvariant(c);
            return glyphs.TryGetValue(c, out g);
        }

        public static int Measure(string s, int scale)
        {
            int w = 0;
            foreach (char c in s)
            {
                Glyph g;
                if (c == ' ') w += 3 * scale;
                else if (TryGet(c, out g)) w += (g.W + 1) * scale;
            }
            return Math.Max(0, w - scale);
        }

        public static void Text(Surface s, string str, int x, int y, int col, int scale)
        {
            int cx = x;
            foreach (char c in str)
            {
                if (c == ' ') { cx += 3 * scale; continue; }
                Glyph g;
                if (!TryGet(c, out g)) continue;
                for (int yy = 0; yy < 7; yy++)
                {
                    byte b = g.Rows[yy];
                    for (int xx = 0; xx < g.W; xx++)
                        if ((b & (1 << (xx + g.X0))) != 0)
                        {
                            if (scale == 1) G.Px(s, cx + xx, y + yy, col);
                            else G.Rect(s, cx + xx * scale, y + yy * scale, scale, scale, col);
                        }
                }
                cx += (g.W + 1) * scale;
            }
        }

        // Text with 1px dark outline (reads on any desktop wallpaper)
        public static void TextO(Surface s, string str, int x, int y, int col, int outline, int scale)
        {
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    if (ox != 0 || oy != 0) Text(s, str, x + ox, y + oy, outline, scale);
            Text(s, str, x, y, col, scale);
        }

        public static void TextShadow(Surface s, string str, int x, int y, int col, int shadow, int scale)
        {
            Text(s, str, x + scale, y + scale, shadow, scale);
            Text(s, str, x, y, col, scale);
        }

        public static void Centered(Surface s, string str, int cx, int y, int col, int scale)
        {
            Text(s, str, cx - Measure(str, scale) / 2, y, col, scale);
        }
        public static void CenteredO(Surface s, string str, int cx, int y, int col, int outline, int scale)
        {
            TextO(s, str, cx - Measure(str, scale) / 2, y, col, outline, scale);
        }
    }
}
