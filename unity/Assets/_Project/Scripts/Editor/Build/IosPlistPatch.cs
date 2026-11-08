using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// iOS derlemesinden sonra Xcode projesine eklenenler: reklam takibi izninde gösterilen açıklama
    /// (NSUserTrackingUsageDescription), App Store'un her yüklemede sorduğu şifreleme sorusunun cevabı (standart dışı
    /// şifreleme yok) ve izin penceresi için AppTrackingTransparency çerçevesi (iOS 14 öncesi için zayıf bağlı).
    /// </summary>
    public static class IosPlistPatch
    {
        public const string TrackingText =
            "Allow tracking to see ads that are more relevant to you. Ads help keep Astro Flow Jam free to play.";

        [PostProcessBuild(46)]
        public static void Apply(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
#if UNITY_IOS
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingText);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            project.WriteToFile(projectPath);
#endif
        }
    }
}
