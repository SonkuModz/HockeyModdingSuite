namespace NHL27Kit.Core;

/// <summary>
/// Where a player was drafted into the NHL, part of a missing player's <see cref="PlayerBio"/>. NHL players' come from
/// the NHL API's player landing page (draftDetails), AHL players' from the AHL feed's player view (info.drafts). Team
/// is a 3-letter NHL abbreviation when the name is known (see NhlApi.TeamAbbr), else the name as the source wrote it.
/// Overall is the overall pick number. A player with no NHL draft is <see cref="Undrafted"/>.
/// </summary>
public sealed record DraftInfo(int Year, string Team, int Round, int Overall)
{
    public static readonly DraftInfo Undrafted = new(0, "", 0, 0);

    public bool IsUndrafted => Year == 0;

    /// <summary>"drafted 2023 MIN, round 1 (#21)", or "undrafted".</summary>
    public string Text => IsUndrafted ? "undrafted" : $"drafted {Year} {Team}, round {Round}" + (Overall > 0 ? $" (#{Overall})" : "");
}
