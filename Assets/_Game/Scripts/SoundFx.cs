using System.Collections.Generic;
using UnityEngine;
namespace MyFootballCareer
{
    public enum Sfx { Click, UpgradeSuccess, UpgradeGreat, UpgradeFail, Buy, Whistle, FullTime, Goal, Conceded, Assist, Tackle, Trophy, Tick, Collect }

    // 효과음. 소리 파일 없이 코드로 8비트 느낌의 소리를 만들어 한 번만 저장해 두고 재생합니다.
    // 소리 켜기/끄기는 이 기기에만 저장합니다 (PlayerPrefs).
    public static class SoundFx
    {
        const int Rate = 22050;
        const float PitchScale = 0.667f;   // 모든 소리의 음 높이 배율 (0.667 = 완전5도 낮게). 화음 간격은 그대로
        const string PrefKey = "gof_sound_on";
        static AudioSource source;
        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static bool? enabled;
        static float lastTickTime = -1f;

        public static bool Enabled
        {
            get
            {
                if (enabled == null) { try { enabled = PlayerPrefs.GetInt(PrefKey, 1) == 1; } catch { enabled = true; } }
                return enabled.Value;
            }
            set
            {
                enabled = value;
                try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); } catch { }
            }
        }

        public static void Play(Sfx kind, float volume = 1f)
        {
            if (!Enabled) return;
            if (kind == Sfx.Tick)   // 숫자가 올라가는 소리는 너무 촘촘하지 않게
            {
                if (Time.unscaledTime - lastTickTime < 0.05f) return;
                lastTickTime = Time.unscaledTime;
            }
            if (source == null)
            {
                var go = new GameObject("SoundFx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (!clips.TryGetValue(kind, out var clip) || clip == null) clips[kind] = clip = Build(kind);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ---------- 소리 만들기 ----------
        enum Wave { Square, Triangle, Sine, Saw, Noise }

        class Synth
        {
            public readonly List<float> data = new List<float>();
            readonly System.Random rng = new System.Random(7);

            // 지금 끝 위치부터 소리 한 조각을 덧붙입니다. (start 초 지점부터 겹쳐 쓰려면 Mix)
            public void Add(float f0, float f1, float seconds, Wave wave, float vol, float attack = 0.005f, float release = 0.06f)
            { Mix(data.Count / (float)Rate, f0, f1, seconds, wave, vol, attack, release); }

            public void Mix(float start, float f0, float f1, float seconds, Wave wave, float vol, float attack = 0.005f, float release = 0.06f)
            {
                int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(seconds * Rate);
                while (data.Count < s0 + n) data.Add(0f);
                double phase = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float k = n > 1 ? i / (float)(n - 1) : 0f;
                    float f = Mathf.Lerp(f0, f1, k) * PitchScale;
                    phase += f / Rate;
                    float p = (float)(phase - System.Math.Floor(phase));
                    float v;
                    switch (wave)
                    {
                        case Wave.Square: v = p < 0.5f ? 1f : -1f; break;
                        case Wave.Triangle: v = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                        case Wave.Saw: v = 2f * p - 1f; break;
                        case Wave.Noise: v = (float)(rng.NextDouble() * 2 - 1); break;
                        default: v = Mathf.Sin(p * Mathf.PI * 2f); break;
                    }
                    float env = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack)) * Mathf.Min(1f, (seconds - t) / Mathf.Max(0.0005f, release));
                    data[s0 + i] += v * vol * Mathf.Clamp01(env);
                }
            }

            public void Rest(float seconds) { int n = Mathf.RoundToInt(seconds * Rate); for (int i = 0; i < n; i++) data.Add(0f); }
        }

        static float Note(int semitonesFromA4) { return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f); }
        // 자주 쓰는 음 (A4 기준 반음 수)
        static readonly float C5 = Note(3), E5 = Note(7), G5 = Note(10), C6 = Note(15), E6 = Note(19), G6 = Note(22), C7 = Note(27), B5 = Note(14), A5 = Note(12), D5 = Note(5), F5 = Note(8);

        static AudioClip Build(Sfx kind)
        {
            var s = new Synth();
            switch (kind)
            {
                case Sfx.Click:
                    s.Add(1400f, 1100f, 0.035f, Wave.Square, 0.18f, 0.002f, 0.02f); break;
                case Sfx.UpgradeSuccess:
                    s.Add(C6, C6, 0.06f, Wave.Square, 0.22f); s.Add(G6, G6, 0.11f, Wave.Square, 0.22f, 0.003f, 0.08f); break;
                case Sfx.UpgradeGreat:
                    s.Add(C6, C6, 0.06f, Wave.Square, 0.22f); s.Add(E6, E6, 0.06f, Wave.Square, 0.22f);
                    s.Add(G6, G6, 0.06f, Wave.Square, 0.22f); s.Add(C7, C7, 0.22f, Wave.Square, 0.24f, 0.003f, 0.18f);
                    s.Mix(0.18f, C7 * 1.5f, C7 * 2f, 0.25f, Wave.Triangle, 0.10f, 0.01f, 0.2f);   // 반짝임
                    break;
                case Sfx.UpgradeFail:
                    s.Add(330f, 160f, 0.24f, Wave.Saw, 0.16f, 0.005f, 0.12f); break;
                case Sfx.Buy:
                case Sfx.Collect:
                    s.Add(B5, B5, 0.07f, Wave.Square, 0.2f); s.Add(E6, E6, kind == Sfx.Collect ? 0.22f : 0.15f, Wave.Square, 0.2f, 0.003f, 0.12f); break;
                case Sfx.Tick:
                    s.Add(1800f, 1800f, 0.018f, Wave.Square, 0.08f, 0.001f, 0.01f); break;
                case Sfx.Whistle:
                    for (int i = 0; i < 2; i++) { s.Add(2600f, 2700f, i == 0 ? 0.12f : 0.32f, Wave.Sine, 0.22f, 0.01f, 0.05f); s.Rest(0.05f); }
                    break;
                case Sfx.FullTime:
                    for (int i = 0; i < 3; i++) { s.Add(2600f, 2650f, i == 2 ? 0.45f : 0.14f, Wave.Sine, 0.22f, 0.01f, 0.05f); s.Rest(0.06f); }
                    break;
                case Sfx.Goal:
                    s.Mix(0f, 0f, 0f, 1.1f, Wave.Noise, 0.10f, 0.15f, 0.6f);   // 관중 함성
                    s.Mix(0.02f, C5, C5, 0.12f, Wave.Square, 0.2f); s.Mix(0.14f, E5, E5, 0.12f, Wave.Square, 0.2f);
                    s.Mix(0.26f, G5, G5, 0.12f, Wave.Square, 0.2f); s.Mix(0.38f, C6, C6, 0.4f, Wave.Square, 0.22f, 0.005f, 0.3f);
                    break;
                case Sfx.Conceded:
                    s.Add(E5, E5, 0.14f, Wave.Triangle, 0.25f); s.Add(C5 * 0.75f, C5 * 0.75f, 0.3f, Wave.Triangle, 0.25f, 0.005f, 0.2f); break;
                case Sfx.Assist:
                    s.Add(G5, G5, 0.06f, Wave.Square, 0.2f); s.Add(D5 * 2f, D5 * 2f, 0.14f, Wave.Square, 0.2f, 0.003f, 0.1f); break;
                case Sfx.Tackle:
                    s.Add(180f, 90f, 0.09f, Wave.Noise, 0.25f, 0.002f, 0.06f); s.Add(A5, A5, 0.07f, Wave.Square, 0.14f); break;
                case Sfx.Trophy:
                    // 팡파레: 솔-도-미-솔(길게) + 아래 화음
                    s.Add(G5, G5, 0.12f, Wave.Square, 0.2f); s.Add(C6, C6, 0.12f, Wave.Square, 0.2f);
                    s.Add(E6, E6, 0.12f, Wave.Square, 0.2f); s.Add(G6, G6, 0.55f, Wave.Square, 0.22f, 0.005f, 0.4f);
                    s.Mix(0.36f, C5, C5, 0.55f, Wave.Triangle, 0.2f, 0.01f, 0.4f);
                    s.Mix(0.36f, E5, E5, 0.55f, Wave.Triangle, 0.14f, 0.01f, 0.4f);
                    s.Mix(0.5f, C7 * 1.5f, C7 * 2f, 0.3f, Wave.Triangle, 0.08f, 0.01f, 0.25f);
                    break;
            }
            var arr = s.data.ToArray();
            for (int i = 0; i < arr.Length; i++) arr[i] = Mathf.Clamp(arr[i], -1f, 1f);
            var clip = AudioClip.Create("sfx_" + kind, Mathf.Max(1, arr.Length), 1, Rate, false);
            clip.SetData(arr, 0);
            return clip;
        }
    }
}
