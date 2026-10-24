using System;
using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// New Campaign replaces one numbered slot. The autosave, world files, and the other
    /// slots stay on disk. A stamp stops a fresh run from booting a previous campaign's
    /// worlds; Load still reads those files.
    /// </summary>
    public static class CampaignSlots
    {
        public const string FreshKey = "SM_FreshCampaign";
        public const string ActiveSlotKey = "SM_ActiveSaveSlot";
        public const string PendingLoadKey = "SM_PendingLoadSlot";
        public const string StampKey = "SM_CampaignStamp";

        public static bool IsPlayerSlot(int slot) =>
            slot >= 1 && slot < SaveSystem.SlotCount;

        /// <summary>The only slot a new campaign may delete. Anything outside 1..3 uses slot 1.</summary>
        public static int SlotReplacedByNewCampaign(int chosen) =>
            IsPlayerSlot(chosen) ? chosen : 1;

        public static void ReplaceChosenSlot(int chosen) =>
            SaveSystem.Delete(SlotReplacedByNewCampaign(chosen));

        public static void BeginFresh(int chosen)
        {
            int slot = SlotReplacedByNewCampaign(chosen);
            GamePrefs.SetInt(ActiveSlotKey, slot);
            GamePrefs.SetInt(FreshKey, 1);
            GamePrefs.DeleteKey(PendingLoadKey);
            GamePrefs.SetString(StampKey, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            GamePrefs.Save();
        }

        public static bool ConsumeFresh()
        {
            if (GamePrefs.GetInt(FreshKey, 0) != 1) return false;
            GamePrefs.DeleteKey(FreshKey);
            GamePrefs.Save();
            return true;
        }

        public static void MarkPendingLoad(int slot)
        {
            GamePrefs.SetInt(PendingLoadKey, slot);
            if (slot >= 0 && slot < SaveSystem.SlotCount)
                GamePrefs.SetInt(ActiveSlotKey, slot);
            GamePrefs.DeleteKey(FreshKey);
            GamePrefs.Save();
        }

        public static int ConsumePendingLoad()
        {
            if (!GamePrefs.HasKey(PendingLoadKey)) return -1;
            int slot = GamePrefs.GetInt(PendingLoadKey, -1);
            GamePrefs.DeleteKey(PendingLoadKey);
            GamePrefs.Save();
            return slot;
        }

        public static int ActiveSlot =>
            GamePrefs.GetInt(ActiveSlotKey, SaveSystem.AutosaveSlot);

        public static string Stamp => GamePrefs.GetString(StampKey, "");

        public static void AdoptStamp(string stamp)
        {
            if (string.IsNullOrEmpty(stamp)) GamePrefs.DeleteKey(StampKey);
            else GamePrefs.SetString(StampKey, stamp);
            GamePrefs.Save();
        }

        public static bool LiveSnapshotMatches(string snapshotStamp) =>
            StampsMatch(Stamp, snapshotStamp);

        /// <summary>No active stamp means a legacy run: keep loading old files.</summary>
        public static bool StampsMatch(string active, string snapshot)
        {
            if (string.IsNullOrEmpty(active)) return true;
            return string.Equals(active, snapshot ?? "", StringComparison.Ordinal);
        }
    }
}
