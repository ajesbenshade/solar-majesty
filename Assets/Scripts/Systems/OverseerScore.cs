using UnityEngine;

namespace SolarMajesty
{
    /// <summary>
    /// Overseer rating from gates, stockpile, roster health, and pace.
    /// Pure numbers — no SpecialistBrain changes.
    /// </summary>
    public struct OverseerRating
    {
        public int Total;
        public string Letter;
        public int Dens;
        public int Sustain;
        public int Launch;
        public int Economy;
        public int Roster;
        public int Pace;

        public string Summary => $"RATING {Letter}  ·  {Total}";
        public string Breakdown =>
            $"dens {Dens}  ·  hold {Sustain}  ·  launch {Launch}  ·  stock {Economy}  ·  roster {Roster}  ·  pace {Pace}";
    }

    public struct OverseerScoreInput
    {
        public bool DensCleared;
        public int UnclearedLairs;
        public int LairCount;
        public bool SustainComplete;
        public float Sustain01;
        public bool LaunchReady;
        public int Metals;
        public int Ice;
        public int PowerGen;
        public int PowerDraw;
        public int RobotCount;
        public float MeanHealth;
        public float MissionElapsed;
        public bool GatesMet;
        public int RevivePenalty;
    }

    public static class OverseerScore
    {
        public static OverseerRating Evaluate(in OverseerScoreInput i)
        {
            int dens = 0;
            if (i.LairCount <= 0)
                dens = i.DensCleared ? 22 : 8;
            else if (i.DensCleared)
                dens = 25;
            else
                dens = Mathf.RoundToInt(22f * (1f - Mathf.Clamp01(i.UnclearedLairs / (float)Mathf.Max(1, i.LairCount))));

            int sustain = i.SustainComplete
                ? 25
                : Mathf.RoundToInt(22f * Mathf.Clamp01(i.Sustain01));

            int launch = i.LaunchReady ? 15 : 0;

            float stock = Mathf.Clamp01(i.Metals / 220f) * 0.55f + Mathf.Clamp01(i.Ice / 80f) * 0.25f;
            float pwr = i.PowerDraw <= 0 ? 1f : Mathf.Clamp01((i.PowerGen + 2f) / (i.PowerDraw + 2f));
            int economy = Mathf.RoundToInt(15f * Mathf.Clamp01(stock * 0.7f + pwr * 0.3f));

            int roster = Mathf.RoundToInt(
                12f * Mathf.Clamp01(i.RobotCount / 4f) * Mathf.Clamp01(i.MeanHealth));

            int pace = 0;
            if (i.GatesMet)
            {
                if (i.MissionElapsed <= 480f) pace = 10;
                else if (i.MissionElapsed <= 720f) pace = 7;
                else if (i.MissionElapsed <= 960f) pace = 4;
                else pace = 2;
            }
            else if (i.MissionElapsed > 60f)
                pace = Mathf.Clamp(6 - Mathf.FloorToInt(i.MissionElapsed / 240f), 0, 6);

            int total = Mathf.Clamp(
                dens + sustain + launch + economy + roster + pace - Mathf.Max(0, i.RevivePenalty),
                0, 100);
            string letter = total >= 90 ? "S" : total >= 75 ? "A" : total >= 60 ? "B" : total >= 45 ? "C" : "D";
            // S wants gates + pace + a living roster. Uncleared dens cannot buy S from stockpile.
            if (letter == "S" &&
                (!i.DensCleared || !i.GatesMet || i.RobotCount < 3 || i.MeanHealth < 0.55f || i.MissionElapsed > 720f))
                letter = "A";

            return new OverseerRating
            {
                Total = total,
                Letter = letter,
                Dens = dens,
                Sustain = sustain,
                Launch = launch,
                Economy = economy,
                Roster = roster,
                Pace = pace
            };
        }
    }
}
