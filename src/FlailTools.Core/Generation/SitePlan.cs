using Structed.Inkwell.Generation;

namespace FlailTools.Core.Generation;

/// <summary>
/// The seed, the locks and the one setting a generated site is a pure function of.
/// </summary>
/// <remarks>
/// Deriving from <see cref="RollPlan"/> rather than reusing it directly is what lets the
/// <c>with</c> helpers below return this type while the pin and re-roll bookkeeping stays in the
/// engine, where it is identical for every generator.
/// </remarks>
public sealed record SitePlan : RollPlan
{
    /// <summary>
    /// The kind to generate, or <c>null</c> to roll for one.
    /// </summary>
    /// <remarks>
    /// A setting rather than a pin. A pin holds a row of a table against a re-roll; this decides
    /// which tables are consulted at all, and so has to be readable before any rolling starts.
    /// </remarks>
    public string? Kind { get; init; }

    public SitePlan WithSeed(uint seed) => this with { Seed = seed };

    /// <summary>Locks a path to the row it landed on.</summary>
    public SitePlan WithPin(string path, string pinValue) =>
        this with { Pins = PinsWith(path, pinValue) };

    /// <summary>Releases a lock.</summary>
    public SitePlan WithoutPin(string path) => this with { Pins = PinsWithout(path) };

    /// <summary>
    /// Re-rolls one path, clearing its lock in the same step.
    /// </summary>
    /// <remarks>
    /// A pinned field would ignore the new roll, so re-rolling a path that is still locked would
    /// look to the reader like a button that does nothing.
    /// </remarks>
    public SitePlan WithReroll(string path) =>
        this with { Pins = PinsWithout(path), Rerolls = RerollsWith(path) };

    /// <summary>Locks both halves of the name, which the interface presents as one field.</summary>
    public SitePlan WithNamePinned(IReadOnlyDictionary<string, string> pinValues)
    {
        SitePlan plan = this;
        foreach (string path in FieldPaths.NameParts)
        {
            if (pinValues.TryGetValue(path, out string? pinValue))
            {
                plan = plan.WithPin(path, pinValue);
            }
        }

        return plan;
    }

    /// <summary>Releases both halves of the name.</summary>
    public SitePlan WithNameUnpinned()
    {
        SitePlan plan = this;
        foreach (string path in FieldPaths.NameParts)
        {
            plan = plan.WithoutPin(path);
        }

        return plan;
    }

    /// <summary>Re-rolls both halves of the name together.</summary>
    public SitePlan WithNameRerolled()
    {
        SitePlan plan = this;
        foreach (string path in FieldPaths.NameParts)
        {
            plan = plan.WithReroll(path);
        }

        return plan;
    }

    /// <summary><c>true</c> when the name is held fixed, which needs both halves locked.</summary>
    public bool IsNamePinned => FieldPaths.NameParts.All(IsPinned);

    /// <summary>
    /// Changes the kind, dropping every lock belonging to the branch being left behind.
    /// </summary>
    /// <remarks>
    /// The abandoned locks cannot simply be carried: a pin is a table position, so a Key Feature
    /// locked on a dungeon would resolve by position against a cave's Key Feature table and produce
    /// a row nobody chose. The spine's locks — the name and the silhouette — are kept, because those
    /// tables are shared across every kind and a reader who liked the name should keep it.
    /// </remarks>
    public SitePlan WithKind(string? kind) =>
        this with { Kind = kind, Pins = PinsWithoutBranches() };

    /// <summary>Re-rolls the kind itself, which likewise abandons the old branch's locks.</summary>
    public SitePlan WithKindRerolled() =>
        this with
        {
            Kind = null,
            Pins = PinsWithoutBranches(),
            Rerolls = RerollsWith(FieldPaths.Kind)
        };

    /// <summary>
    /// The pins with every branch path removed.
    /// </summary>
    /// <remarks>
    /// The engine's <c>PinsWithoutPrefix</c> handles one prefix at a time and always reads the
    /// current pins, so it cannot be chained across the five branches in one step.
    /// </remarks>
    private IReadOnlyDictionary<string, string> PinsWithoutBranches()
    {
        Dictionary<string, string> pins = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> pin in Pins)
        {
            bool isBranch = FieldPaths.BranchPrefixes
                .Any(prefix => pin.Key.StartsWith(prefix, StringComparison.Ordinal));

            if (!isBranch)
            {
                pins[pin.Key] = pin.Value;
            }
        }

        return pins;
    }
}
