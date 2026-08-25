using System;
using UnityEditor;
using UnityEngine;

namespace S46129.QRCode.Editor
{
    internal static class QRCodePluginImporterSettings
    {
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
            }

            AssetDatabase.Refresh();
            Debug.Log(configuredCount == 0
                ? "ZXing-C++ QR Code: no ZXing native plugins were found. Build them from Native~ first."
                : $"ZXing-C++ QR Code: configured {configuredCount} native plugin importer(s).");
        }

        private static void ConfigureWindows(PluginImporter importer)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetEditorData("CPU", "x86_64");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
            importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
            importer.SaveAndReimport();
        }

        private static void ConfigureAndroid(PluginImporter importer)
        {
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
            importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            importer.SaveAndReimport();
        }
    }
}
