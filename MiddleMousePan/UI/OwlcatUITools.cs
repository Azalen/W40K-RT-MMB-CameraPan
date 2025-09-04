using MiddleMousePan.Localization;
using Kingmaker.UI.Models.SettingsUI;
using Kingmaker.UI.Models.SettingsUI.SettingAssets;
using UnityEngine;

namespace MiddleMousePan.UI;

public static class OwlcatUITools
{
    public static UISettingsGroup MakeSettingsGroup(string key, string name, params UISettingsEntityBase[] settings)
    {
        UISettingsGroup group = ScriptableObject.CreateInstance<UISettingsGroup>();
        group.name = key;
        group.Title = ModLocalizationManager.CreateString(key, name);

        group.SettingsList = settings;

        return group;
    }
}
