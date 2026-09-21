using System.Globalization;
using FlailTools.Core.Data;
using FlailTools.Core.Model;
using Structed.Inkwell.Generation;

namespace FlailTools.Core.Generation;

/// <summary>
/// One d6 of the stack, as it came to rest.
/// </summary>
/// <remarks>
/// A thrown die settles with one of its six faces uppermost and turned to one of four quarters, so
/// there are twenty-four ways for it to land and this holds which one happened. It matters because
/// a die in a stack does not show all of itself: the face on top is under the next storey, the face
/// beneath is on the one below, and only the remaining four are visible to anybody walking around
/// the tower.
/// </remarks>
internal readonly record struct StackedDie(int UpFace, int Rotation)
{
    /// <summary>The number this die shows to one of the stack's four sides.</summary>
    public int Side(int facade) => Ring(UpFace)[(facade + Rotation) % Towers.Facades];

    /// <summary>
    /// The four faces girding a die, in turning order.
    /// </summary>
    /// <remarks>
    /// Opposite faces of a d6 sum to seven, so whichever pair stands vertical is out of sight and
    /// the other two pairs are what the room can see. That depends only on which pair is vertical —
    /// a die showing 1 and a die showing 6 present the same four numbers, merely turned differently
    /// — which is why this keys off the lower of the two.
    /// </remarks>
    private static int[] Ring(int upFace) => Math.Min(upFace, Faces + 1 - upFace) switch
    {
        1 => [2, 3, 5, 4],
        2 => [1, 3, 6, 4],
        _ => [1, 2, 6, 5]
    };

    private const int Faces = 6;
}

/// <summary>A tower as its own procedure built it: what it rolled, and its floors from the ground up.</summary>
internal sealed record TowerStack(IReadOnlyList<SiteField> Fields, IReadOnlyList<SiteArea> Floors);

/// <summary>
/// FLAIL!'s Wizard Towers procedure, in the order the book gives it.
/// </summary>
/// <remarks>
/// <para>
/// The book builds a tower out of real dice. Roll four to six d6s, stack them without looking, and
/// balance a d4 on top; that stack <em>is</em> the tower, one die per floor. Then pick a façade —
/// one of the stack's four sides — and read the numbers facing you down it. Each is the kind of
/// room on that floor, and a further d4 per floor says which room of that kind it is. The d4 on top
/// is the top floor and reads from a table of its own.
/// </para>
/// <para>
/// Which is why this does not simply roll a d6 per floor, as an easier reading of the same
/// paragraph would. A stacked die hides the face it stands on and the face it stands under, so only
/// four of its six numbers are available to a façade at all, and turning the tower shows a
/// different four. Rolling a d6 per floor would build a stack that could not exist, and would throw
/// away the one choice the procedure actually offers.
/// </para>
/// <para>
/// The façade is therefore a field in its own right, and re-rolling it walks round the tower rather
/// than rebuilding it: the same dice, read from another side, are a different tower. A floor's lock
/// holds its die, which is the only thing about a floor that a façade cannot change.
/// </para>
/// <para>
/// What the book asks the referee to <em>write</em> — the wizard's name and spells, a couple of key
/// features per floor, a purpose for the top floor — is left to the reader, as every other
/// judgement in this tool is. What the book bounds with a number is rolled.
/// </para>
/// </remarks>
internal static class Towers
{
    /// <summary>The four sides of a stack, and so the four façades a tower has.</summary>
    internal const int Facades = 4;

    /// <summary>The die the book rolls for a floor's exact nature, and balances on top of the stack.</summary>
    private const int TopDie = 4;

    /// <summary>"Roll a handful of d6s (between four and six depending on how many levels you want)".</summary>
    private const int FewestDice = 4;
    private const int MostDice = 6;

    /// <summary>Six faces up, four quarter-turns each.</summary>
    private const int RestingStates = 6 * Facades;

    /// <summary>"Only high-level mages have their own towers".</summary>
    private const int LowestLevel = 6;
    private const int HighestLevel = 10;

    private const int FewestHitPoints = 10;
    private const int MostHitPoints = 20;

    private const int LeastMana = 20;
    private const int MostMana = 30;

    public static TowerStack Build(GameData data, RollContext roll)
    {
        int count = Rolls.Number(roll, FieldPaths.TowerFloorCount, FewestDice, MostDice);
        int facade = Rolls.Number(roll, FieldPaths.TowerFacade, 0, Facades - 1);

        List<SiteArea> floors = new(count + 1);
        int[] reading = new int[count];

        for (int index = 0; index < count; index++)
        {
            int face = Throw(roll, FieldPaths.TowerFloorDie(index)).Side(facade);
            reading[index] = face;

            floors.Add(new SiteArea
            {
                Number = index + 1,
                Path = FieldPaths.TowerFloorDie(index),
                Role = AreaRoles.Plain,
                Face = face,
                Value = Floor(data, roll, index, face)
            });
        }

        (int topIndex, string top) = Rolls.Face(
            roll, FieldPaths.TowerTopFloor, data.Tower.TopFloorTypes, sides: TopDie);

        floors.Add(new SiteArea
        {
            Number = count + 1,
            Path = FieldPaths.TowerTopFloor,
            Role = AreaRoles.Top,
            Face = topIndex + 1,
            Value = top
        });

        List<SiteField> fields =
        [
            Number(roll, FieldPaths.TowerWizardLevel, LowestLevel, HighestLevel),
            Number(roll, FieldPaths.TowerWizardHitPoints, FewestHitPoints, MostHitPoints),
            Number(roll, FieldPaths.TowerWizardMana, LeastMana, MostMana),
            new SiteField { Path = FieldPaths.TowerFacade, Value = Reading(reading) }
        ];

        return new TowerStack(fields, floors);
    }

    /// <summary>Throws one of the stack's dice and remembers how it landed.</summary>
    private static StackedDie Throw(RollContext roll, string path)
    {
        int state = Rolls.Number(roll, path, 0, RestingStates - 1);

        return new StackedDie(UpFace: (state / Facades) + 1, Rotation: state % Facades);
    }

    /// <summary>
    /// One floor: the kind of room the façade shows, then a d4 for which room of that kind.
    /// </summary>
    /// <remarks>
    /// The detail is rolled on its own stream, so the d4 a floor rolled is the d4 it keeps even when
    /// the tower is turned and the floor becomes a library instead of a laboratory. That is what
    /// happens at the table too: the façade decides which column you read the d4 against, not what
    /// the d4 said.
    /// </remarks>
    private static string Floor(GameData data, RollContext roll, int index, int face)
    {
        string type = Row(data.Tower.FloorTypes, face - 1);

        if (type.Length == 0)
        {
            return "";
        }

        IReadOnlyList<string> details = face - 1 < data.Tower.FloorDetails.Count
            ? data.Tower.FloorDetails[face - 1]
            : [];

        string detail = Rolls.Face(roll, FieldPaths.TowerFloorDetail(index), details, sides: TopDie).Value;

        return detail.Length == 0 ? type : $"{type}: {detail}";
    }

    /// <summary>A bounded number, shown as the field it is.</summary>
    private static SiteField Number(RollContext roll, string path, int min, int max) => new()
    {
        Path = path,
        Value = Rolls.Number(roll, path, min, max).ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    /// The façade, shown as what it actually is: the numbers facing you, ground floor first.
    /// </summary>
    /// <remarks>
    /// Named by its reading rather than by a compass point or a made-up label, because the stack has
    /// no orientation of its own and inventing one would be a word this tool put in the book's
    /// mouth. The numbers are also the useful thing: they are what a reader with the book open
    /// looks up, and what the map draws as pips.
    /// </remarks>
    private static string Reading(IReadOnlyList<int> faces) =>
        string.Join(" · ", faces.Select(face => face.ToString(CultureInfo.InvariantCulture)));

    private static string Row(IReadOnlyList<string> rows, int index) =>
        index >= 0 && index < rows.Count ? rows[index] : "";
}
