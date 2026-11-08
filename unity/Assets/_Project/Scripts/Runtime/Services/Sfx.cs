using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Ses efektleri (müzik yok): Resources/Audio altındaki kısa sesler, kanal havuzundan çalınır. Sık çalan seslerin
    /// perdesi her seferinde biraz değişir (tekrar hissi olmasın) ve aynı sesin üst üste çalması sınırlanır. Ayarlardaki
    /// Sound anahtarı (afj_set_sound) kapalıysa hiçbir ses çalmaz.
    /// </summary>
    public static class Sfx
    {
        public const string BlockPick = "block_pick", BlockDrop = "block_drop", GateExit = "gate_exit", Wrong = "wrong",
            RocketLaunch = "rocket_launch", PixelPop = "pixel_pop", SlotLand = "slot_land", Button = "button",
            LevelWin = "level_win", LevelFail = "level_fail", IceBreak = "ice_break", Unlock = "unlock",
            AsteroidBreak = "asteroid_break", SpeedUp = "speed_up", Booster = "booster", Freeze = "freeze", Ufo = "ufo",
            Warp = "warp", TimerWarning = "timer_warning", Coin = "coin", LockBreak = "lock_break", Fireworks = "fireworks";
        const int Channels = 12;

        // Ses başına: perde oynaması (±), aynı sesin iki çalışı arası en az süre (sn), aynı anda en çok kaç kopya.
        static readonly Dictionary<string, (float pitch, float gap, int max)> Tuning = new()
        {
            { PixelPop, (0.12f, 0.035f, 4) },
            { RocketLaunch, (0.08f, 0.04f, 4) },
            { SlotLand, (0.06f, 0.05f, 2) },
            { BlockPick, (0.05f, 0.05f, 1) },
            { BlockDrop, (0.05f, 0.05f, 1) },
            { Button, (0.03f, 0.04f, 2) },
            { Wrong, (0f, 0.25f, 1) },
            { Fireworks, (0.15f, 0.08f, 4) },
            { IceBreak, (0.05f, 0.05f, 2) },
            { AsteroidBreak, (0.05f, 0.05f, 2) },
        };

        // Dosyası diğerlerine göre yüksek kalan seslerin kısılma çarpanı.
        static readonly Dictionary<string, float> Loudness = new()
        {
            { Fireworks, 0.35f },
            { BlockDrop, 0.6f },
        };

        static readonly Dictionary<string, AudioClip> clips = new();
        static readonly Dictionary<string, float> lastPlayed = new();
        static AudioSource[] sources;
        static int next;

        public static bool Enabled => GameSave.GetInt(SaveKeys.Sound, 1) == 1;

        public static void Play(string name, float volume = 1f)
        {
            if (!Enabled) return;
            var clip = Clip(name);
            if (!clip) return;
            Tuning.TryGetValue(name, out var t);
            var now = Time.unscaledTime;
            if (t.gap > 0f && lastPlayed.TryGetValue(name, out var last) && now - last < t.gap) return;
            if (t.max > 0 && Playing(clip) >= t.max) return;
            lastPlayed[name] = now;
            var src = Source();
            src.clip = clip;
            src.volume = volume * (Loudness.TryGetValue(name, out var loudness) ? loudness : 1f);
            src.pitch = 1f + (t.pitch > 0f ? Random.Range(-t.pitch, t.pitch) : 0f);
            src.Play();
        }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            return clips[name] = Resources.Load<AudioClip>("Audio/" + name);
        }

        static int Playing(AudioClip clip)
        {
            if (sources == null) return 0;
            var n = 0;
            foreach (var s in sources) if (s.isPlaying && s.clip == clip) n++;
            return n;
        }

        // Boş kanal; yoksa sırayla en eskisi (havuz sahneler arası yaşar).
        static AudioSource Source()
        {
            if (sources == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                sources = new AudioSource[Channels];
                for (var i = 0; i < Channels; i++)
                {
                    sources[i] = go.AddComponent<AudioSource>();
                    sources[i].playOnAwake = false;
                    sources[i].spatialBlend = 0f;
                    sources[i].ignoreListenerPause = true;   // duraklatmada da buton sesi çalsın
                }
            }
            foreach (var s in sources) if (!s.isPlaying) return s;
            next = (next + 1) % Channels;
            return sources[next];
        }
    }
}
