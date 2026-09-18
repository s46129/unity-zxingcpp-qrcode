using System;
using UnityEditor;
using UnityEngine;

namespace ZXingCpp.QRCode.Editor
{
    internal static class QRCodePluginImporterSettings
    {
        private static readonly BuildTarget[] PlayerTargets =
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
            BuildTarget.Android,
            BuildTarget.iOS
        };

        [MenuItem("Tools/ZXing-C++ QR Code/Apply Native Plugin Import Settings")]
        private static void ApplyAll()
        {
            string[] assetGuids = AssetDatabase.FindAssets("ZXing");
            int configuredCount = 0;

            foreach (string guid in assetGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(path) is PluginImporter importer))
                    continue;

                if (path.EndsWith("/Windows/x86_64/ZXing.dll", StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureWindows(importer);
                    configuredCount++;
                }
                else if (path.EndsWith("/Android/arm64-v8a/libZXing.so", StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureAndroid(importer);
                    configuredCount++;
                }
                else if (path.EndsWith("/macOS/libZXing.dylib", StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureMacOS(importer);
                    configuredCount++;
                }
                else if (path.EndsWith("/iOS/libZXing.a", StringComparison.OrdinalIgnoreCase))
                {
                    ConfigureIOS(importer);
                    configuredCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log(configuredCount == 0
                ? "ZXing-C++ QR Code: no ZXing native plugins were found. Build them from Native~ first."
                : $"ZXing-C++ QR Code: configured {configuredCount} native plugin importer(s).");
        }

        private static void ConfigureWindows(PluginImporter importer)
        {
            RestrictToPlayerTarget(importer, BuildTarget.StandaloneWindows64);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetEditorData("CPU", "x86_64");
            importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            importer.SaveAndReimport();
        }

        private static void ConfigureAndroid(PluginImporter importer)
        {
            RestrictToPlayerTarget(importer, BuildTarget.Android);
            importer.SetCompatibleWithEditor(false);
            importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            importer.SaveAndReimport();
        }

        private static void ConfigureMacOS(PluginImporter importer)
        {
            // The dylib is universal, so both the Apple silicon and the Intel Editor may load it.
            RestrictToPlayerTarget(importer, BuildTarget.StandaloneOSX);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "OSX");
            importer.SetEditorData("CPU", "AnyCPU");
            importer.SetPlatformData(BuildTarget.StandaloneOSX, "CPU", "AnyCPU");
            importer.SaveAndReimport();
        }

        private static void ConfigureIOS(PluginImporter importer)
        {
            RestrictToPlayerTarget(importer, BuildTarget.iOS);
            importer.SetCompatibleWithEditor(false);
            importer.SaveAndReimport();
        }

        private static void RestrictToPlayerTarget(PluginImporter importer, BuildTarget target)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            foreach (BuildTarget playerTarget in PlayerTargets)
                importer.SetCompatibleWithPlatform(playerTarget, playerTarget == target);
        }
    }
}
