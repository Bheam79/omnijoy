using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;
using Omnijoy.Infrastructure.Migrations;

namespace Omnijoy.Tests.Services;

public class UserFollowModelTests
{
    private static OmnijoyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OmnijoyDbContext(options);
    }

    [Fact]
    public void UserFollow_UsesUniqueDirectedPairAndPagedQueryIndexes()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(UserFollow))!;

        entity.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(nameof(UserFollow.FollowerId), nameof(UserFollow.FolloweeId));

        var indexes = entity.GetIndexes()
            .Select(index => index.Properties.Select(property => property.Name).ToArray())
            .ToList();
        indexes.Should().ContainEquivalentOf(new[]
        {
            nameof(UserFollow.FollowerId),
            nameof(UserFollow.CreatedAt),
            nameof(UserFollow.FolloweeId),
        });
        indexes.Should().ContainEquivalentOf(new[]
        {
            nameof(UserFollow.FolloweeId),
            nameof(UserFollow.CreatedAt),
            nameof(UserFollow.FollowerId),
        });
    }

    [Fact]
    public void UserFollow_CascadesCleanupForEitherDeletedUser()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(UserFollow))!;

        entity.GetForeignKeys().Should().HaveCount(2)
            .And.OnlyContain(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        entity.GetForeignKeys().Single(foreignKey =>
                foreignKey.Properties.Single().Name == nameof(UserFollow.FollowerId))
            .PrincipalToDependent!.Name.Should().Be(nameof(User.Following));
        entity.GetForeignKeys().Single(foreignKey =>
                foreignKey.Properties.Single().Name == nameof(UserFollow.FolloweeId))
            .PrincipalToDependent!.Name.Should().Be(nameof(User.Followers));
    }

    [Fact]
    public void FollowCountsAndFollowerVisibility_HaveSafeDefaults()
    {
        using var db = CreateContext();
        var userEntity = db.Model.FindEntityType(typeof(User))!;
        var privacyEntity = db.Model.FindEntityType(typeof(UserPrivacySettings))!;
        var user = new User();
        var privacy = new UserPrivacySettings();

        user.FollowersCount.Should().Be(0);
        user.FollowingCount.Should().Be(0);
        userEntity.FindProperty(nameof(User.FollowersCount))!.GetDefaultValue().Should().Be(0);
        userEntity.FindProperty(nameof(User.FollowingCount))!.GetDefaultValue().Should().Be(0);

        privacy.WhoCanSeeFollowers.Should().Be(privacy.WhoCanSeeFriendList)
            .And.Be(PrivacyLevel.Friends);
        privacyEntity.FindProperty(nameof(UserPrivacySettings.WhoCanSeeFollowers))!
            .GetDefaultValue().Should().Be(PrivacyLevel.Friends);
    }

    [Fact]
    public void AddUserFollowsMigration_AddsDefaultsUniquePairAndCascadeForeignKeys()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        new TestableAddUserFollows().ApplyUp(builder);

        var addedColumns = builder.Operations.OfType<AddColumnOperation>().ToList();
        addedColumns.Single(column => column.Name == nameof(User.FollowersCount))
            .DefaultValue.Should().Be(0);
        addedColumns.Single(column => column.Name == nameof(User.FollowingCount))
            .DefaultValue.Should().Be(0);
        addedColumns.Single(column => column.Name == nameof(UserPrivacySettings.WhoCanSeeFollowers))
            .DefaultValue.Should().Be("Friends");

        var table = builder.Operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "UserFollows");
        table.PrimaryKey!.Columns.Should()
            .Equal(nameof(UserFollow.FollowerId), nameof(UserFollow.FolloweeId));
        table.ForeignKeys.Should().HaveCount(2)
            .And.OnlyContain(foreignKey => foreignKey.OnDelete == ReferentialAction.Cascade);

        builder.Operations.OfType<CreateIndexOperation>()
            .Where(index => index.Table == "UserFollows")
            .Select(index => index.Columns)
            .Should().ContainEquivalentOf(new[]
            {
                nameof(UserFollow.FolloweeId),
                nameof(UserFollow.CreatedAt),
                nameof(UserFollow.FollowerId),
            }).And.ContainEquivalentOf(new[]
            {
                nameof(UserFollow.FollowerId),
                nameof(UserFollow.CreatedAt),
                nameof(UserFollow.FolloweeId),
            });
    }

    private sealed class TestableAddUserFollows : AddUserFollows
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => base.Up(migrationBuilder);
    }
}
