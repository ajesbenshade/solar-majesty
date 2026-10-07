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
            PlayerPrefs.SetInt(ActiveSlotKey, slot);
            PlayerPrefs.SetInt(FreshKey, 1);
            PlayerPrefs.DeleteKey(PendingLoadKey);
            PlayerPrefs.SetString(StampKey, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            PlayerPrefs.Save();
        }

        public static bool ConsumeFresh()
        {
            if (PlayerPrefs.GetInt(FreshKey, 0) != 1) return false;
            PlayerPrefs.DeleteKey(FreshKey);
            PlayerPrefs.Save();
            return true;
        }

        public static void MarkPendingLoad(int slot)
        {
            PlayerPrefs.SetInt(PendingLoadKey, slot);
            if (slot >= 0 && slot < SaveSystem.SlotCount)
                PlayerPrefs.SetInt(ActiveSlotKey, slot);
            PlayerPrefs.DeleteKey(FreshKey);
            PlayerPrefs.Save();
        }

        public static int ConsumePendingLoad()
        {
            if (!PlayerPrefs.HasKey(PendingLoadKey)) return -1;
            int slot = PlayerPrefs.GetInt(PendingLoadKey, -1);
            PlayerPrefs.DeleteKey(PendingLoadKey);
            PlayerPrefs.Save();
            return slot;
        }

        public static int ActiveSlot =>
            PlayerPrefs.GetInt(ActiveSlotKey, SaveSystem.AutosaveSlot);

        public static string Stamp => PlayerPrefs.GetString(StampKey, "");

        public static void AdoptStamp(string stamp)
        {
            if (string.IsNullOrEmpty(stamp)) PlayerPrefs.DeleteKey(StampKey);
            else PlayerPrefs.SetString(StampKey, stamp);
            PlayerPrefs.Save();
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
