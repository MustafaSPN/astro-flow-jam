using System.Collections.Generic;
using AstroFlowJam.Core;
using UnityEngine;

namespace AstroFlowJam
{
    /// <summary>Oyunun 3D model prefabları (Art/Models altındaki FBX'ler). Editörde Astro Flow Jam/Modelleri Bağla ile kurulur.</summary>
    [CreateAssetMenu(menuName = "Astro Flow Jam/Model Library", fileName = "ModelLibrary")]
    public sealed class ModelLibrary : ScriptableObject
    {
        public GameObject blockConnector; // AFJ_Block_Connector_Seamless: iki hücre arası bağlantı
        public GameObject block;          // AFJ_Final_Block_1x1: tek hücre + dört yönde bağlantı parçası
        public GameObject rocket;         // AFJ_Rounded_Rocket_Parts: gövde, burun, kanatlar, motor, arma
        public GameObject gate;           // AFJ_Rounded_Gate_3Cells: 3 hücrelik sürgülü kapı
        public GameObject floorTile;      // AFJ_Floor_Tile_1x1
        public GameObject wallStraight;   // AFJ_Rounded_Wall_1Unit: yarım yuvarlak profilli duvar
        public GameObject wallCorner;     // AFJ_Rounded_Wall_Corner: yuvarlak dirsek
        public GameObject orbit;          // AFJ_Orbit_With_Depot: yörünge, kartuş deposu ve fırlatma rampası
        public GameObject rewardDoor;     // AFJ_Simple_Reward_Door: gizli resmin önündeki iki kapaklı sürgülü kapı ve kilit
        public GameObject padlock;        // AFJ_Space_Padlock: ödül kapısının ortasındaki uzay kilidi
        public GameObject pixel;          // AFJ_Pixel_1x1: resmin üstündeki piksel (boncuk)
        public GameObject cartridge;      // AFJ_Energy_Cartridge: rokete takılan enerji kartuşu (yörünge kapasitesinin bir birimi)
        [Header("Blok özellikleri (tahta mekanikleri)")]
        public GameObject arrowShaft;     // AFJ_Arrow_Shaft: tek eksen okunun gövdesi (yatay, ortası pivot; boyu ölçekle uzar)
        public GameObject arrowHeadLeft;  // AFJ_Arrow_Head_Left: sol uç (tabanı pivotta)
        public GameObject arrowHeadRight; // AFJ_Arrow_Head_Right: sağ uç (tabanı pivotta)
        public GameObject keyEmblem;      // AFJ_Key_Emblem: anahtarlı blok
        public GameObject lockHub;        // AFJ_Lock_Hub: kilidin merkez göbeği
        public GameObject lockArm;        // AFJ_Lock_Arm_1Unit: +X yönünde 1 birimlik kayış (pivot başlangıçta)
        public GameObject lockAnchor;     // AFJ_Lock_Anchor: kayış ucu bağlantısı
        public GameObject lock1x1;        // AFJ_Lock_1x1: tek hücrelik çapraz dört kollu hazır kilit
        [Header("Jokerler")]
        public GameObject ufo;            // UfoBooster/AFJ_UFO: UFO jokerinin uçan dairesi (üçgenleştirilmiş)

        [Header("Asteroit engelleri (AsteroidObstacles, pivot (0,0) hücresi, taban z=0)")]
        public GameObject asteroid1x1;
        public GameObject asteroid1x2;
        public GameObject asteroidL;
        public GameObject asteroidT;

        public GameObject Asteroid(Core.AsteroidShape shape) => shape switch
        {
            Core.AsteroidShape.Single => asteroid1x1,
            Core.AsteroidShape.Double => asteroid1x2,
            Core.AsteroidShape.L => asteroidL,
            _ => asteroidT,
        };
        public Material flameMaterial;    // roket alevi parçacıkları (URP Particles/Unlit, toplamalı)
        public Material neonMaterial;     // yörünge ışıkları ve neon cisimler (URP Unlit)
    }

    /// <summary>
    /// FBX modellerini sahneye yerleştirir. Modeller Blender'da Z yukarı hazırlandı ve Unity'ye (−x, z, −y) eksenleriyle gelir;
    /// tahta XY düzleminde ve kameraya doğru yön −Z olduğu için her model bir sarmalayıcıyla Blender (x, y, z) → sahne
    /// (x, y, −z) olacak şekilde döndürülür. Malzemeler kesme düzlemi destekleyen shader'la yeniden kurulur: yalnızca pembe
    /// (mercan) bölgeler oyun rengine boyanır, krem ve koyu detaylar sabit kalır.
    /// </summary>
    public static class ModelKit
    {
        /// <summary>Blender'daki zemin seviyesi (z = −0,03) blok tabanıyla (z = −0,26) aynı yere gelsin diye zemin, duvar ve kapı bu kadar geride durur.</summary>
        public const float GroundOffset = 0.23f;

        public static readonly Quaternion BlenderToScene = Quaternion.LookRotation(Vector3.down, Vector3.back);

        /// <summary>Oyun rengine boyanan (pembe/mercan) malzemeler.</summary>
        static readonly HashSet<string> Tintable = new() { "AFJ Coral", "Gate_Color_Changeable", "Gate_Light_Changeable" };
        const string LightMaterial = "Gate_Light_Changeable";
        const float LightBoost = 0.35f;   // ışık şeritleri rengin açık tonu

        static readonly Dictionary<(string, Color), Material> Cache = new();
        static Shader shader;

        public static void Init(Shader clippableShader) => shader = clippableShader;

        /// <summary>
        /// Modeli 'parent' altına yerleştirir. Boyanan renderer'lar ve malzemeleri 'tinted' listesine eklenir; 'unique' true
        /// ise boyanan malzemeler bu örneğe özeldir (kapının parlaması gibi tek tek değişen renkler için).
        /// </summary>
        public static Transform Spawn(GameObject prefab, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Color tint, List<Renderer> tinted = null, bool unique = false, List<Material> uniqueMaterials = null)
        {
            var wrapper = new GameObject(prefab.name).transform;
            wrapper.SetParent(parent, false);
            wrapper.localPosition = localPosition;
            wrapper.localRotation = localRotation * BlenderToScene;
            var instance = Object.Instantiate(prefab, wrapper, false);
            instance.name = prefab.name;
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = r.sharedMaterials;
                var anyTint = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var name = mats[i].name.Replace(" (Instance)", "");
                    if (Tintable.Contains(name))
                    {
                        var color = name == LightMaterial ? Color.Lerp(tint, Color.white, LightBoost) : tint;
                        if (unique)
                        {
                            mats[i] = Make(name, color);
                            uniqueMaterials?.Add(mats[i]);
                        }
                        else mats[i] = Get(name, color);
                        anyTint = true;
                    }
                    else mats[i] = Get(name, BaseColor(mats[i]));
                }
                r.sharedMaterials = mats;
                if (anyTint) tinted?.Add(r);
            }
            return wrapper;
        }

        /// <summary>
        /// Model içindeki adı verilen alt nesne (derinlemesine arar). Blender'ın kopyalara eklediği ".001" gibi ekler de
        /// kabul edilir; tam ad önceliklidir.
        /// </summary>
        /// <summary>Blokların oyun rengine boyanan malzemesi (aynı renkteki bloklar ve kapılar paylaşır).</summary>
        public static Material BlockMaterial(Color tint) => Get("AFJ Coral", tint);

        /// <summary>Resim piksellerinin malzemesi: blokla aynı görünüm, ayrı parlama grubu (UFO seçimi).</summary>
        public static Material PixelMaterial(Color tint) => Get(PixelMaterialName, tint);

        /// <summary>Kapı kapaklarının malzemesi: blokla aynı görünüm, joker seçiminde parlamaz.</summary>
        public static Material GateMaterial(Color tint) => Get(GateMaterialName, tint);

        const string BlockMaterialName = "AFJ Coral", PixelMaterialName = "AFJ Coral Pixel", GateMaterialName = "AFJ Coral Gate";

        /// <summary>Kesme düzlemli, verilen renkte paylaşılan malzeme (ad + renk başına bir tane).</summary>
        public static Material Lit(string name, Color color) => Get(name, color);

        /// <summary>Aynı, verilen pürüzsüzlükle (parlak metal/plastik parçalar).</summary>
        public static Material Lit(string name, Color color, float smoothness)
        {
            var m = Get(name, color);
            m.SetFloat(ShaderIds.Smoothness, smoothness);
            return m;
        }


        public static Transform Find(Transform root, string name)
        {
            Transform suffixed = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
                if (!suffixed && t.name.StartsWith(name + ".")) suffixed = t;
            }
            return suffixed;
        }

        static Color BaseColor(Material m) =>
            m.HasProperty(ShaderIds.BaseColor) ? m.GetColor(ShaderIds.BaseColor) : m.HasProperty(ShaderIds.Color) ? m.color : Color.white;

        static Material Get(string name, Color color)
        {
            if (Cache.TryGetValue((name, color), out var m) && m) return m;
            m = Make(name, color);
            Cache[(name, color)] = m;
            return m;
        }

        static Material Make(string name, Color color)
        {
            var m = new Material(shader) { name = $"{name} ({ColorUtility.ToHtmlStringRGB(color)})" };
            m.SetColor(ShaderIds.BaseColor, color);
            m.SetFloat(ShaderIds.Smoothness, 0.35f);
            // Joker seçimi parlaması: bloklar 1, resim pikselleri 2 (ClippableLit _GlowGroup).
            if (name == BlockMaterialName) m.SetFloat(ShaderIds.GlowGroup, 1f);
            else if (name == PixelMaterialName) m.SetFloat(ShaderIds.GlowGroup, 2f);
            // Bloklar, pikseller ve kapılar: aynı renkte daha parlak, şekerimsi yüzey. Siyah kapı kapakları eski görünümde
            // kalır (_GlossKeepDark): parlak malzemede gri ortam ışığı olmadığı için siyah kapaklar koyu duvarla birleşiyor,
            // açılışları görünmüyordu.
            if (name == BlockMaterialName || name == PixelMaterialName)
            {
                m.SetFloat(ShaderIds.Gloss, 1f);
                m.SetFloat(ShaderIds.Smoothness, 0.6f);
            }
            else if (name == GateMaterialName)
            {
                m.SetFloat(ShaderIds.Gloss, 1f);
                m.SetFloat(ShaderIds.GlossKeepDark, 1f);
            }
            return m;
        }

        /// <summary>Paletteki oyun rengi (malzemenin taban rengi).</summary>
        public static Color ColorOf(PrototypePalette palette, GameColor color) => palette.For(color).GetColor(ShaderIds.BaseColor);
    }
}
