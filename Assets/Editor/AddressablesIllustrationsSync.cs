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

        int cardArtScanned = 0;
        int cardArtMoved = 0;
        int cardArtAddressed = 0;
        int cardArtLabeled = 0;
        EnsureArtInDefaultGroup(settings, ref cardArtScanned, ref cardArtMoved, ref cardArtAddressed, ref cardArtLabeled);

        if (changed > 0 || cardArtMoved > 0 || cardArtAddressed > 0 || cardArtLabeled > 0)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            $"Addressables sync complete. Scanned {scanned} art entries, updated {changed} addresses. " +
            $"Checked {cardArtScanned} art assets, moved/created {cardArtMoved} in the Default group, " +
            $"updated {cardArtAddressed} art addresses, labeled {cardArtLabeled} as default.");
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

    private static void EnsureArtInDefaultGroup(
        AddressableAssetSettings settings,
        ref int scanned,
        ref int movedOrCreated,
        ref int addressChanged,
        ref int labelChanged)
    {
        AddressableAssetGroup defaultGroup = settings.DefaultGroup;
        if (defaultGroup == null)
        {
            Debug.LogError("Addressables default group not found.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { ArtRoot });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                continue;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                continue;
            }

            scanned++;

            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid, false);
            if (existingEntry == null || existingEntry.parentGroup != defaultGroup)
            {
                movedOrCreated++;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, defaultGroup);
            if (entry == null)
            {
                continue;
            }

            if (entry.SetLabel("default", true, false, false))
            {
                labelChanged++;
            }

            string expectedAddress = assetPath;
            if (entry.address == expectedAddress)
            {
                continue;
            }

            Debug.Log($"Addressables sync: {entry.address} -> {expectedAddress}");
            entry.SetAddress(expectedAddress, false);
            addressChanged++;
        }
    }
}
