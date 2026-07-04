using AppCore;
using AppUI.Classes;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck
{
    /// <summary>
    /// Load-order conflict detection for the Installed-mods list. Evaluates the same
    /// OrderAfter/OrderBefore rules as <see cref="GameLauncher.VerifyOrdering"/> (which
    /// only runs at launch behind a blocking dialog), but per mod and passively, so the
    /// Deck UI can badge offending rows and explain the problem in the details pane.
    /// </summary>
    internal static class DeckLoadOrderConflicts
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Conflict messages keyed by mod id, for the active profile's current order.
        /// A misordered pair is reported under both mods, since moving either fixes it.
        /// Mods without constraints (or inactive mods) have no entry.
        /// </summary>
        public static Dictionary<Guid, List<string>> FindConflicts()
        {
            var conflicts = new Dictionary<Guid, List<string>>();

            try
            {
                List<ProfileItem> activeItems = Sys.ActiveProfile?.ActiveItems;

                if (activeItems == null || activeItems.Count == 0)
                {
                    return conflicts;
                }

                var details = activeItems
                    .Select(i => Sys.Library.GetItem(i.ModID))
                    .Where(ii => ii != null)
                    .Select(ii => new { Mod = ii, Info = ii.GetModInfo() }) // GetModInfo is cached per install path
                    .ToDictionary(a => a.Mod.ModID, a => a);

                string belowFormat = ResourceHelper.Get(StringKey.ModIsMeantToComeBelowModInTheLoadOrder);
                string aboveFormat = ResourceHelper.Get(StringKey.ModIsMeantToComeAboveModInTheLoadOrder);

                void Add(Guid modId, string message)
                {
                    if (!conflicts.TryGetValue(modId, out List<string> list))
                    {
                        conflicts[modId] = list = new List<string>();
                    }

                    if (!list.Contains(message))
                    {
                        list.Add(message);
                    }
                }

                foreach (int i in Enumerable.Range(0, activeItems.Count))
                {
                    ProfileItem mod = activeItems[i];

                    if (!details.TryGetValue(mod.ModID, out var entry) || entry.Info == null)
                    {
                        continue;
                    }

                    string name = entry.Mod.CachedDetails?.Name ?? mod.ModID.ToString();

                    foreach (Guid after in entry.Info.OrderAfter)
                    {
                        if (activeItems.Skip(i).Any(pi => pi.ModID.Equals(after)))
                        {
                            string otherName = details[after].Mod.CachedDetails?.Name ?? after.ToString();
                            string message = string.Format(belowFormat, name, otherName);

                            Add(mod.ModID, message);
                            Add(after, message);
                        }
                    }

                    foreach (Guid before in entry.Info.OrderBefore)
                    {
                        if (activeItems.Take(i).Any(pi => pi.ModID.Equals(before)))
                        {
                            string otherName = details[before].Mod.CachedDetails?.Name ?? before.ToString();
                            string message = string.Format(aboveFormat, name, otherName);

                            Add(mod.ModID, message);
                            Add(before, message);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // a broken mod archive must not take the mod list down with it
                Logger.Warn(e, "Failed to evaluate load-order conflicts");
            }

            return conflicts;
        }
    }
}
