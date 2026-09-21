using FlailTools.Core.Data;

namespace FlailTools.Core.Generation;

/// <summary>
/// Every field path this generator rolls, named once.
/// </summary>
/// <remarks>
/// <para>
/// A field path is not a label. It is hashed into the seed of that field's stream, so it decides
/// <em>what</em> a given seed rolls; and it is the key a lock is filed under in every shared link
/// and every export, so it decides whether a saved link still resolves. Renaming one does both at
/// once. They are declared here, in one place, and pinned by a test.
/// </para>
/// <para>
/// The governing rule is that <strong>one path maps to exactly one table, forever</strong>. A pin
/// stores a table position rather than words, so if two different tables ever shared a path, a pin
/// taken under one would resolve silently to an unrelated row of the other. That is why dungeons
/// and caves have separate paths despite sharing all five axis names, and why the two hexcrawl
/// generators do not share their Biome or Condition axes.
/// </para>
/// <para>
/// Conventions: a subject gets a top-level prefix rather than a strict containment tree, which
/// keeps paths short; a collection-level roll is plural (<c>dungeon/rooms/count</c>); a member is
/// singular with a <strong>zero-based</strong> index (<c>dungeon/room/0/stocking</c>); a set of
/// plain strings is one path rolled with <c>TextMany</c> rather than an indexed subtree.
/// </para>
/// </remarks>
public static class FieldPaths
{
    // The spine. Three fields, all of them ours rather than the book's, and all of them shared by
    // every kind so that they survive a change of kind.
    public const string NameStem = "site/name/stem";
    public const string NameTail = "site/name/tail";
    public const string Kind = "site/kind";
    public const string Silhouette = "site/silhouette";

    // Dungeons: Flavour, Type, Location, Key Feature, Creatures.
    public const string DungeonPrefix = "dungeon/";
    public const string DungeonFlavour = "dungeon/flavour";
    public const string DungeonType = "dungeon/type";
    public const string DungeonLocation = "dungeon/location";
    public const string DungeonKeyFeature = "dungeon/key-feature";
    public const string DungeonCreatures = "dungeon/creatures";
    public const string DungeonRoomCount = "dungeon/rooms/count";

    // Caves: the same five axes, over their own tables.
    public const string CavePrefix = "cave/";
    public const string CaveFlavour = "cave/flavour";
    public const string CaveType = "cave/type";
    public const string CaveLocation = "cave/location";
    public const string CaveKeyFeature = "cave/key-feature";
    public const string CaveCreatures = "cave/creatures";
    public const string CaveChamberCount = "cave/chambers/count";

    // Wizard towers: Shape, Occupant, Reaction and Goal, the wizard whose tower it is, how many
    // dice are stacked to make it, and which side of that stack is read as the front.
    public const string TowerPrefix = "tower/";
    public const string TowerShape = "tower/shape";
    public const string TowerOccupant = "tower/occupant";
    public const string TowerReaction = "tower/reaction";
    public const string TowerGoal = "tower/goal";

    /// <summary>The wizard's level, which FLAIL! bounds rather than tabulates.</summary>
    public const string TowerWizardLevel = "tower/wizard/level";

    public const string TowerWizardHitPoints = "tower/wizard/hit-points";

    public const string TowerWizardMana = "tower/wizard/mana";

    /// <summary>How many d6s go into the stack. In the book the referee chooses; here it is rolled.</summary>
    public const string TowerFloorCount = "tower/floors/count";

    /// <summary>Which of the stack's four sides is read as the front of the tower.</summary>
    public const string TowerFacade = "tower/facade";

    /// <summary>
    /// The d4 balanced on top of the stack.
    /// </summary>
    /// <remarks>
    /// Not indexed with the floors below it, although it is drawn above them and numbered after
    /// them. The top floor is one particular die rather than the last of a run, so its lock should
    /// survive the stack gaining or losing a storey — which an indexed path could not promise.
    /// </remarks>
    public const string TowerTopFloor = "tower/top-floor";

    // Hexcrawl locations: Location, Biome, Condition, Key Feature, Occupant.
    public const string LocationPrefix = "location/";
    public const string LocationLocation = "location/location";
    public const string LocationBiome = "location/biome";
    public const string LocationCondition = "location/condition";
    public const string LocationKeyFeature = "location/key-feature";
    public const string LocationOccupant = "location/occupant";

    // Hexcrawl landmarks: Landmark, Biome, Condition, Key Feature, Occupant.
    public const string LandmarkPrefix = "landmark/";
    public const string LandmarkLandmark = "landmark/landmark";
    public const string LandmarkBiome = "landmark/biome";
    public const string LandmarkCondition = "landmark/condition";
    public const string LandmarkKeyFeature = "landmark/key-feature";
    public const string LandmarkOccupant = "landmark/occupant";

    /// <summary>
    /// The prefixes a change of kind invalidates, cleared together when the kind is re-rolled.
    /// </summary>
    /// <remarks>
    /// Without this, a reader who rolled a dungeon, locked its Key Feature and then re-rolled the
    /// kind would carry a dead lock into a cave, where it would resolve against the cave's own Key
    /// Feature table by position and produce a row nobody chose.
    /// </remarks>
    public static IReadOnlyList<string> BranchPrefixes { get; } =
        [DungeonPrefix, CavePrefix, TowerPrefix, LocationPrefix, LandmarkPrefix];

    /// <summary>The name's two halves, which are locked and re-rolled as one field.</summary>
    public static IReadOnlyList<string> NameParts { get; } = [NameStem, NameTail];

    /// <summary>The stream a dungeon room's keying concept is picked from.</summary>
    public static string DungeonRoom(int index) => $"dungeon/room/{index}/stocking";

    /// <summary>The stream a cave chamber's contents are rolled from.</summary>
    public static string CaveChamber(int index) => $"cave/chamber/{index}/chamber";

    /// <summary>
    /// The stream a cave chamber's die-landing is drawn from.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="CaveChamber"/> so that locking what is in a chamber does not also
    /// pin where it sits, and — more importantly — so that locking it does not let the rest of the
    /// drop shift around it. Where the dice land decides which chamber is the way in and which is
    /// the heart of the place, and that should not move because somebody liked one room.
    /// </remarks>
    public static string CaveDrop(int index) => $"cave/chamber/{index}/drop";

    /// <summary>
    /// The stream one of the stack's d6s is thrown on.
    /// </summary>
    /// <remarks>
    /// The die, not the floor. FLAIL! stacks the dice without looking and only afterwards picks a
    /// façade to read them from, so what is on a given floor is the die <em>and</em> the façade
    /// together. Pinning the die is therefore what pinning a floor can honestly mean: it holds that
    /// storey's die still while the tower is turned around it.
    /// </remarks>
    public static string TowerFloorDie(int index) => $"tower/floor/{index}/die";

    /// <summary>
    /// The stream a tower floor's d4 detail is drawn from.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="TowerFloorDie"/> because the book rolls it separately: locking a
    /// floor to be a library should not also decide which library it is, and re-rolling the detail
    /// should not be able to turn the library into a laboratory.
    /// </remarks>
    public static string TowerFloorDetail(int index) => $"tower/floor/{index}/detail";

    /// <summary>The branch prefix a kind's fields live under.</summary>
    public static string PrefixFor(string kind) => kind switch
    {
        SiteKinds.Dungeon => DungeonPrefix,
        SiteKinds.Cave => CavePrefix,
        SiteKinds.Tower => TowerPrefix,
        SiteKinds.Location => LocationPrefix,
        SiteKinds.Landmark => LandmarkPrefix,
        _ => ""
    };

    /// <summary>The axis paths a kind rolls, in the order they are shown.</summary>
    /// <remarks>
    /// Only the paths read off a printed table. A tower rolls more than these — its wizard and its
    /// façade are numbers and choices rather than table rows — and those are added by the tower's
    /// own procedure, which is the only thing that knows how to roll them.
    /// </remarks>
    public static IReadOnlyList<string> AxesFor(string kind) => kind switch
    {
        SiteKinds.Dungeon => [DungeonFlavour, DungeonType, DungeonLocation, DungeonKeyFeature, DungeonCreatures],
        SiteKinds.Cave => [CaveFlavour, CaveType, CaveLocation, CaveKeyFeature, CaveCreatures],
        SiteKinds.Tower => [TowerShape, TowerOccupant, TowerReaction, TowerGoal],
        SiteKinds.Location => [LocationLocation, LocationBiome, LocationCondition, LocationKeyFeature, LocationOccupant],
        SiteKinds.Landmark => [LandmarkLandmark, LandmarkBiome, LandmarkCondition, LandmarkKeyFeature, LandmarkOccupant],
        _ => []
    };

    /// <summary>Every fixed path, for the inventory test to compare against a committed list.</summary>
    public static IReadOnlyList<string> Fixed { get; } =
    [
        NameStem,
        NameTail,
        Kind,
        Silhouette,
        DungeonFlavour,
        DungeonType,
        DungeonLocation,
        DungeonKeyFeature,
        DungeonCreatures,
        DungeonRoomCount,
        CaveFlavour,
        CaveType,
        CaveLocation,
        CaveKeyFeature,
        CaveCreatures,
        CaveChamberCount,
        TowerShape,
        TowerOccupant,
        TowerReaction,
        TowerGoal,
        TowerWizardLevel,
        TowerWizardHitPoints,
        TowerWizardMana,
        TowerFloorCount,
        TowerFacade,
        TowerTopFloor,
        LocationLocation,
        LocationBiome,
        LocationCondition,
        LocationKeyFeature,
        LocationOccupant,
        LandmarkLandmark,
        LandmarkBiome,
        LandmarkCondition,
        LandmarkKeyFeature,
        LandmarkOccupant
    ];
}
