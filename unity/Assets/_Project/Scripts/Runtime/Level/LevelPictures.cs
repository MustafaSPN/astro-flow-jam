using System.Collections.Generic;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>
    /// Gizli resimler yalnız gerektiğinde yüklenir (Resources/LevelPictures): bölüm verileri resme doğrudan bağlansaydı hepsi
    /// oyun açılışında belleğe girerdi. Aynı resmi birden çok yer kullanabilir; son kullanıcı bırakınca bellekten çıkar.
    /// </summary>
    public static class LevelPictures
    {
        const string Folder = "LevelPictures/";

        sealed class Entry
        {
            public Texture2D texture;
            public int users;
        }

        static readonly Dictionary<string, Entry> Loaded = new();

        public static Texture2D Acquire(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!Loaded.TryGetValue(name, out var entry))
            {
                var texture = Resources.Load<Texture2D>(Folder + name);
                if (!texture) return null;
                entry = Loaded[name] = new Entry { texture = texture };
            }
            entry.users++;
            return entry.texture;
        }

        public static void Release(string name)
        {
            if (string.IsNullOrEmpty(name) || !Loaded.TryGetValue(name, out var entry)) return;
            if (--entry.users > 0) return;
            Loaded.Remove(name);
            Resources.UnloadAsset(entry.texture);
        }
    }
}
