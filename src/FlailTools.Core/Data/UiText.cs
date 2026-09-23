namespace FlailTools.Core.Data;

/// <summary>
/// Every word the interface says that is not a table entry.
/// </summary>
/// <remarks>
/// <para>
/// Kept in data rather than in markup so that adding a language is a matter of supplying files, and
/// so that a missing string is a findable gap rather than a sentence buried in a component.
/// </para>
/// <para>
/// The same source-generator caveat applies here as to the table files: an absent property arrives
/// as null whatever initialiser is written, so every getter coerces.
/// </para>
/// </remarks>
public sealed record UiText
{
    public AppText App { get => field ?? new AppText(); init; } = new();

    public LicenceText Licence { get => field ?? new LicenceText(); init; } = new();

    /// <summary>What each site kind is called, keyed by its id.</summary>
    public IReadOnlyDictionary<string, string> Kinds { get => field ?? Empty; init; } = Empty;

    /// <summary>What one numbered part of a site is called, keyed by the kind it belongs to.</summary>
    /// <remarks>
    /// A dungeon has rooms, a cave has chambers and a tower has floors. The map legend needs the
    /// right noun and the generator should not be the thing that knows it.
    /// </remarks>
    public IReadOnlyDictionary<string, string> AreaNames { get => field ?? Empty; init; } = Empty;

    /// <summary>
    /// What each rolled field is called, keyed by <strong>field path</strong>.
    /// </summary>
    /// <remarks>
    /// Keyed by path rather than by a separate label id so that the set of things needing a label is
    /// exactly the set of things that get rolled. A new axis without a label is then a visible hole
    /// rather than an unnoticed one.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Labels { get => field ?? Empty; init; } = Empty;

    /// <summary>What each structural area role is called, keyed by its id.</summary>
    public IReadOnlyDictionary<string, string> Roles { get => field ?? Empty; init; } = Empty;

    /// <summary>Button and control wording, keyed by id.</summary>
    public IReadOnlyDictionary<string, string> Actions { get => field ?? Empty; init; } = Empty;

    /// <summary>Everything else the interface says, keyed by id.</summary>
    public IReadOnlyDictionary<string, string> Messages { get => field ?? Empty; init; } = Empty;

    /// <summary>The label for a field path, falling back to the path so a gap is visible, not blank.</summary>
    public string LabelFor(string path) => Labels.TryGetValue(path, out string? label) && label.Length > 0
        ? label
        : path;

    public string KindName(string kind) => Kinds.TryGetValue(kind, out string? name) && name.Length > 0
        ? name
        : kind;

    /// <summary>What one part of this kind of site is called: a room, a chamber, a floor.</summary>
    public string AreaName(string kind) => AreaNames.TryGetValue(kind, out string? name) && name.Length > 0
        ? name
        : "";

    public string RoleName(string role) => Roles.TryGetValue(role, out string? name) && name.Length > 0
        ? name
        : "";

    public string Action(string id) => Actions.TryGetValue(id, out string? text) ? text : id;

    public string Message(string id) => Messages.TryGetValue(id, out string? text) ? text : id;

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>The shell: what the tool is called and what it says it is.</summary>
public sealed record AppText
{
    public string Title { get => field ?? ""; init; } = "";

    public string Tagline { get => field ?? ""; init; } = "";

    public string Description { get => field ?? ""; init; } = "";
}

/// <summary>
/// The licence notices as they are rendered, markup and all.
/// </summary>
/// <remarks>
/// The wording of these two is fixed by <see cref="Attribution"/> and checked by a test. What lives
/// here is only how they are marked up — chiefly so the publisher's name can be a link without an
/// anchor being hard-coded into a component.
/// <para>
/// The compatibility badge's wording sits here too, but for the opposite reason: section 1 of the
/// licence mandates the <em>logo</em> and says nothing at all about the text beside it, so there is
/// no canonical sentence for <see cref="Attribution"/> to hold. The file is what must not change,
/// and a pinned checksum in the artwork tests is what says so.
/// </para>
/// </remarks>
public sealed record LicenceText
{
    /// <summary>The independence notice. Must still read as <see cref="Attribution.IndependentNotice"/>.</summary>
    public string IndependentHtml { get => field ?? ""; init; } = "";

    /// <summary>The copyright notice. Must still read as <see cref="Attribution.CopyrightNotice"/>.</summary>
    public string CopyrightHtml { get => field ?? ""; init; } = "";

    /// <summary>The wording of the "unofficial" flag shown beside the tool's name.</summary>
    public string Unofficial { get => field ?? ""; init; } = "";

    /// <summary>What the about page says about where the words in the tables came from.</summary>
    public string ContentHtml { get => field ?? ""; init; } = "";

    /// <summary>The link text for the licence itself.</summary>
    public string LicenceLinkText { get => field ?? ""; init; } = "";

    /// <summary>
    /// The alternative text for the Games Omnivorous compatibility logo.
    /// </summary>
    /// <remarks>
    /// The logo is a lead-in rather than a stamp: it reads "FLAIL swings hard with", and the product
    /// name is set beside it as real text. So this says only what the picture says. Restating the
    /// product name here would make a screen reader announce it twice, and padding it out with
    /// "official" or "approved" would claim the one thing section 1 of the licence forbids a
    /// compatibility logo from suggesting.
    /// </remarks>
    public string CompatibleAlt { get => field ?? ""; init; } = "";

    /// <summary>The product name that completes the compatibility logo's sentence.</summary>
    /// <remarks>
    /// Deliberately not borrowed from the masthead title. The two read the same today, but the
    /// masthead is a heading somebody may one day shorten for space, and this is half of a sentence
    /// the publisher's artwork begins.
    /// </remarks>
    public string CompatibleWith { get => field ?? ""; init; } = "";
}
