using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class CharacterAnimatorControllers
{
    private const string ControllersLabel = "default";
    private const string ControllersAddressRoot = "Assets/Art/Characters/Animations/";
    private const string FallbackControllerName = "common";

    private static readonly Dictionary<string, RuntimeAnimatorController> controllersByName = new();
    private static bool loadStarted;
    private static bool isLoaded;
    private static int pendingLoads;

    public static bool IsLoaded => isLoaded;

    public static void EnsureLoading()
    {
        if (loadStarted) return;
        loadStarted = true;
        Addressables.LoadResourceLocationsAsync(ControllersLabel, typeof(RuntimeAnimatorController)).Completed += OnLocationsLoaded;
    }

    private static void OnLocationsLoaded(AsyncOperationHandle<IList<IResourceLocation>> handle)
    {
        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            isLoaded = true;
            Debug.LogError($"CharacterAnimatorControllers: failed to load Addressables label '{ControllersLabel}'.");
            return;
        }

        foreach (IResourceLocation location in handle.Result)
        {
            if (location == null || string.IsNullOrWhiteSpace(location.PrimaryKey)) continue;
            if (!location.PrimaryKey.StartsWith(ControllersAddressRoot)) continue;

            pendingLoads++;
            Addressables.LoadAssetAsync<RuntimeAnimatorController>(location).Completed += OnControllerLoaded;
        }

        isLoaded = pendingLoads == 0;
    }

    private static void OnControllerLoaded(AsyncOperationHandle<RuntimeAnimatorController> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            controllersByName[Normalize(handle.Result.name)] = handle.Result;
        }

        pendingLoads = Mathf.Max(0, pendingLoads - 1);
        if (pendingLoads == 0)
        {
            isLoaded = true;
            Debug.Log($"CharacterAnimatorControllers: loaded {controllersByName.Count} animator controllers.");
        }
    }

    public static RuntimeAnimatorController Resolve(string characterSprite, RacesEnum race)
    {
        EnsureLoading();
        if (TryGet(characterSprite, out RuntimeAnimatorController controller)) return controller;
        if (TryGet(race.ToString(), out controller)) return controller;
        if (TryGet(FallbackControllerName, out controller)) return controller;
        return null;
    }

    private static bool TryGet(string controllerName, out RuntimeAnimatorController controller)
    {
        controller = null;
        if (string.IsNullOrWhiteSpace(controllerName)) return false;
        return controllersByName.TryGetValue(Normalize(controllerName), out controller) && controller != null;
    }

    private static string Normalize(string controllerName)
    {
        return controllerName.Trim().ToLowerInvariant();
    }
}
