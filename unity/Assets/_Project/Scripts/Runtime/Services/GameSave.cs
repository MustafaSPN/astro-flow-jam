using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Oyuncu kaydına tek erişim noktası. Okumalar bellekte önbelleklenir: iOS'ta PlayerPrefs her okumada yerel koda iner ve
    /// bazı değerler her karede sorulur. Depolama değişirse (ör. bulut kaydı) yalnız burası değişir.
    /// </summary>
    public static class GameSave
    {
        static readonly Dictionary<string, int> Ints = new();
        static readonly Dictionary<string, string> Strings = new();
        static readonly HashSet<string> Missing = new();

        public static int GetInt(string key, int fallback = 0)
        {
            if (Ints.TryGetValue(key, out var value)) return value;
            if (Missing.Contains(key)) return fallback;
            if (!PlayerPrefs.HasKey(key)) { Missing.Add(key); return fallback; }
            return Ints[key] = PlayerPrefs.GetInt(key);
        }

        public static void SetInt(string key, int value)
        {
            Ints[key] = value;
            Missing.Remove(key);
            PlayerPrefs.SetInt(key, value);
        }

        public static string GetString(string key, string fallback)
        {
            if (Strings.TryGetValue(key, out var value)) return value;
            if (Missing.Contains(key)) return fallback;
            if (!PlayerPrefs.HasKey(key)) { Missing.Add(key); return fallback; }
            return Strings[key] = PlayerPrefs.GetString(key);
        }

        public static void SetString(string key, string value)
        {
            Strings[key] = value;
            Missing.Remove(key);
            PlayerPrefs.SetString(key, value);
        }

        public static void DeleteKey(string key)
        {
            Forget(key);
            PlayerPrefs.DeleteKey(key);
        }

        public static void DeleteAll()
        {
            ForgetAll();
            PlayerPrefs.DeleteAll();
        }

        public static void Save() => PlayerPrefs.Save();

        // Editörde alan yeniden yükleme kapalıyken Play modları arasında statik önbellek taşınmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetAll()
        {
            Ints.Clear();
            Strings.Clear();
            Missing.Clear();
        }

        static void Forget(string key)
        {
            Ints.Remove(key);
            Strings.Remove(key);
            Missing.Remove(key);
        }
    }
}
