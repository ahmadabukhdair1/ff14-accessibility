using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace FF14Accessibility.Services;

/// <summary>
/// Shared reading of companion-chocobo rank and experience from
/// <c>UIState.Buddy.CompanionInfo</c> and the BuddyRank sheet.
/// Used by the hotkey announcement and by the Mitstreiter (<c>Buddy</c>) window
/// summary — one formula, no duplicated XP math.
/// </summary>
/// <remarks>
/// Mapping and painted-array caveats are documented in
/// <c>docs/game-api.md</c> (Begleit-Chocobo) and measured 2026-09-04.
/// </remarks>
internal static class ChocoboCompanionReading
{
    /// <summary>Live companion snapshot for speech.</summary>
    public readonly struct Snapshot
    {
        /// <summary>Companion name, or empty.</summary>
        public string Name { get; init; }

        /// <summary>Rank from CompanionInfo (0 = none / not started).</summary>
        public int Rank { get; init; }

        /// <summary>Stars next to the rank in the window.</summary>
        public int Stars { get; init; }

        /// <summary>XP progress within the current rank (live).</summary>
        public int CurrentXp { get; init; }

        /// <summary>
        /// Threshold to the next rank; 0 at rank cap; negative if unknown.
        /// </summary>
        public int NeededXp { get; init; }

        /// <summary>True when the painted BuddyNumberArray matched this rank.</summary>
        public bool ArrayFresh { get; init; }

        /// <summary>Painted array rank/current/max for logging.</summary>
        public int ArrayRank { get; init; }

        /// <summary>Painted current XP (stale when window closed).</summary>
        public int ArrayCur { get; init; }

        /// <summary>Painted max XP for the rank.</summary>
        public int ArrayMax { get; init; }
    }

    /// <summary>
    /// Reads name, rank, stars, live CurrentXP, and the next-rank threshold.
    /// Threshold: BuddyRank sheet row = rank; BuddyNumberArray.MaxExp only as
    /// cross-check when the array is fresh for this rank (never for CurrentXP).
    /// </summary>
    public static unsafe Snapshot Read(IDataManager data, IPluginLog log)
    {
        var empty = new Snapshot
        {
            Name = string.Empty,
            Rank = 0,
            Stars = 0,
            CurrentXp = 0,
            NeededXp = -1,
            ArrayFresh = false,
            ArrayRank = -1,
            ArrayCur = 0,
            ArrayMax = 0,
        };

        var ui = UIState.Instance();
        if (ui == null)
        {
            log.Warning("[Chocobo] UIState nicht verfuegbar.");
            return empty;
        }

        var companion = ui->Buddy.CompanionInfo;
        int rank = companion.Rank;
        int stars = companion.Stars;
        var name = companion.NameString ?? string.Empty;
        int current = (int)companion.CurrentXP;

        if (rank <= 0)
        {
            return new Snapshot
            {
                Name = name,
                Rank = rank,
                Stars = stars,
                CurrentXp = current,
                NeededXp = -1,
                ArrayFresh = false,
                ArrayRank = -1,
                ArrayCur = 0,
                ArrayMax = 0,
            };
        }

        var sheetRow = data.GetExcelSheet<Lumina.Excel.Sheets.BuddyRank>()?.GetRowOrDefault((uint)rank);
        int needed = sheetRow == null ? -1 : (int)sheetRow.Value.ExpRequired;

        var num = BuddyNumberArray.Instance();
        int arrayRank = num == null ? -1 : num->BuddyRank;
        int arrayCur = num == null ? 0 : num->CurrentExp;
        int arrayMax = num == null ? 0 : num->MaxExp;
        var fresh = num != null && arrayRank == rank && arrayMax > 0;

        if (fresh && needed > 0 && arrayMax != needed)
        {
            log.Warning($"[Chocobo] Schwelle uneinig: Sheet Zeile {rank} = {needed}, " +
                        $"Balken = {arrayMax}. Balken hat Vorrang.");
        }

        if (fresh)
            needed = arrayMax;

        return new Snapshot
        {
            Name = name,
            Rank = rank,
            Stars = stars,
            CurrentXp = current,
            NeededXp = needed,
            ArrayFresh = fresh,
            ArrayRank = arrayRank,
            ArrayCur = arrayCur,
            ArrayMax = arrayMax,
        };
    }
}
