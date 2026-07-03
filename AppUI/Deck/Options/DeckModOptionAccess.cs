using AppCore;
using AppUI.Classes;
using AppUI.ViewModels;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppUI.Deck.Options
{
    /// <summary>
    /// Builds everything <see cref="ConfigureModViewModel.Init"/> needs for a mod.
    /// Mirrors the reader/constraint setup embedded in
    /// <see cref="MyModsViewModel.ShowConfigureModWindow"/>, which cannot be reused
    /// because it is welded to the desktop modal dialog. Unlike the desktop path the
    /// archive must stay open while the Deck screen is up — dispose this when done.
    /// </summary>
    public class DeckModOptionAccess : IDisposable
    {
        public AppWrapper.ModInfo Info { get; private set; }
        public Func<string, string> ImageReader { get; private set; }
        public Func<string, Stream> AudioReader { get; private set; }
        internal List<Constraint> Constraints { get; private set; }
        public string PathToModXml { get; private set; }

        private IDisposable _archive;

        private DeckModOptionAccess() { }

        /// <summary>
        /// Returns null (with a <see cref="Sys.Message"/> explaining why) when the mod
        /// cannot be configured: missing from disk, unreadable mod.xml, or no options.
        /// </summary>
        public static DeckModOptionAccess TryCreate(InstalledModViewModel mod)
        {
            if (!mod.InstallInfo.ModExistsOnFileSystem())
            {
                Sys.ValidateAndRemoveDeletedMods();
                Sys.Message(new WMessage(string.Format(ResourceHelper.Get(StringKey.CanNotConfigureModItHasBeenRemoved), mod.Name), true));
                return null;
            }

            AppWrapper.ModInfo info = mod.InstallInfo.GetModInfo();

            if (info == null)
            {
                Sys.Message(new WMessage(string.Format(ResourceHelper.Get(StringKey.CanNotConfigureModFailedToReadModXml), mod.Name), true));
                return null;
            }

            if (info.Options.Count == 0)
            {
                Sys.Message(new WMessage(ResourceHelper.Get(StringKey.ThereAreNoOptionsToConfigureForThisMod), true));
                return null;
            }

            InstalledVersion installed = Sys.Library.GetItem(mod.InstallInfo.ModID)?.LatestInstalled;
            string configTempFolder = Path.Combine(Sys.PathToTempFolder, "configmod", mod.InstallInfo.CachedDetails.ID.ToString());
            string pathToModXml = Path.Combine(Sys.Settings.LibraryLocation, installed.InstalledLocation);

            var access = new DeckModOptionAccess()
            {
                Info = info,
                PathToModXml = pathToModXml,
                Constraints = GameLauncher.GetConstraints().Where(c => c.ModID.Equals(mod.InstallInfo.ModID)).ToList(),
            };

            if (pathToModXml.EndsWith(".iro", StringComparison.InvariantCultureIgnoreCase))
            {
                var arc = new AppWrapper.IrosArc(pathToModXml);
                access._archive = arc;

                access.ImageReader = s =>
                {
                    if (!arc.HasFile(s))
                    {
                        return null;
                    }

                    string tempImgPath = Path.Combine(configTempFolder, s);

                    if (File.Exists(tempImgPath))
                    {
                        return tempImgPath;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(tempImgPath));

                    using (Stream imgStream = arc.GetData(s))
                    {
                        try
                        {
                            using (FileStream fileStream = new FileStream(tempImgPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
                            {
                                imgStream.CopyTo(fileStream);
                            }
                        }
                        catch (Exception e)
                        {
                            NLog.LogManager.GetCurrentClassLogger().Warn(e, "Failed to extract preview image from iro");
                            return null;
                        }
                    }

                    return tempImgPath;
                };

                // matches the desktop implementation, which also never returns iro audio streams
                access.AudioReader = s => null;
            }
            else
            {
                access.ImageReader = s =>
                {
                    string ifile = Path.Combine(pathToModXml, s);
                    return File.Exists(ifile) ? ifile : null;
                };

                access.AudioReader = s =>
                {
                    string ifile = Path.Combine(pathToModXml, s);
                    return File.Exists(ifile)
                        ? new FileStream(ifile, FileMode.Open, FileAccess.Read, FileShare.Read)
                        : (Stream)null;
                };
            }

            return access;
        }

        public void Dispose()
        {
            _archive?.Dispose();
            _archive = null;
        }
    }
}
