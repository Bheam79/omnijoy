using FluentAssertions;
using Omnijoy.Core.Services;

namespace Omnijoy.Tests.Services;

public class MentionParserTests
{
    [Fact]
    public void Parse_RecognizesHandlesAtTextAndPunctuationBoundaries()
    {
        var result = MentionParser.Parse("@Alice, meet (@bob_user)! Then ask #@carol-2.");

        result.Slugs.Should().Equal("alice", "bob_user", "carol-2");
        result.ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Parse_IgnoresEmailsAndWordInternalAtSigns()
    {
        var result = MentionParser.Parse(
            "alice@example.com prefix@bob prefix_@carol café@delta @@echo");

        result.Slugs.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NormalizesCaseAndDeduplicatesInFirstOccurrenceOrder()
    {
        var result = MentionParser.Parse("@BOB then @alice and @Bob and @ALICE");

        result.Slugs.Should().Equal("bob", "alice");
    }

    [Fact]
    public void Parse_UsesSlugGrammarRatherThanDisplayNames()
    {
        var tooLong = new string('a', SlugValidator.MaxLength + 1);
        var result = MentionParser.Parse(
            $"@ab @1alice @alice--smith @bob_ @admin @{tooLong} @Alice Smith");

        // "Smith" is plain text; only the valid @Alice slug is extracted.
        result.Slugs.Should().Equal("alice");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ordinary text")]
    [InlineData("@éloise")]
    [InlineData("@aliceé")]
    public void Parse_ContentWithoutAValidHandle_ReturnsEmpty(string? content)
    {
        MentionParser.Parse(content).Slugs.Should().BeEmpty();
    }

    [Fact]
    public void Parse_TenDistinctHandles_IsWithinLimit()
    {
        var content = string.Join(' ', Enumerable.Range(0, 10).Select(i => $"@user{i}"));

        var result = MentionParser.Parse(content);

        result.Slugs.Should().HaveCount(MentionParser.MaxDistinctMentions);
        result.ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Parse_ElevenDistinctHandles_IsNotTruncatedAndExceedsLimit()
    {
        var content = string.Join(' ', Enumerable.Range(0, 11).Select(i => $"@user{i}"));

        var result = MentionParser.Parse(content);

        result.Slugs.Should().HaveCount(11);
        result.ExceedsLimit.Should().BeTrue();
    }

    [Fact]
    public void Parse_RepeatedAndInvalidHandles_DoNotConsumeLimit()
    {
        var valid = string.Join(' ', Enumerable.Range(0, 10).Select(i => $"@user{i}"));

        var result = MentionParser.Parse($"{valid} @USER0 @ab @user1");

        result.Slugs.Should().HaveCount(10);
        result.ExceedsLimit.Should().BeFalse();
    }
}
