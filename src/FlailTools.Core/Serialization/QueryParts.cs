namespace FlailTools.Core.Serialization;

/// <summary>
/// Splitting a query into its parameters, once, for everything that carries one.
/// </summary>
/// <remarks>
/// <para>
/// Both tools that hand out links need this and neither can afford its own copy: a link is the save
/// file, so two splitters that disagreed about an edge — a bare key, a second <c>=</c>, an empty
/// value — would mean the same address read two ways depending on which page it landed on.
/// </para>
/// <para>
/// Values come back exactly as they arrived. Unescaping here would be a bug rather than a
/// convenience: a packed list has to be split on its separators before its halves are unescaped, or
/// an escaped separator inside somebody's typed words would reappear and tear the list apart at the
/// wrong place.
/// </para>
/// </remarks>
internal static class QueryParts
{
    internal static Dictionary<string, string> Split(string? query)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(query))
        {
            return parameters;
        }

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            parameters[pair[..separator]] = pair[(separator + 1)..];
        }

        return parameters;
    }
}
