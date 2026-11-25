using System;
using System.Collections.Generic;

namespace Deskosaur
{
    // Tiny chiptune synth + sequencer. Platform layer calls Render() from its audio thread.
    public static class Audio
    {
        public const int Rate = 22050;
        static readonly object sync = new object();
        static Dictionary<string, short[]> sfx = new Dictionary<string, short[]>();
        struct Voice { public short[] Data; public int Pos; public float Vol; }
        static List<Voice> voices = new List<Voice>();
        static Random rng = new Random(7);
        public static float Master = 0.7f;
        public static bool SoundOn = true, MusicOn = true;
        static Song song, nextSong; static float songFade = 1f;

        // ---------------- SFX generation ----------------
        delegate float Wave(float t, float p);

        static float Sq(float ph, float duty) { ph -= (float)Math.Floor(ph); return ph < duty ? 1f : -1f; }
        static float Tri(float ph) { ph -= (float)Math.Floor(ph); return ph < 0.5f ? ph * 4 - 1 : 3 - ph * 4; }
        static float Noise() { return (float)rng.NextDouble() * 2 - 1; }

        static short[] Make(float dur, Func<float, float, float> f)
        {
            int n = (int)(dur * Rate);
            short[] d = new short[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, u = i / (float)n;
                float v = f(t, u);
                if (v > 1) v = 1; if (v < -1) v = -1;
                d[i] = (short)(v * 9000);
            }
            return d;
        }

        static short[] Sweep(float dur, float f0, float f1, float duty, float vol)
        {
            float ph = 0;
            return Make(dur, delegate (float t, float u) { float f = f0 + (f1 - f0) * u; ph += f / Rate; return Sq(ph, duty) * vol * (1 - u) * (1 - u * 0.3f); });
        }

        static short[] Arp(float step, float[] notes, float duty, float vol, float tail)
        {
            float ph = 0; float dur = step * notes.Length + tail;
            return Make(dur, delegate (float t, float u)
            {
                int k = Math.Min(notes.Length - 1, (int)(t / step));
                ph += notes[k] / Rate;
                float env = t > step * notes.Length ? Math.Max(0, 1 - (t - step * notes.Length) / Math.Max(0.001f, tail)) : 1f;
                float local = (t - k * step) / step; if (t < step * notes.Length) env *= 1 - local * 0.4f;
                return Sq(ph, duty) * vol * env;
            });
        }

        static void Init()
        {
            if (sfx.Count > 0) return;
            float ph = 0;
            sfx["jump"] = Sweep(0.12f, 260, 620, 0.25f, 0.55f);
            sfx["djump"] = Make(0.16f, delegate (float t, float u) { ph += (420 + 700 * u + (float)Math.Sin(t * 120) * 40) / Rate; return Sq(ph, 0.125f) * 0.5f * (1 - u); });
            sfx["land"] = Make(0.07f, delegate (float t, float u) { return Noise() * 0.45f * (1 - u) * (1 - u); });
            ph = 0; sfx["spring"] = Make(0.32f, delegate (float t, float u) { float f = 200 + 420 * (float)Math.Sin(u * 3.14f) + 60 * (float)Math.Sin(t * 70); ph += f / Rate; return (float)Math.Sin(ph * 6.283f) * 0.8f * (1 - u); });
            sfx["orb"] = Arp(0.045f, new float[] { 660, 880, 1320, 1760 }, 0.5f, 0.35f, 0.08f);
            ph = 0; sfx["portal"] = Make(0.38f, delegate (float t, float u) { float f = 300 + 900 * (float)Math.Abs(Math.Sin(u * 9)); ph += f / Rate; return Tri(ph) * 0.6f * (1 - u); });
            sfx["gem"] = Arp(0.035f, new float[] { 1320, 1980 }, 0.25f, 0.3f, 0.06f);
            sfx["amber"] = Arp(0.05f, new float[] { 784, 988, 1175, 1568, 1976 }, 0.25f, 0.35f, 0.2f);
            ph = 0; sfx["death"] = Make(0.45f, delegate (float t, float u) { ph += (600 - 520 * u) / Rate; return (Sq(ph, 0.5f) * 0.4f + Noise() * 0.15f * (1 - u)) * (1 - u); });
            ph = 0; sfx["respawn"] = Make(0.22f, delegate (float t, float u) { ph += (220 + 700 * u) / Rate; return Tri(ph) * 0.6f * (1 - u * 0.6f); });
            sfx["checkpoint"] = Arp(0.08f, new float[] { 523, 784, 1047 }, 0.5f, 0.3f, 0.15f);
            sfx["win"] = Arp(0.05f, new float[] { 523, 659, 784, 1047, 1319, 1568, 2093 }, 0.25f, 0.3f, 0.3f);
            ph = 0; sfx["suck"] = Make(1.1f, delegate (float t, float u) { ph += (160 + 1300 * u * u) / Rate; return (Tri(ph) * 0.45f + Noise() * 0.18f * u) * (0.3f + u * 0.7f) * (u > 0.92f ? (1 - u) / 0.08f : 1); });
            sfx["clear"] = Arp(0.09f, new float[] { 523, 659, 784, 1047, 784, 1047 }, 0.5f, 0.32f, 0.45f);
            sfx["skid"] = Make(0.05f, delegate (float t, float u) { return Noise() * 0.18f * (1 - u); });
            sfx["crumble"] = Make(0.2f, delegate (float t, float u) { return Noise() * 0.4f * (1 - u) * (float)(0.5 + 0.5 * Math.Sin(t * 300)); });
            sfx["click"] = Sweep(0.025f, 900, 900, 0.5f, 0.25f);
            sfx["select"] = Sweep(0.07f, 660, 1320, 0.25f, 0.35f);
            sfx["back"] = Sweep(0.07f, 660, 380, 0.25f, 0.35f);
            sfx["pause"] = Arp(0.06f, new float[] { 784, 523 }, 0.5f, 0.3f, 0.05f);
            sfx["start"] = Arp(0.05f, new float[] { 392, 523, 659, 784 }, 0.25f, 0.28f, 0.1f);
            sfx["equip"] = Arp(0.04f, new float[] { 988, 1319 }, 0.25f, 0.3f, 0.05f);
            sfx["locked"] = Sweep(0.12f, 200, 150, 0.5f, 0.35f);
            float lp = 0;
            sfx["thunder"] = Make(2.2f, delegate (float t, float u) { lp += (Noise() - lp) * (0.03f + 0.05f * (1 - u)); float env = (u < 0.05f ? u / 0.05f : 1) * (float)Math.Pow(1 - u, 1.6) * (0.7f + 0.3f * (float)Math.Sin(t * 9)); return lp * 3.2f * env; });
        }

        public static void Play(string name)
        {
            if (!SoundOn) return;
            lock (sync)
            {
                Init();
                short[] d;
                if (!sfx.TryGetValue(name, out d)) return;
                // limit duplicate spam
                for (int i = 0; i < voices.Count; i++) if (voices[i].Data == d && voices[i].Pos < 800) return;
                if (voices.Count > 12) voices.RemoveAt(0);
                Voice v = new Voice(); v.Data = d; v.Pos = 0; v.Vol = 1f;
                voices.Add(v);
            }
        }

        public static void SetSong(int idx)
        {
            lock (sync)
            {
                Song target = idx < 0 ? null : (idx == 0 ? Song.NeonBar() : Song.DeskJam());
                if (song == null) { song = target; songFade = 1; }
                else { nextSong = target; if (target == null) nextSong = new Song(); }
            }
        }

        public static void Render(short[] buf, int n)
        {
            lock (sync)
            {
                Init();
                for (int i = 0; i < n; i++)
                {
                    float acc = 0;
                    for (int k = voices.Count - 1; k >= 0; k--)
                    {
                        Voice v = voices[k];
                        acc += v.Data[v.Pos] * v.Vol;
                        v.Pos++;
                        if (v.Pos >= v.Data.Length) voices.RemoveAt(k); else voices[k] = v;
                    }
                    if (nextSong != null)
                    {
                        songFade -= 1f / (Rate * 0.4f);
                        if (songFade <= 0) { song = nextSong.Empty ? null : nextSong; nextSong = null; songFade = 1; }
                    }
                    if (song != null && MusicOn) acc += song.Sample() * 9000f * 0.55f * songFade;
                    acc *= Master;
                    if (acc > 32000) acc = 32000; if (acc < -32000) acc = -32000;
                    buf[i] = (short)acc;
                }
            }
        }

        // ---------------- music ----------------
        sealed class Chan { public float Freq, Ph, Amp, Decay, Duty; public int Type; }

        sealed class Song
        {
            public bool Empty = true;
            int bpm; int step, sampleInStep, samplesPerStep;
            string[] chords; string bassPat, arpPat, kick, snare, hat; string[] lead;
            Chan bass = new Chan(), arp = new Chan(), ld = new Chan(), drum = new Chan();
            float leadVol, arpVol;
            int noiseLfsr = 1;

            public static Song NeonBar()
            {
                Song s = new Song(); s.Empty = false; s.bpm = 86;
                s.chords = new string[] { "A3 C4 E4 G4", "F3 A3 C4 E4", "D3 F3 A3 C4", "E3 G#3 B3 D4" };
                s.bassPat = "R.....R...R.R...";
                s.arpPat = "0123210301232103";
                s.kick = "x.........x.....";
                s.snare = "....x.......x...";
                s.hat = "..x...x...x...xx";
                s.lead = new string[] {
                    "E5 - - . G5 - A5 - . . E5 - D5 - C5 -", "C5 - - . A4 - . . C5 - D5 - E5 - - .",
                    "F5 - - . E5 - D5 - . . C5 - D5 - . .", "B4 - - - G#4 - - - B4 - D5 - E5 - - -" };
                s.leadVol = 0.16f; s.arpVol = 0.07f;
                s.Init(); return s;
            }

            public static Song DeskJam()
            {
                Song s = new Song(); s.Empty = false; s.bpm = 118;
                s.chords = new string[] { "C4 E4 G4 B4", "A3 C4 E4 G4", "F3 A3 C4 E4", "G3 B3 D4 F4" };
                s.bassPat = "R.R.5.R.R.R.5.R.";
                s.arpPat = "0123012301230123";
                s.kick = "x...x...x...x...";
                s.snare = "....x.......x..x";
                s.hat = "xxx.xxx.xxx.xxx.";
                s.lead = new string[] {
                    "G5 - E5 - C5 - E5 - G5 - - - A5 - G5 -", "E5 - - - C5 - D5 - E5 - G5 - E5 - - -",
                    "F5 - E5 - D5 - C5 - A4 - C5 - D5 - - -", "D5 - - - B4 - G4 - B4 - D5 - G5 - - -" };
                s.leadVol = 0.12f; s.arpVol = 0.05f;
                s.Init(); return s;
            }

            void Init()
            {
                samplesPerStep = Rate * 60 / bpm / 4;
                ld.Type = 0; ld.Duty = 0.25f; arp.Type = 0; arp.Duty = 0.125f; bass.Type = 1; drum.Type = 2;
                step = -1; sampleInStep = samplesPerStep;
            }

            static float NoteFreq(string n)
            {
                if (string.IsNullOrEmpty(n) || n == "-" || n == ".") return 0;
                int[] semis = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
                int idx = "ABCDEFG".IndexOf(n[0]); if (idx < 0) return 0;
                int s = semis[idx]; int p = 1;
                if (n.Length > 2 && n[1] == '#') { s++; p = 2; }
                int oct = n[p] - '0';
                int midi = (oct + 1) * 12 + s;
                return 440f * (float)Math.Pow(2, (midi - 69) / 12.0);
            }

            void Trigger()
            {
                step++;
                int bar = (step / 16) % 4, st = step % 16;
                string[] chord = chords[bar].Split(' ');
                // bass
                char b = bassPat[st];
                if (b == 'R' || b == '5')
                {
                    float f = NoteFreq(chord[0]) / 2; if (b == '5') f = NoteFreq(chord[2]) / 2;
                    bass.Freq = f; bass.Amp = 0.32f; bass.Decay = 0.99985f;
                }
                // arp
                int ai = arpPat[st] - '0';
                arp.Freq = NoteFreq(chord[Math.Min(ai, chord.Length - 1)]) * 2; arp.Amp = arpVol; arp.Decay = 0.9996f;
                // lead
                string[] notes = lead[bar].Split(' ');
                string n = st < notes.Length ? notes[st] : ".";
                if (n == ".") ld.Amp *= 0.3f;
                else if (n != "-") { ld.Freq = NoteFreq(n); ld.Amp = leadVol; ld.Decay = 0.99993f; }
                // drums
                if (kick[st] == 'x') { drum.Type = 3; drum.Amp = 0.55f; drum.Decay = 0.9993f; drum.Freq = 140; }
                else if (snare[st] == 'x') { drum.Type = 2; drum.Amp = 0.22f; drum.Decay = 0.9990f; }
                else if (hat[st] == 'x') { drum.Type = 4; drum.Amp = 0.07f; drum.Decay = 0.996f; }
            }

            public float Sample()
            {
                if (++sampleInStep >= samplesPerStep) { sampleInStep = 0; Trigger(); }
                float v = 0;
                v += Osc(ld); v += Osc(arp); v += Osc(bass);
                // drums
                if (drum.Amp > 0.001f)
                {
                    if (drum.Type == 3) { drum.Freq *= 0.9997f; drum.Ph += drum.Freq / Rate; v += (float)Math.Sin(drum.Ph * 6.283f) * drum.Amp; }
                    else
                    {
                        int bit = ((noiseLfsr >> 0) ^ (noiseLfsr >> 1)) & 1; noiseLfsr = (noiseLfsr >> 1) | (bit << 14);
                        float nz = (noiseLfsr & 1) == 1 ? 1 : -1;
                        v += nz * drum.Amp;
                    }
                    drum.Amp *= drum.Decay;
                }
                return v;
            }

            static float Osc(Chan c)
            {
                if (c.Amp < 0.0005f || c.Freq <= 0) return 0;
                c.Ph += c.Freq / Rate; if (c.Ph > 1) c.Ph -= 1;
                float s = c.Type == 1 ? Tri(c.Ph) : Sq(c.Ph, c.Duty);
                float o = s * c.Amp; c.Amp *= c.Decay;
                return o;
            }
        }
    }
}
