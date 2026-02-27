using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressablesIllustrationsSync
{
    private const string ArtRoot = "Assets/Art/";
    private const string MenuSync = "Tools/Addressables/Sync Art Addresses";
    private const string MenuReport = "Tools/Addressables/Report Art Address Mismatches";

    [MenuItem(MenuSync)]
    public static void SyncArtAddresses()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressables settings not found.");
            return;
        }

        int scanned = 0;
        int changed = 0;
        foreach (AddressableAssetEntry entry in EnumerateArtEntries(settings))
        {
            scanned++;
            string expectedAddress = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(expectedAddress))
            {
                continue;
            }

            if (entry.address == expectedAddress)
            {
                continue;
            }

            Debug.Log($"Addressables sync: {entry.address} -> {expectedAddress}");
            entry.SetAddress(expectedAddress, false);
            changed++;
        }

        if (changed > 0)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"Addressables sync complete. Scanned {scanned} art entries, updated {changed}.");
    }

    [MenuItem(MenuReport)]
    public static void ReportArtAddressMismatches()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressables settings not found.");
            return;
        }

        int mismatches = 0;
        foreach (AddressableAssetEntry entry in EnumerateArtEntries(settings))
        {
            string expectedAddress = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (string.IsNullOrEmpty(expectedAddress))
            {
                continue;
            }

            if (entry.address == expectedAddress)
            {
                continue;
            }

            mismatches++;
            Debug.LogWarning($"Address mismatch: '{entry.address}' should be '{expectedAddress}'.");
        }

        Debug.Log($"Addressables mismatch report complete. Found {mismatches} mismatches.");
    }

    private static IEnumerable<AddressableAssetEntry> EnumerateArtEntries(AddressableAssetSettings settings)
    {
        foreach (AddressableAssetGroup group in settings.groups)
        {
            if (group == null) continue;

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null) continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                if (!assetPath.StartsWith(ArtRoot)) continue;

                yield return entry;
            }
        }
    }
}
