using UnityEngine;

namespace GameVSM
{
    // Procedural sound bank. Every clip is computed from oscillators and seeded noise, so the
    // build ships no recordings, voices or third-party audio. Clips are mono, 22.05 kHz; loops
    // are made seamless by blending their tail into the head. Cards: docs/planning/audio-cards-2026-09-27.md.
    public static class AmbientSynth
    {
        public const int Rate = 22050;
        public enum Surface { Concrete, Asphalt, Stone, CarFloor, Gravel }
        const float Tau = Mathf.PI * 2;

        // RBJ biquad; kind 0 = low-pass, 1 = high-pass, 2 = band-pass (0 dB peak).
        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            static Biquad Make(float f, float q, int kind)
            {
                float w = Tau * Mathf.Clamp(f, 10, Rate * .45f) / Rate, c = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2 * q), a0 = 1 + alpha;
                var b = new Biquad { a1 = -2 * c / a0, a2 = (1 - alpha) / a0 };
                if (kind == 0) { b.b0 = b.b2 = (1 - c) / 2 / a0; b.b1 = (1 - c) / a0; }
                else if (kind == 1) { b.b0 = b.b2 = (1 + c) / 2 / a0; b.b1 = -(1 + c) / a0; }
                else { b.b0 = alpha / a0; b.b1 = 0; b.b2 = -alpha / a0; }
                return b;
            }
            public static Biquad Low(float f, float q = .707f) => Make(f, q, 0);
            public static Biquad High(float f, float q = .707f) => Make(f, q, 1);
            public static Biquad Band(float f, float q) => Make(f, q, 2);
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y; return y;
            }
        }

        static float N(System.Random r) => (float)r.NextDouble() * 2 - 1;
        static float U(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        static float[] Buffer(float seconds) => new float[(int)(Rate * seconds)];

        static AudioClip Clip(string name, float[] s, float peak)
        {
            float max = 1e-6f; foreach (var v in s) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < s.Length; i++) s[i] *= peak / max;
            // Off the main thread only the samples are computed; the clip is created later on the main thread.
            if (capturing) { captured = (name, s); return null; }
            return Create((name, s));
        }
        [System.ThreadStatic] static bool capturing;
        [System.ThreadStatic] static (string name, float[] data) captured;
        // Runs a clip factory without touching the Unity API, so it can be called from a worker thread.
        public static (string name, float[] data) Samples(System.Func<AudioClip> make)
        {
            capturing = true; captured = default;
            try { make(); return captured; } finally { capturing = false; }
        }
        public static AudioClip Create((string name, float[] data) pcm)
        {
            var clip = AudioClip.Create(pcm.name, pcm.data.Length, 1, Rate, false); clip.SetData(pcm.data, 0); return clip;
        }
        // The last half second continues the signal; it is blended into the head so the loop wraps without a click.
        static AudioClip Loop(string name, float[] s, float peak)
        {
            int fade = Rate / 2; var o = new float[s.Length - fade]; System.Array.Copy(s, o, o.Length);
            for (int i = 0; i < fade; i++) { float k = (float)i / fade; o[i] = o[i] * Mathf.Sqrt(k) + s[o.Length + i] * Mathf.Sqrt(1 - k); }
            return Clip(name, o, peak);
        }
        static void Filter(float[] s, Biquad f) { for (int i = 0; i < s.Length; i++) s[i] = f.Run(s[i]); }
        static void FadeTail(float[] s, float seconds)
        {
            int n = Mathf.Min(s.Length, (int)(Rate * seconds));
            for (int i = 0; i < n; i++) s[s.Length - 1 - i] *= (float)i / n;
        }
        // Baked Schroeder reverb (four damped combs, two all-passes): cheaper than a runtime filter on Android.
        static void Reverb(float[] s, float wet, float feedback, float damp = .35f, float size = 1)
        {
            int[] combs = { 557, 593, 638, 677 }, passes = { 278, 220 };
            var dry = (float[])s.Clone(); var sum = new float[s.Length];
            foreach (int length in combs)
            {
                int d = (int)(length * size); var line = new float[d]; float low = 0; int p = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    float y = line[p]; low = y * (1 - damp) + low * damp;
                    line[p] = dry[i] + low * feedback; p = (p + 1) % d; sum[i] += y * .25f;
                }
            }
            foreach (int d in passes)
            {
                var line = new float[d]; int p = 0;
                for (int i = 0; i < s.Length; i++) { float b = line[p]; line[p] = sum[i] + b * .5f; sum[i] = b - sum[i]; p = (p + 1) % d; }
            }
            for (int i = 0; i < s.Length; i++) s[i] = dry[i] + sum[i] * wet;
        }
        // Struck metal: inharmonic partials plus a short noise transient.
        static void Metal(float[] s, float at, float f, float decay, float gain, System.Random r)
        {
            float[] ratio = { 1, 2.76f, 5.4f, 8.93f }, phase = new float[4];
            for (int k = 0; k < 4; k++) phase[k] = U(r, 0, Tau);
            int start = (int)(at * Rate), end = Mathf.Min(s.Length, start + (int)(Rate * 6 / decay));
            for (int i = start; i < end; i++)
            {
                float t = (float)(i - start) / Rate, v = 0;
                for (int k = 0; k < 4; k++)
                    if (f * ratio[k] < Rate * .45f) v += Mathf.Sin(Tau * f * ratio[k] * t + phase[k]) * Mathf.Exp(-t * decay * (1 + k * .7f)) / (1 + k);
                s[i] += gain * (v + N(r) * Mathf.Exp(-t * 600) * .8f);
            }
        }
        static void Click(float[] s, float at, float f, float gain, System.Random r)
        {
            var b = Biquad.Band(f, 2); int start = (int)(at * Rate), end = Mathf.Min(s.Length, start + Rate / 25);
            for (int i = start; i < end; i++) s[i] += b.Run(N(r) * Mathf.Exp(-(i - start) * 400f / Rate)) * gain * 4;
        }
        // Unintelligible crowd: buzz and breath through vowel formants that jump per syllable,
        // low-passed as if heard across the platform. It carries no language or real voice.
        static float[] Murmur(System.Random r, int n, int voices, float cutoff)
        {
            var o = new float[n];
            for (int v = 0; v < voices; v++)
            {
                float pitch = U(r, 95, 225), phase = 0, gain = U(r, .5f, 1);
                var lp = Biquad.Low(cutoff); int i = (int)(r.NextDouble() * Rate);
                while (i < n)
                {
                    int syllables = 3 + r.Next(6);
                    for (int k = 0; k < syllables && i < n; k++)
                    {
                        int length = (int)(Rate * U(r, .11f, .3f)); float bend = U(r, .9f, 1.15f);
                        var f1 = Biquad.Band(U(r, 300, 800), 4); var f2 = Biquad.Band(U(r, 900, 2200), 5);
                        for (int j = 0; j < length && i < n; j++, i++)
                        {
                            float e = Mathf.Sin(Mathf.PI * j / length); phase += pitch * bend / Rate; if (phase > 1) phase -= 1;
                            float source = (phase * 2 - 1) * .6f + N(r) * .4f;
                            o[i] += lp.Run((f1.Run(source) + f2.Run(source) * .6f) * e * e) * gain;
                        }
                    }
                    int pause = (int)(Rate * U(r, .3f, 2.1f));
                    for (int j = 0; j < pause && i < n; j++, i++) o[i] += lp.Run(0) * gain;
                }
            }
            return o;
        }

        // ---- Place beds (2D loops) ----
        public static AudioClip DepotHall()
        {
            var r = new System.Random(11); var s = Buffer(8.5f);
            var rumble = Biquad.Low(70); var air = Biquad.Band(420, .8f); var hiss = Biquad.Band(1800, .6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, sway = .75f + .25f * Mathf.Sin(Tau * .25f * t);
                s[i] = rumble.Run(N(r)) * 3 + air.Run(N(r)) * .5f * sway + hiss.Run(N(r)) * .08f
                    + .05f * Mathf.Sin(Tau * 50 * t) + .025f * Mathf.Sin(Tau * 100 * t) + .02f * Mathf.Sin(Tau * 25 * t) * sway;
            }
            return Loop("Цех: вентиляция и гул", s, .7f);
        }
        public static AudioClip City(string name, int seed, float traffic, float crowd)
        {
            var r = new System.Random(seed); var s = Buffer(8.5f);
            var low = Biquad.Low(160); var mid = Biquad.Band(600, .5f); var murmur = Murmur(r, s.Length, 7, 1600);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, swell = .7f + .3f * Mathf.Sin(Tau * .125f * t + seed);
                s[i] = (low.Run(N(r)) * 2.2f + mid.Run(N(r)) * .35f) * traffic * swell + murmur[i] * crowd;
            }
            return Loop(name, s, .7f);
        }

        // ---- Train ----
        public static AudioClip Rolling()
        {
            var r = new System.Random(47); var s = Buffer(4.5f);
            var rumble = Biquad.Low(110); var roar = Biquad.Band(650, .9f); var body = Biquad.Band(230, 1.5f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] = rumble.Run(N(r)) * 2.5f + roar.Run(N(r)) * .35f * (.85f + .15f * Mathf.Sin(Tau * .5f * t)) + body.Run(N(r)) * .4f
                    + .06f * Mathf.Sin(Tau * 58 * t) + .03f * Mathf.Sin(Tau * 116 * t);
            }
            return Loop("Колёсный ход", s, .75f);
        }
        public static AudioClip Airflow()
        {
            var r = new System.Random(5); var s = Buffer(4.5f); var high = Biquad.High(700); var band = Biquad.Band(1400, .7f);
            for (int i = 0; i < s.Length; i++) s[i] = band.Run(high.Run(N(r))) * (.8f + .2f * Mathf.Sin(Tau * .25f * i / Rate));
            return Loop("Обтекание кузова", s, .7f);
        }
        public static AudioClip RailJoint()
        {
            var r = new System.Random(31); var s = Buffer(.4f);
            foreach (float at in new[] { 0f, .055f })   // two axles of one bogie
            {
                var ring = Biquad.Band(1700, 3);
                for (int i = (int)(at * Rate); i < s.Length; i++)
                {
                    float t = (float)i / Rate - at;
                    s[i] += ring.Run(N(r) * Mathf.Exp(-t * 90)) * .8f + Mathf.Sin(Tau * 85 * t) * Mathf.Exp(-t * 28) * .5f;
                }
            }
            FadeTail(s, .05f); return Clip("Стык рельса", s, .8f);
        }

        // ---- Nearby 3D sources (loops) ----
        public static AudioClip Transformer()
        {
            var r = new System.Random(19); var s = Buffer(2.5f); var buzz = Biquad.Low(900); var air = Biquad.Band(3000, 2);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                s[i] = .2f * Mathf.Sin(Tau * 50 * t) + .5f * Mathf.Sin(Tau * 100 * t) + .25f * Mathf.Sin(Tau * 150 * t) + .12f * Mathf.Sin(Tau * 200 * t)
                    + buzz.Run(Mathf.Sign(Mathf.Sin(Tau * 100 * t))) * .08f + air.Run(N(r)) * .05f;
            }
            return Loop("Электрошкаф", s, .7f);
        }
        public static AudioClip WallFan()
        {
            var r = new System.Random(29); var s = Buffer(3.5f); var blade = Biquad.Band(320, .8f); var low = Biquad.Low(150);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, beat = .6f + .4f * (.5f + .5f * Mathf.Sin(Tau * 12.5f * t));
                s[i] = blade.Run(N(r)) * beat + low.Run(N(r)) * 1.5f + .08f * Mathf.Sin(Tau * 100 * t) + .03f * Mathf.Sin(Tau * 300 * t);
            }
            return Loop("Вентилятор цеха", s, .7f);
        }
        public static AudioClip Workbench()
        {
            var r = new System.Random(23); var s = Buffer(8);
            for (int k = 0; k < 9; k++) Click(s, .4f + k * .075f, 2600, .35f, r);   // ratchet
            Metal(s, 2.1f, 880, 14, .6f, r); Metal(s, 2.55f, 880, 14, .5f, r); Metal(s, 3f, 900, 14, .55f, r);
            for (int k = 0; k < 6; k++) Click(s, 4.3f + k * .085f, 2400, .3f, r);
            Metal(s, 5.6f, 430, 8, .45f, r);   // spanner set down
            Reverb(s, .35f, .8f); FadeTail(s, .3f); return Clip("Работа с инструментом", s, .8f);
        }
        public static AudioClip Cafe()
        {
            var r = new System.Random(71); var s = Buffer(7.5f); var steam = Biquad.High(2500); var murmur = Murmur(r, s.Length, 2, 2200);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, e = Mathf.Clamp01((t - 1.2f) * 6) * Mathf.Clamp01((3.4f - t) * 3);
                s[i] = steam.Run(N(r)) * .25f * e + murmur[i] * .6f;
            }
            Metal(s, 4.6f, 2300, 30, .25f, r); Metal(s, 5.3f, 2100, 34, .2f, r);   // cups
            return Loop("Кофейный павильон", s, .7f);
        }
        public static AudioClip Group(int seed)
        {
            var r = new System.Random(seed); var s = Murmur(r, (int)(Rate * 8.5f), 3, 1900);
            return Loop("Разговор группы", s, .6f);
        }

        // ---- One-off events ----
        public static AudioClip DistantClank()
        {
            var r = new System.Random(53); var s = Buffer(2.6f);
            Metal(s, .02f, 310, 6, 1, r); Metal(s, .09f, 523, 9, .5f, r);
            for (int i = 0; i < Rate / 4; i++) s[i] += Mathf.Sin(Tau * 70 * i / Rate) * Mathf.Exp(-20f * i / Rate) * .8f;
            Filter(s, Biquad.Low(1800)); Reverb(s, .9f, .88f, .5f, 1.6f); FadeTail(s, .4f);
            return Clip("Удалённый удар металла", s, .8f);
        }
        public static AudioClip Pneumatic()
        {
            var r = new System.Random(67); var s = Buffer(2.2f); var high = Biquad.High(1800); var band = Biquad.Band(3500, 1.2f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, e = Mathf.Clamp01(t / .03f) * (t < .3f ? 1 : Mathf.Exp(-(t - .3f) * 5));
                s[i] = (high.Run(N(r)) + band.Run(N(r)) * .5f) * e;
            }
            Reverb(s, .6f, .86f, .5f, 1.6f); FadeTail(s, .3f); return Clip("Сброс воздуха", s, .7f);
        }
        public static AudioClip DistantHorn()
        {
            var s = Buffer(3.2f); float phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, e = Mathf.Clamp01(t / .08f) * Mathf.Clamp01((1.25f - t) / .25f), v = 0;
                phase += (t < .55f ? 370 : 311) / (float)Rate; if (phase > 1) phase -= 1;
                for (int h = 1; h <= 5; h++) v += Mathf.Sin(Tau * h * phase) / h;
                s[i] = v * e;
            }
            Filter(s, Biquad.Low(900)); Reverb(s, .9f, .86f, .5f, 1.6f); FadeTail(s, .4f);
            return Clip("Далёкий сигнал поезда", s, .8f);
        }
        // Three rising bell notes before boarding and on arrival.
        public static AudioClip StationChime()
        {
            var s = Buffer(3.4f); float[] notes = { 587.33f, 739.99f, 880f };
            for (int n = 0; n < notes.Length; n++)
                for (int i = (int)(n * .52f * Rate), start = i; i < s.Length; i++)
                {
                    float t = (float)(i - start) / Rate, f = notes[n];
                    s[i] += Mathf.Min(1, t / .006f) * (Mathf.Sin(Tau * f * t) * Mathf.Exp(-t * 1.6f)
                        + .35f * Mathf.Sin(Tau * f * 2 * t) * Mathf.Exp(-t * 2.6f) + .12f * Mathf.Sin(Tau * f * 3.01f * t) * Mathf.Exp(-t * 4));
                }
            Reverb(s, .25f, .75f); FadeTail(s, .25f); return Clip("Вокзальный сигнал", s, .8f);
        }
        public static AudioClip TaskCue()
        {
            var s = Buffer(.5f); float phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate; phase += (t < .2f ? 660 : 880) / (float)Rate; if (phase > 1) phase -= 1;
                s[i] = Mathf.Sin(Tau * phase) * Mathf.Sin(Mathf.PI * t * 2);
            }
            return Clip("Сигнал рабочего задания", s, .45f);
        }

        // Heel and toe contact through a surface-specific band; variants differ by seed and tuning.
        public static AudioClip Footstep(Surface surface, int variant)
        {
            var r = new System.Random(100 + (int)surface * 10 + variant); float v = (float)r.NextDouble();
            var s = Buffer(surface == Surface.Concrete ? .55f : .24f);
            (float heel, float band, float q, float grit, float decay, float thump) = surface switch
            {
                Surface.Concrete => (95f, 900f, 1.2f, .5f, 45f, .5f),
                Surface.Asphalt => (80f, 550f, .9f, .7f, 38f, .45f),
                Surface.Stone => (140f, 2600f, 2.5f, .35f, 70f, .3f),
                Surface.CarFloor => (120f, 420f, 1.5f, .25f, 30f, .8f),
                _ => (70f, 1400f, .7f, 1f, 22f, .3f)
            };
            var tone = Biquad.Band(band * (.9f + v * .2f), q); float roll = .018f + v * .012f;   // heel-to-toe gap
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate, toe = t - roll;
                float e = Mathf.Exp(-t * decay) + (toe > 0 ? .6f * Mathf.Exp(-toe * decay * 1.2f) : 0);
                float grain = surface == Surface.Gravel ? (r.NextDouble() < .02 ? N(r) * 3 : 0) + N(r) * .3f : N(r);
                s[i] = tone.Run(grain) * e * grit * 3 + Mathf.Sin(Tau * heel * (1 + v * .1f) * t) * Mathf.Exp(-t * decay * .8f) * thump;
            }
            if (surface == Surface.Concrete) Reverb(s, .22f, .78f);   // high depot hall
            if (surface == Surface.CarFloor) Filter(s, Biquad.Low(1200));
            FadeTail(s, .03f);
            return Clip("Шаг · " + surface + " " + variant, s, .8f);
        }
    }
}
