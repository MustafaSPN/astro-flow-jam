using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// iOS Xcode projesi üretir. Simülatör derlemesi SDK ayarını yalnızca derleme süresince değiştirir.
    /// Komut satırından: -executeMethod AstroFlowJam.Editor.BuildIOS.Simulator
    /// </summary>
    public static class BuildIOS
    {
        const string SimulatorPath = "Builds/iOS-Simulator";
        // Xcode'daki kişisel geliştirici ekibi (Personal Team); cihaz derlemesi otomatik imzalanır.
        const string DevelopmentTeam = "F2C2MQQJ84";
        const string DevicePath = "Builds/iOS-Device";

        [MenuItem("Astro Flow Jam/iOS Derle/Simülatör")]
        public static void Simulator() => Build(iOSSdkVersion.SimulatorSDK,
            System.Environment.GetEnvironmentVariable("AFJ_BUILD_DIR") ?? SimulatorPath, BuildOptions.Development);

        /// <summary>Mağaza ekran görüntüleri için: simülatörde yayın derlemesi (geliştirici menüsü yok; rozete dokununca bölüm geçilir).</summary>
        public static void SimulatorRelease() => Build(iOSSdkVersion.SimulatorSDK,
            System.Environment.GetEnvironmentVariable("AFJ_BUILD_DIR") ?? SimulatorPath, BuildOptions.None, "AFJ_SHOTS");

        [MenuItem("Astro Flow Jam/iOS Derle/Cihaz")]
        public static void Device() => Build(iOSSdkVersion.DeviceSDK, DevicePath, BuildOptions.Development);

        /// <summary>Yayın ve performans ölçümü: geliştirme eklerini içermez.</summary>
        [MenuItem("Astro Flow Jam/iOS Derle/Cihaz (Release)")]
        public static void DeviceRelease() => Build(iOSSdkVersion.DeviceSDK, DevicePath, BuildOptions.None);

        /// <summary>
        /// App Store'a gönderilecek derleme: yayın derlemesiyle aynı, tek farkı gerçek AdMob reklam birimleri (AFJ_STORE).
        /// Kendi cihazında denemek için DeviceRelease kullan (test reklamları).
        /// </summary>
        [MenuItem("Astro Flow Jam/iOS Derle/App Store")]
        public static void AppStore() => Build(iOSSdkVersion.DeviceSDK, AppStorePath, BuildOptions.None, "AFJ_STORE");

        const string AppIconPath = "Assets/_Project/Art/AppIcon/AppIcon.png";

        /// <summary>
        /// Uygulama simgesi: 1024x1024, şeffaflıksız tek görsel varsayılan simge yapılır; Unity iPhone, iPad ve App Store
        /// boyutlarını bundan üretir. Görsel sıkıştırılmadan alınır (simgede kalite kaybı olmasın).
        /// </summary>
        static void ApplyAppIcon()
        {
            if (AssetImporter.GetAtPath(AppIconPath) is TextureImporter importer)
            {
                var changed = importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled
                    || importer.maxTextureSize < 1024 || importer.npotScale != TextureImporterNPOTScale.None;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 2048;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = false;
                    importer.SaveAndReimport();
                }
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (icon) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            else Debug.LogWarning($"Uygulama simgesi bulunamadı: {AppIconPath}");
        }

        const string AppStorePath = "Builds/iOS-AppStore";

        static void Build(iOSSdkVersion sdk, string path, BuildOptions buildOptions, params string[] defines)
        {
            var previous = PlayerSettings.iOS.sdkVersion;
            PlayerSettings.iOS.sdkVersion = sdk;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = DevelopmentTeam;
            // Apple Silicon Mac'lerdeki simülatörler yalnızca arm64 çalıştırır.
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            ApplyAppIcon();
            try
            {
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/_Project/Scenes/Prototype.unity" },
                    locationPathName = path,
                    target = BuildTarget.iOS,
                    options = buildOptions,
                    // Geliştirici araçları yalnız geliştirme derlemesinde (DevTools).
                    extraScriptingDefines = (buildOptions & BuildOptions.Development) != 0 ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(defines, "AFJ_DEV")) : defines,
                };
                var report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"iOS derlemesi ({sdk}): {report.summary.result} → {path}");
                if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                    EditorApplication.Exit(1);
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previous;
            }
        }
    }
}
