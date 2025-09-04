using MiddleMousePan.Common;
using HarmonyLib;
using Kingmaker.UI.Models.SettingsUI.SettingAssets;
using Owlcat.Runtime.Core;
using System;
using System.Linq;
using System.Reflection;

namespace MiddleMousePan.Settings;

public abstract class ModSettingEntry
{
    public const string PREFIX = "azalen.middlemousepan";

    public readonly string Key;
    public readonly string Title;
    public readonly string Tooltip;

    public SettingStatus Status { get; private set; } = SettingStatus.NOT_APPLIED;

    protected ModSettingEntry(string key, string title, string tooltip)
    {
        Key = key;
        Title = title;
        Tooltip = tooltip;
    }

    /// <summary>
    /// Enable setting.
    /// By default searches for nested static class with <see cref="HarmonyPatch"/> attribute
    /// and calls TryEnableAndPatch with it
    /// </summary>
    /// <returns></returns>
    public virtual SettingStatus TryEnable()
    {
        Type[] nestedTypes = GetType().GetNestedTypes(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var patchClasses = nestedTypes
            .Where(type => type.IsDefined(typeof(HarmonyPatch), inherit: false))
            .ToArray();
        if (patchClasses.Empty())
        {
            Main.log.Warning($"No patch classes defined for {Title}");
        }
#if DEBUG
        foreach (var patchClass in patchClasses)
        {
            Main.log.Log($"For {Title} found patch class: {patchClass.Name}");
        }
#endif
        return TryEnableAndPatch(patchClasses);
    }

    protected abstract SettingStatus TryEnableAndPatch(params Type[] type);

    public virtual SettingStatus TryDisable() => Status;

    public abstract void BuildUIAndLink();

    public abstract UISettingsEntityBase GetUISettings();

    protected SettingStatus TryPatchInternal(params Type[] type)
    {
        if (Status != SettingStatus.NOT_APPLIED) return Status;
        try
        {
            foreach (Type t in type)
            {
                Main.HarmonyInstance.CreateClassProcessor(t).Patch();
            }
            Status = SettingStatus.WORKING;
            Main.log.Log($"{Title} patch succeeded");
        }
        catch (Exception ex)
        {
            Main.log.Error($"{Title} patch exception: {ex.Message}");
            Status = SettingStatus.ERROR;
#if DEBUG
            throw ex;
#endif
        }
        return Status;
    }

    protected SettingStatus TryUnpatchInternal(params Type[] type)
    {
        if (Status != SettingStatus.WORKING) return Status;
        try
        {
            foreach (Type t in type)
            {
                HarmonyUtils.UnpatchClass(Main.HarmonyInstance, t);
            }
            Status = SettingStatus.NOT_APPLIED;
            Main.log.Log($"{Title} unpatch succeeded");
        }
        catch (Exception ex)
        {
            Main.log.Error($"{Title} unpatch exception: {ex.Message}");
            Status = SettingStatus.ERROR;
        }
        return Status;
    }
}
