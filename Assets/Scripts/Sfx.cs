using UnityEngine;

namespace LangGio
{
    public enum SfxId { Pop, Place, Delete, Undo, Redo, Chime, Click, Splash, Shutter, Bird, Cricket, Step }

    /// <summary>
    /// Âm thanh tự tổng hợp lúc chạy (không cần file âm thanh): tiếng "pốp" khi vật mọc lên, đặt / xoá,
    /// hoàn tác, chuông khám phá, click; âm nền gió + chim (ngày) + dế (đêm).
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        const int Rate = 44100;

        AudioClip[] clips;
        AudioSource[] voices;
        int nextVoice;
        AudioSource wind, rain;
        float lastPop, birdTimer = 3f, cricketTimer = 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => I = null;

        void Awake()
        {
            I = this;
            clips = new AudioClip[System.Enum.GetValues(typeof(SfxId)).Length];
            clips[(int)SfxId.Pop] = Make("pop", 0.11f, Pop);
            clips[(int)SfxId.Place] = Make("place", 0.2f, Place);
            clips[(int)SfxId.Delete] = Make("delete", 0.24f, Delete);
            clips[(int)SfxId.Undo] = Make("undo", 0.2f, t => TwoNotes(t, 659f, 494f));
            clips[(int)SfxId.Redo] = Make("redo", 0.2f, t => TwoNotes(t, 494f, 659f));
            clips[(int)SfxId.Chime] = Make("chime", 1.6f, Chime);
            clips[(int)SfxId.Click] = Make("click", 0.04f, Click);
            clips[(int)SfxId.Splash] = Make("splash", 0.4f, Splash);
            clips[(int)SfxId.Shutter] = Make("shutter", 0.16f, Shutter);
            clips[(int)SfxId.Bird] = Make("bird", 0.5f, Bird);
            clips[(int)SfxId.Cricket] = Make("cricket", 0.45f, Cricket);
            clips[(int)SfxId.Step] = Make("step", 0.09f, Step);

            voices = new AudioSource[10];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }

            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = WindLoop();
            wind.loop = true;
            wind.volume = 0f;
            wind.Play();

            rain = gameObject.AddComponent<AudioSource>();
            rain.clip = RainLoop();
            rain.loop = true;
            rain.volume = 0f;
            rain.Play();
        }

        /// <summary>Phát 1 tiếng. pan: −1 trái … 1 phải.</summary>
        public static void Play(SfxId id, float pitch = 1f, float volume = 1f, float pan = 0f)
        {
            if (!I) return;
            float vol = volume * Settings.SfxVolume;
            if (id == SfxId.Bird || id == SfxId.Cricket) vol = volume * Settings.AmbVolume;
            if (vol <= 0.001f) return;
            var v = I.voices[I.nextVoice];
            I.nextVoice = (I.nextVoice + 1) % I.voices.Length;
            v.pitch = pitch;
            v.panStereo = pan;
            v.PlayOneShot(I.clips[(int)id], vol);
        }

        /// <summary>Tiếng "pốp" (giới hạn để 1 nét tường dài không thành tràng tiếng ồn).</summary>
        public static void Pop()
        {
            if (!I || Time.unscaledTime - I.lastPop < 0.05f) return;
            I.lastPop = Time.unscaledTime;
            Play(SfxId.Pop, Random.Range(0.85f, 1.25f), 0.45f);
        }

        void Update()
        {
            bool menu = GameUI.Screen == UiScreen.Main;
            float amb = Settings.AmbVolume;
            float target = amb * (menu ? 0.25f : 0.18f) * (DayCycle.IsNight ? 0.7f : 1f);
            float r = Seasons.Rain * (Settings.Season == 3 ? 0.25f : 1f);   // tuyết rơi: chỉ còn gió nhẹ
            wind.volume = Mathf.MoveTowards(wind.volume, target * (1f + r * 0.8f) * (Settings.Season == 3 ? 1.5f : 1f), Time.unscaledDeltaTime * 0.2f);   // đông: gió rít hơn
            rain.volume = Mathf.MoveTowards(rain.volume, amb * 0.38f * r * (menu ? 0.5f : 1f), Time.unscaledDeltaTime * 0.3f);
            if (amb <= 0.001f) return;

            if (!DayCycle.IsNight && Seasons.Rain < 0.3f && Settings.Season != 3)
            {
                birdTimer -= Time.deltaTime;
                if (birdTimer <= 0f)
                {
                    birdTimer = Random.Range(2.5f, 9f);
                    Play(SfxId.Bird, Random.Range(0.85f, 1.2f), Random.Range(0.12f, 0.25f), Random.Range(-0.8f, 0.8f));
                }
            }
            if (DayCycle.IsEvening)
            {
                cricketTimer -= Time.deltaTime;
                if (cricketTimer <= 0f)
                {
                    cricketTimer = Random.Range(0.8f, 2.5f);
                    Play(SfxId.Cricket, Random.Range(0.95f, 1.05f), DayCycle.IsNight ? 0.12f : 0.05f, Random.Range(-0.9f, 0.9f));
                }
            }
        }

        // ================= Tổng hợp âm =================
        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            // làm mềm 3 ms đầu / cuối để không lách tách
            int fade = Mathf.Min(n / 4, Rate * 3 / 1000);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[n - 1 - i] *= k;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        const float TAU = Mathf.PI * 2f;
        static float Sin(float hz, float t) => Mathf.Sin(TAU * hz * t);
        static readonly System.Random noiseRng = new System.Random(1234);
        static float Noise() => (float)(noiseRng.NextDouble() * 2.0 - 1.0);

        static float Pop(float t)
        {
            // quét tần số lên nhanh + tắt dần
            float f = Mathf.Lerp(420f, 900f, Mathf.Clamp01(t / 0.05f));
            return Mathf.Sin(TAU * f * t + 2f * Mathf.Sin(TAU * f * 0.5f * t) * Mathf.Exp(-t * 40f)) * Mathf.Exp(-t * 32f) * 0.8f;
        }

        static float Place(float t)
        {
            float f = Mathf.Lerp(150f, 85f, t / 0.2f);
            return (Sin(f, t) * 0.8f + Noise() * 0.25f * Mathf.Exp(-t * 90f)) * Mathf.Exp(-t * 18f);
        }

        static float lp;
        static float Delete(float t)
        {
            lp += (Noise() - lp) * 0.15f;   // nhiễu lọc thấp
            float f = Mathf.Lerp(520f, 180f, t / 0.24f);
            return (lp * 0.7f + Sin(f, t) * 0.35f) * Mathf.Exp(-t * 12f);
        }

        static float TwoNotes(float t, float a, float b)
        {
            float half = 0.09f;
            float f = t < half ? a : b, tt = t < half ? t : t - half;
            float tri = Mathf.PingPong(f * tt * 2f, 1f) * 2f - 1f;
            return tri * 0.35f * Mathf.Exp(-tt * 22f);
        }

        static float Chime(float t)
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float tt = t - i * 0.09f;
                if (tt < 0f) continue;
                float env = Mathf.Exp(-tt * 3.2f);
                s += (Sin(notes[i], tt) + 0.3f * Sin(notes[i] * 2.01f, tt)) * env * 0.22f;
            }
            return s;
        }

        static float Click(float t) => (Sin(1800f, t) * 0.5f + Noise() * 0.3f) * Mathf.Exp(-t * 160f);

        static float sp;
        static float Splash(float t)
        {
            sp += (Noise() - sp) * 0.3f;
            float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 9f);
            return sp * env * 0.8f + Sin(Mathf.Lerp(700f, 300f, t / 0.4f), t) * 0.1f * env;
        }

        static float Shutter(float t)
        {
            float a = t < 0.02f ? Noise() * Mathf.Exp(-t * 300f) : 0f;
            float tt = t - 0.08f;
            float b = tt >= 0f && tt < 0.03f ? Noise() * Mathf.Exp(-tt * 220f) : 0f;
            return (a + b) * 0.7f;
        }

        static float Bird(float t)
        {
            // 3 tiếng hót ngắn
            float s = 0f;
            for (int i = 0; i < 3; i++)
            {
                float tt = t - i * 0.14f;
                if (tt < 0f || tt > 0.1f) continue;
                float k = tt / 0.1f;
                float f = 2600f + 1400f * Mathf.Sin(k * Mathf.PI) - i * 150f;
                s += Mathf.Sin(TAU * f * tt) * Mathf.Sin(k * Mathf.PI) * 0.5f;
            }
            return s;
        }

        static float Cricket(float t)
        {
            // các xung 4.6 kHz, 30 lần/giây, 2 cụm
            float burst = (t % 0.22f) < 0.12f ? 1f : 0f;
            float pulse = Mathf.Max(0f, Mathf.Sin(TAU * 30f * t));
            return Sin(4600f, t) * pulse * burst * 0.5f;
        }

        static float stLp;
        static float Step(float t)
        {
            stLp += (Noise() - stLp) * 0.25f;
            float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * 42f);
            float thud = Sin(Mathf.Lerp(160f, 65f, t / 0.09f), t) * Mathf.Exp(-t * 55f);
            return (stLp * 0.65f + thud * 0.55f) * env;
        }

        /// <summary>Mưa: nhiễu hồng dày + tiếng giọt lách tách ngẫu nhiên, vòng lặp 5 s.</summary>
        static AudioClip RainLoop()
        {
            int n = Rate * 5;
            var d = new float[n];
            var rng = new System.Random(311);
            float b0 = 0, b1 = 0, b2 = 0, hp = 0, prev = 0;
            for (int i = 0; i < n; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                b0 = 0.997f * b0 + w * 0.029f; b1 = 0.985f * b1 + w * 0.032f; b2 = 0.95f * b2 + w * 0.048f;
                float pink = b0 + b1 + b2 + w * 0.02f;
                hp = 0.92f * (hp + pink - prev); prev = pink;     // cắt trầm cho giống mưa rào
                d[i] = hp * 0.8f;
            }
            // giọt lách tách
            for (int k = 0; k < 900; k++)
            {
                int at = rng.Next(n - 800);
                float amp = (float)rng.NextDouble() * 0.25f + 0.05f;
                float hz = 2500f + (float)rng.NextDouble() * 3500f;
                for (int i = 0; i < 700; i++)
                {
                    float t = i / (float)Rate;
                    d[at + i] += Mathf.Sin(TAU * hz * t) * Mathf.Exp(-t * 900f) * amp;
                }
            }
            int x = Rate / 2;
            for (int i = 0; i < x; i++)
            {
                float k = i / (float)x;
                d[i] = d[i] * k + d[n - x + i] * (1f - k);
            }
            var loop = new float[n - x];
            System.Array.Copy(d, loop, n - x);
            float max = 0.0001f;
            foreach (var v in loop) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < loop.Length; i++) loop[i] = loop[i] / max * 0.7f;
            var clip = AudioClip.Create("rain", loop.Length, 1, Rate, false);
            clip.SetData(loop, 0);
            return clip;
        }

        static AudioClip WindLoop()
        {
            int n = Rate * 6;
            var d = new float[n];
            float b = 0f;
            var rng = new System.Random(77);
            for (int i = 0; i < n; i++)
            {
                b = b * 0.996f + (float)(rng.NextDouble() * 2.0 - 1.0) * 0.04f;   // nhiễu nâu
                float t = i / (float)Rate;
                float gust = 0.6f + 0.4f * Mathf.Sin(TAU * t / 6f) * Mathf.Sin(TAU * t / 2f + 1f);
                d[i] = b * gust;
            }
            // nối vòng: trộn 0.5 s cuối vào đầu
            int x = Rate / 2;
            for (int i = 0; i < x; i++)
            {
                float k = i / (float)x;
                d[i] = d[i] * k + d[n - x + i] * (1f - k);
            }
            var loop = new float[n - x];
            System.Array.Copy(d, loop, n - x);
            float max = 0.0001f;
            foreach (var v in loop) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < loop.Length; i++) loop[i] = loop[i] / max * 0.6f;
            var clip = AudioClip.Create("wind", loop.Length, 1, Rate, false);
            clip.SetData(loop, 0);
            return clip;
        }
    }
}
