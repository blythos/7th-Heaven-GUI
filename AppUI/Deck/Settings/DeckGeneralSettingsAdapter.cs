using AppCore;
using AppUI.ViewModels;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck.Settings
{
    /// <summary>
    /// Builds Deck settings rows for the bespoke <see cref="GeneralSettingsViewModel"/>,
    /// one hand-authored row per known property. Every change calls SaveSettings
    /// immediately (Deck live-apply; the desktop batches behind its OK button).
    /// Path pickers, subscription and extra-folder management are separate list
    /// screens and come later.
    /// </summary>
    internal static class DeckGeneralSettingsAdapter
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static List<DeckSettingRowViewModel> BuildRows()
        {
            var vm = new GeneralSettingsViewModel();
            vm.LoadSettings(Sys.Settings);

            Action apply = () =>
            {
                // SaveSettings pops a modal validation dialog when paths are missing;
                // pre-check so Deck mode degrades to a status message instead
                if (string.IsNullOrWhiteSpace(vm.FF7ExePathInput) || string.IsNullOrWhiteSpace(vm.LibraryPathInput))
                {
                    Sys.Message(new WMessage("Cannot save - set the game and library paths in the desktop settings first", true));
                    return;
                }

                try
                {
                    if (vm.SaveSettings())
                    {
                        Sys.SaveSettings();
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    Sys.Message(new WMessage("Failed to save general settings", true));
                }
            };

            List<string> ffnxChannels = Enum.GetNames(typeof(FFNxUpdateChannelOptions)).ToList();
            List<string> appChannels = Enum.GetNames(typeof(AppUpdateChannelOptions)).ToList();

            List<string> playCommands = new List<string>() { "Play with mods", "Play without mods", "Play with debug log", "Play with variable dump" };

            return new List<DeckSettingRowViewModel>()
            {
                DeckSettingRowViewModel.Header("Paths"),

                // usually auto-detected (and set up by MateriaForge on the Deck); editable
                // via the keyboard overlay for correction. Saving re-runs the desktop path.
                DeckSettingRowViewModel.Text("Game exe", "Path to ff7.exe / ff7_en.exe / FFVII.exe",
                    () => vm.FF7ExePathInput, v => { vm.FF7ExePathInput = v; apply(); }),

                DeckSettingRowViewModel.Text("Library folder", "Where installed mods are stored",
                    () => vm.LibraryPathInput, v => { vm.LibraryPathInput = v; apply(); }),

                DeckSettingRowViewModel.Header("Play"),

                // stored on GameLaunchSettings, so it persists via Sys.SaveSettings directly
                DeckSettingRowViewModel.Choice("Default play command", "What the play button launches; hold play to change it from anywhere",
                    playCommands,
                    () => (int)Sys.Settings.GameLaunchSettings.DefaultPlayCommand,
                    i => { Sys.Settings.GameLaunchSettings.DefaultPlayCommand = (DefaultPlayCommandOptions)i; Sys.SaveSettings(); }),

                DeckSettingRowViewModel.Header("Updates"),

                DeckSettingRowViewModel.Toggle("Check for updates automatically", "Check for app and driver updates at startup",
                    () => vm.CheckForUpdatesAuto, v => { vm.CheckForUpdatesAuto = v; apply(); }),

                DeckSettingRowViewModel.Choice("App update channel", "Which release channel to use for 7th Heaven updates",
                    appChannels,
                    () => appChannels.IndexOf(vm.AppUpdateChannel.ToString()),
                    i => { vm.AppUpdateChannel = (AppUpdateChannelOptions)Enum.Parse(typeof(AppUpdateChannelOptions), appChannels[i]); apply(); }),

                DeckSettingRowViewModel.Choice("FFNx update channel", "Which release channel to use for game driver updates",
                    ffnxChannels,
                    () => ffnxChannels.IndexOf(vm.FFNxUpdateChannel.ToString()),
                    i => { vm.FFNxUpdateChannel = (FFNxUpdateChannelOptions)Enum.Parse(typeof(FFNxUpdateChannelOptions), ffnxChannels[i]); apply(); }),

                DeckSettingRowViewModel.Toggle("Auto update mods", "Install mod updates automatically instead of notifying",
                    () => vm.AutoUpdateModsByDefault, v => { vm.AutoUpdateModsByDefault = v; apply(); }),

                DeckSettingRowViewModel.Header("Mods"),

                DeckSettingRowViewModel.Toggle("Auto sort mods", "Sort the load order by category automatically",
                    () => vm.AutoSortModsByDefault, v => { vm.AutoSortModsByDefault = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Activate installed mods automatically", "New mods become active as soon as they install",
                    () => vm.ActivateInstalledModsAuto, v => { vm.ActivateInstalledModsAuto = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Auto import mods", "Watch the library folder and import new mod files",
                    () => vm.ImportLibraryFolderAuto, v => { vm.ImportLibraryFolderAuto = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Bypass compatibility locks", "Ignore mod compatibility restrictions (advanced)",
                    () => vm.BypassCompatibilityLocks, v => { vm.BypassCompatibilityLocks = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Warn about mod code", "Warn before running mods that contain code",
                    () => vm.WarnAboutModCode, v => { vm.WarnAboutModCode = v; apply(); }),

                DeckSettingRowViewModel.Header("Desktop integration"),

                DeckSettingRowViewModel.Toggle("Open iros links with 7th Heaven", "Register the iros:// link handler",
                    () => vm.OpenIrosLinks, v => { vm.OpenIrosLinks = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Open mod files with 7th Heaven", "Associate .iro files with the app",
                    () => vm.OpenModFilesWith7H, v => { vm.OpenModFilesWith7H = v; apply(); }),

                DeckSettingRowViewModel.Toggle("Show in file explorer context menu", "Add 7th Heaven entries to the right-click menu",
                    () => vm.ShowContextMenuInExplorer, v => { vm.ShowContextMenuInExplorer = v; apply(); }),
            };
        }
    }
}
