using AppCore;
using AppUI.Classes;
using AppUI.ViewModels;
using Iros;
using Iros.Workshop;
using Iros.Workshop.ConfigSettings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppUI.Deck.Settings
{
    /// <summary>
    /// Builds Deck settings rows for the FFNx game driver over the existing
    /// <see cref="GLSettingViewModel"/>. The spec loading (including the runtime
    /// resolution/display dropdown overrides) mirrors ConfigureGLWindow.Init, which
    /// cannot be reused because it is welded to the desktop dialog. Unlike the
    /// desktop's batch save, every change here saves the FFNx config immediately.
    /// </summary>
    internal static class DeckGameDriverAdapter
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Returns null (reporting why via <see cref="Sys.Message"/>) when the driver
        /// config or ui spec cannot be loaded.
        /// </summary>
        public static List<DeckSettingRowViewModel> TryBuildRows()
        {
            if (!File.Exists(Sys.PathToFFNxToml))
            {
                Sys.Message(new WMessage("Game driver config not found - run the game once first", true));
                return null;
            }

            Sys.FFNxConfig.Reload();

            ConfigSpec spec;

            try
            {
                spec = Util.Deserialize<ConfigSpec>(Sys.PathToGameDriverUiXml(Sys.Settings.AppLanguage));
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Sys.Message(new WMessage("Failed to read the game driver settings spec - update FFNx", true));
                return null;
            }

            OverrideResolutionAndDisplayOptions(spec);

            var settings = new Iros.Workshop.ConfigSettings.Settings();
            settings.SetMissingDefaults(spec.Settings);

            // same group ordering as the desktop tabs; unknown groups go last
            var groupOrder = new Dictionary<string, int>()
            {
                { ResourceHelper.Get(StringKey.Graphics), 0 },
                { ResourceHelper.Get(StringKey.Controls), 1 },
                { ResourceHelper.Get(StringKey.Cheats), 2 },
                { ResourceHelper.Get(StringKey.Advanced), 3 },
            };

            var rows = new List<DeckSettingRowViewModel>();

            foreach (var group in spec.Settings.GroupBy(s => s.Group)
                                               .OrderBy(g => groupOrder.TryGetValue(g.Key, out int order) ? order : int.MaxValue))
            {
                rows.Add(DeckSettingRowViewModel.Header(group.Key));

                foreach (Setting setting in group)
                {
                    rows.Add(BuildRow(setting, settings));
                }
            }

            return rows;
        }

        private static DeckSettingRowViewModel BuildRow(Setting setting, Iros.Workshop.ConfigSettings.Settings settings)
        {
            var vm = new GLSettingViewModel(setting, settings);

            Action apply = () =>
            {
                try
                {
                    vm.Save(settings);
                    settings.Save();
                }
                catch (UnauthorizedAccessException)
                {
                    Sys.Message(new WMessage(ResourceHelper.Get(StringKey.CouldNotWriteTo7HGameDriverCfg), true));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    Sys.Message(new WMessage("Failed to save game driver setting", true));
                }
            };

            switch (vm.SettingType)
            {
                case GLSettingType.Checkbox:
                    return DeckSettingRowViewModel.Toggle(vm.Name, vm.Description,
                        get: () => vm.IsOptionChecked,
                        set: value => { vm.IsOptionChecked = value; apply(); });

                case GLSettingType.Dropdown:
                    return DeckSettingRowViewModel.Choice(vm.Name, vm.Description,
                        choices: vm.DropdownOptions.Select(o => o.DisplayText).ToList(),
                        getIndex: () => vm.SelectedDropdownIndex,
                        setIndex: index => { vm.SelectedDropdownIndex = index; apply(); });

                default:
                    return BuildTextEntryRow(vm, setting, apply);
            }
        }

        private static DeckSettingRowViewModel BuildTextEntryRow(GLSettingViewModel vm, Setting setting, Action apply)
        {
            List<string> suggestions = (setting as TextEntry)?.Suggestions?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            // suggestion list -> treat like a dropdown over the suggestions
            if (suggestions != null && suggestions.Any())
            {
                return DeckSettingRowViewModel.Choice(vm.Name, vm.Description,
                    choices: suggestions,
                    getIndex: () => suggestions.FindIndex(s => s.Equals(vm.TextEntryOptionValue, StringComparison.InvariantCultureIgnoreCase)),
                    setIndex: index => { vm.TextEntryOptionValue = suggestions[index]; apply(); },
                    getCustomValue: () => vm.TextEntryOptionValue);
            }

            // numeric value -> stepper
            if (long.TryParse(vm.TextEntryOptionValue, out _) || double.TryParse(vm.TextEntryOptionValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return DeckSettingRowViewModel.Stepper(vm.Name, vm.Description,
                    get: () => vm.TextEntryOptionValue,
                    set: value => { vm.TextEntryOptionValue = value; apply(); });
            }

            // free text -> keyboard-fallback field
            return DeckSettingRowViewModel.Text(vm.Name, vm.Description,
                get: () => vm.TextEntryOptionValue,
                set: value => { vm.TextEntryOptionValue = value; apply(); });
        }

        /// <summary>
        /// Replaces the spec's resolution and display dropdowns with values reported by
        /// the OS, exactly as ConfigureGLWindow.Init does.
        /// </summary>
        private static void OverrideResolutionAndDisplayOptions(ConfigSpec spec)
        {
            var resolutions = new List<DDOption>()
            {
                new DDOption() { Settings = "window_size_x = 0,window_size_y = 0", Text = "Auto" },
            };

            foreach (SupportedResolution sr in PrimaryScreen.GetSupportedResolutions())
            {
                resolutions.Add(new DDOption() { Settings = $"window_size_x = {sr.H},window_size_y = {sr.V}", Text = $"{sr.H}x{sr.V}" });
            }

            if (spec.Settings.Find(item => item.DefaultValue == "window_size_x = 1280,window_size_y = 720") is DropDown resolutionDropdown)
            {
                resolutionDropdown.Options = resolutions;
            }

            var screens = new List<DDOption>()
            {
                new DDOption() { Settings = "display_index = -1", Text = "Primary Display" },
            };

            foreach (var (index, screen) in WindowsDisplayAPI.DisplayConfig.PathDisplayTarget.GetDisplayTargets().Index())
            {
                screens.Add(new DDOption() { Settings = $"display_index = {index + 1}", Text = $"{screen.FriendlyName}" });
            }

            if (spec.Settings.Find(item => item.DefaultValue == "display_index = -1") is DropDown displayDropdown)
            {
                displayDropdown.Options = screens;
            }
        }
    }
}
