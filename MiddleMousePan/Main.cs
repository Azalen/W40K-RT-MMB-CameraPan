using MiddleMousePan.Localization;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace MiddleMousePan;

#if DEBUG
[EnableReloading]
#endif
public static class Main
{
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    internal static UnityModManager.ModEntry ModEntry;

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        ModEntry = modEntry;

        log = modEntry.Logger;
        HarmonyInstance = new Harmony(modEntry.Info.Id);

        // load localization
        ModLocalizationManager.Init();

        // patches to configure mod
        HarmonyInstance.CreateClassProcessor(typeof(SettingsUIPatches)).Patch();

        modEntry.OnGUI = OnGUI;
#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        return true;
    }

#if DEBUG
    static bool OnUnload(UnityModManager.ModEntry modEntry)
    {
        return true;
    }
#endif

    static void OnGUI(UnityModManager.ModEntry modEntry)
    {
        GUILayout.Label("This mod is configured through normal game settings. Go to Settings -> Controls -> scroll to the bottom");
    }
}