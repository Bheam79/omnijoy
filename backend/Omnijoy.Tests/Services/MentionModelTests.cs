using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Omnijoy.Core.Models;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Tests.Services;

public class MentionModelTests
{
    private static OmnijoyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OmnijoyDbContext(options);
    }

    [Theory]
    [InlineData(typeof(PostMention), "PostId")]
    [InlineData(typeof(CommentMention), "CommentId")]
    public void MentionConfiguration_UsesUniqueParentAndUserCompositeKey(
        Type mentionType,
        string parentIdProperty)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(mentionType)!;

        entity.FindPrimaryKey()!.Properties.Select(property => property.Name)
            .Should().Equal(parentIdProperty, nameof(PostMention.MentionedUserId));
        entity.FindProperty(nameof(PostMention.MatchedSlug))!.GetMaxLength().Should().Be(30);
        entity.FindProperty(nameof(PostMention.MatchedSlug))!.IsNullable.Should().BeFalse();
        entity.FindProperty(nameof(PostMention.CreatedAt))!.IsNullable.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(PostMention), typeof(Post))]
    [InlineData(typeof(PostMention), typeof(User))]
    [InlineData(typeof(CommentMention), typeof(Comment))]
    [InlineData(typeof(CommentMention), typeof(User))]
    public void MentionConfiguration_CascadesCleanupFromParentsAndMentionedUsers(
        Type mentionType,
        Type principalType)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(mentionType)!;

        entity.GetForeignKeys().Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == principalType)
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void MentionEntities_KeepMatchedSlugSnapshotAndCreatedAt()
    {
        var createdAt = DateTime.UtcNow;
        var postMention = new PostMention { MatchedSlug = "old-slug", CreatedAt = createdAt };
        var commentMention = new CommentMention { MatchedSlug = "old-slug", CreatedAt = createdAt };

        postMention.MatchedSlug.Should().Be("old-slug");
        postMention.CreatedAt.Should().Be(createdAt);
        commentMention.MatchedSlug.Should().Be("old-slug");
        commentMention.CreatedAt.Should().Be(createdAt);
    }
}
