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

public class CommentReactionModelTests
{
    private static OmnijoyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Model_UsesUniqueCommentUserIndexAndExpectedRelationships()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CommentReaction))!;

        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CommentReaction.CommentId), nameof(CommentReaction.UserId) }));

        var commentForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Comment));
        commentForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        commentForeignKey.PrincipalToDependent!.Name.Should().Be(nameof(Comment.Reactions));

        var userForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User));
        userForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        userForeignKey.PrincipalToDependent!.Name.Should().Be(nameof(User.CommentReactions));
    }

    [Fact]
    public void Entity_StoresReactionFieldsAndNavigations()
    {
        var id = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var comment = new Comment();
        var user = new User();
        var reaction = new CommentReaction
        {
            Id = id,
            CommentId = commentId,
            UserId = userId,
            ReactionType = ReactionType.Love,
            CreatedAt = createdAt,
            Comment = comment,
            User = user,
        };

        reaction.Id.Should().Be(id);
        reaction.CommentId.Should().Be(commentId);
        reaction.UserId.Should().Be(userId);
        reaction.ReactionType.Should().Be(ReactionType.Love);
        reaction.CreatedAt.Should().Be(createdAt);
        reaction.Comment.Should().BeSameAs(comment);
        reaction.User.Should().BeSameAs(user);
        comment.Reactions.Should().BeEmpty();
        user.CommentReactions.Should().BeEmpty();
    }

    [Fact]
    public void Migration_CreatesUniqueIndexAndRestrictsCommentDeletion()
    {
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        new TestableAddCommentReactions().ApplyUp(builder);

        var table = builder.Operations.OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "CommentReactions");
        table.Columns.Select(column => column.Name).Should().Contain(new[]
        {
            nameof(CommentReaction.Id),
            nameof(CommentReaction.CommentId),
            nameof(CommentReaction.UserId),
            nameof(CommentReaction.ReactionType),
            nameof(CommentReaction.CreatedAt),
        });
        table.ForeignKeys.Single(foreignKey => foreignKey.PrincipalTable == "Comments")
            .OnDelete.Should().Be(ReferentialAction.Restrict);
        table.ForeignKeys.Single(foreignKey => foreignKey.PrincipalTable == "Users")
            .OnDelete.Should().Be(ReferentialAction.Cascade);

        builder.Operations.OfType<CreateIndexOperation>()
            .Should().Contain(index =>
                index.Table == "CommentReactions"
                && index.IsUnique
                && index.Columns.SequenceEqual(new[]
                {
                    nameof(CommentReaction.CommentId),
                    nameof(CommentReaction.UserId),
                }));
    }

    private sealed class TestableAddCommentReactions : AddCommentReactions
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => base.Up(migrationBuilder);
    }
}
