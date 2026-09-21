using System.Text;
using FlailTools.Core.Data;
using FlailTools.Core.Generation;
using FlailTools.Core.Tests.Golden;

namespace FlailTools.Core.Tests;

/// <summary>
/// Field paths are the most expensive thing in this project to change, so they are pinned.
/// </summary>
/// <remarks>
/// <para>
/// A path is hashed into the seed of its own stream, so it decides what a given seed rolls; and it
/// is the key a lock is filed under in every shared link and every export, so it decides whether an
/// old link still resolves. Renaming one silently changes what existing seeds produce and orphans
/// every lock saved against the old name — with no error at either end.
/// </para>
/// <para>
/// The committed inventory is therefore not a formality. A diff to it is a diff to every link
/// anybody has ever shared.
/// </para>
/// </remarks>
public sealed class FieldPathTests
{
    [Fact]
    public void TheInventoryMatchesTheCommittedList()
    {
        StringBuilder inventory = new();

        foreach (string path in FieldPaths.Fixed)
        {
            inventory.Append(path).Append('\n');
        }

        GoldenFile.Verify("field-paths.txt", inventory.ToString());
    }

    [Fact]
    public void NoPathIsDeclaredTwice()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string path in FieldPaths.Fixed)
        {
            Assert.True(seen.Add(path), $"'{path}' is declared more than once.");
        }
    }

    [Fact]
    public void EveryPathIsLowerCaseAndSlashSeparated()
    {
        foreach (string path in FieldPaths.Fixed)
        {
            Assert.Equal(path.ToLowerInvariant(), path);
            Assert.DoesNotContain(' ', path);
            Assert.DoesNotContain('_', path);
            Assert.False(path.StartsWith('/') || path.EndsWith('/'), $"'{path}' has a stray slash.");
        }
    }

    [Fact]
    public void EveryKindRollsAtLeastOneAxisAndOnlyItsOwn()
    {
        foreach (string kind in SiteKinds.All)
        {
            IReadOnlyList<string> axes = FieldPaths.AxesFor(kind);

            Assert.NotEmpty(axes);

            foreach (string axis in axes)
            {
                Assert.StartsWith(FieldPaths.PrefixFor(kind), axis, StringComparison.Ordinal);
                Assert.Contains(axis, FieldPaths.Fixed, StringComparer.Ordinal);
            }
        }
    }

    [Fact]
    public void NoTwoKindsShareAnAxis()
    {
        Dictionary<string, string> owner = new(StringComparer.Ordinal);

        foreach (string kind in SiteKinds.All)
        {
            foreach (string axis in FieldPaths.AxesFor(kind))
            {
                Assert.False(
                    owner.TryGetValue(axis, out string? already),
                    $"'{axis}' is rolled by both '{already}' and '{kind}'. A lock stores a row's " +
                    "position, so a shared path would let a lock taken on one resolve against the " +
                    "other's table and produce a row nobody chose.");

                owner[axis] = kind;
            }
        }
    }

    [Fact]
    public void EveryBranchPrefixBelongsToAKind()
    {
        foreach (string prefix in FieldPaths.BranchPrefixes)
        {
            Assert.Contains(prefix, SiteKinds.All.Select(FieldPaths.PrefixFor), StringComparer.Ordinal);
        }
    }

    [Fact]
    public void IndexedPathsAreDistinctAndStayInTheirBranch()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int index = 0; index < 16; index++)
        {
            Assert.True(seen.Add(FieldPaths.DungeonRoom(index)));
            Assert.True(seen.Add(FieldPaths.CaveChamber(index)));
            Assert.True(seen.Add(FieldPaths.TowerFloorDie(index)));

            Assert.StartsWith(FieldPaths.DungeonPrefix, FieldPaths.DungeonRoom(index), StringComparison.Ordinal);
            Assert.StartsWith(FieldPaths.CavePrefix, FieldPaths.CaveChamber(index), StringComparison.Ordinal);
            Assert.StartsWith(FieldPaths.TowerPrefix, FieldPaths.TowerFloorDie(index), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EveryPathHasSomethingToCallItself()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string path in FieldPaths.Fixed)
        {
            Assert.True(
                data.Ui.Labels.ContainsKey(path),
                $"'{path}' has no label in '{DataPaths.Ui}', so the interface would show the raw path.");
        }
    }

    [Fact]
    public async Task EveryLabelBelongsToAPathThatIsActuallyRolled()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string path in data.Ui.Labels.Keys)
        {
            Assert.Contains(path, FieldPaths.Fixed, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task EveryKindAndRoleHasSomethingToCallItself()
    {
        GameData data = await TestData.LoadAsync();

        foreach (string kind in SiteKinds.All)
        {
            Assert.True(data.Ui.Kinds.ContainsKey(kind), $"'{kind}' has no name in '{DataPaths.Ui}'.");
        }

        foreach (string role in AllRoles)
        {
            Assert.True(data.Ui.Roles.ContainsKey(role), $"The role '{role}' has no name in '{DataPaths.Ui}'.");
        }
    }

    private static IReadOnlyList<string> AllRoles { get; } =
    [
        Model.AreaRoles.Plain,
        Model.AreaRoles.Entrance,
        Model.AreaRoles.Finale,
        Model.AreaRoles.Entry,
        Model.AreaRoles.Core,
        Model.AreaRoles.Hidden,
        Model.AreaRoles.Cluster,
        Model.AreaRoles.Top
    ];
}
