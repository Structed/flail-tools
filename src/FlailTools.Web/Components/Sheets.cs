using Microsoft.AspNetCore.Components;

namespace FlailTools.Web.Components;

/// <summary>Small things the sheet's inputs need and a markup file should not hold.</summary>
internal static class Sheets
{
    /// <summary>
    /// The number in a number box, or what was already there.
    /// </summary>
    /// <remarks>
    /// A number input hands back an empty string while somebody is part-way through clearing it to
    /// type a new figure. Reading that as zero would make the hit points flick to nothing under
    /// their cursor, which reads as a bug and, on a hit point box, an alarming one.
    /// </remarks>
    public static int Number(ChangeEventArgs args, int fallback) =>
        int.TryParse(args?.Value as string, out int value) ? value : fallback;
}
