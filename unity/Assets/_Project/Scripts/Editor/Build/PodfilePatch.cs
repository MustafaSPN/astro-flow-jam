using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

namespace AstroFlowJam.Editor
{
    /// <summary>
    /// Bazı pod'lar eski iOS hedefi (12.0) ister, güncel Xcode bunu derlemez. Podfile üretildikten sonra, pod install'dan önce
    /// bütün pod'ların hedefi oyunun hedefine çekilir (External Dependency Manager: üretim 40–41, kurulum 50).
    /// </summary>
    public static class PodfilePatch
    {
        const string Marker = "IPHONEOS_DEPLOYMENT_TARGET";

        [PostProcessBuild(45)]
        public static void Apply(BuildTarget target, string path)
        {
            var podfile = Path.Combine(path, "Podfile");
            if (target != BuildTarget.iOS || !File.Exists(podfile) || File.ReadAllText(podfile).Contains(Marker)) return;
            File.AppendAllText(podfile, $@"
post_install do |installer|
  installer.pods_project.targets.each do |t|
    t.build_configurations.each {{ |c| c.build_settings['{Marker}'] = '{PlayerSettings.iOS.targetOSVersionString}' }}
  end
end
");
        }
    }
}
