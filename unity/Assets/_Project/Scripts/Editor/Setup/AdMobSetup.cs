using UnityEditor;
using UnityEngine;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// AdMob uygulama kimliğini eklenti ayarına yazar (Info.plist'e buradan geçer). Gerçek uygulama kimliği bütün
    /// derlemelerde kullanılır; reklam birimleri ise yalnız App Store derlemesinde gerçektir (bkz. Ads). Komut satırından: -executeMethod AstroFlowJam.Editor.AdMobSetup.Apply
    /// </summary>
    public static class AdMobSetup
    {
        const string IosAppId = "ca-app-pub-1035801224777804~1936843638";
        const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        [MenuItem("Astro Flow Jam/AdMob Ayarını Yaz")]
        public static void Apply()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(SettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance("GoogleMobileAdsSettings");
                AssetDatabase.CreateFolder("Assets/GoogleMobileAds", "Resources");
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            var so = new SerializedObject(settings);
            so.FindProperty("adMobIOSAppId").stringValue = IosAppId;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
