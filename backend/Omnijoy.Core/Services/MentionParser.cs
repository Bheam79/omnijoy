namespace Omnijoy.Core.Services;

/// <summary>
/// The result of parsing mention handles from one piece of content.
/// Handles are canonical lowercase slugs in first-occurrence order and are
/// never truncated, so callers can reject over-limit content before saving it.
/// </summary>
public sealed record MentionParseResult(IReadOnlyList<string> Slugs)
{
    public bool ExceedsLimit => Slugs.Count > MentionParser.MaxDistinctMentions;
}

/// <summary>
/// Extracts <c>@slug</c> mentions without consulting persistence. Display
/// names are deliberately not recognised; every candidate must satisfy the
/// same grammar and reservation rules as a vanity URL slug.
/// </summary>
public static class MentionParser
{
    public const int MaxDistinctMentions = 10;

    public static MentionParseResult Parse(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new MentionParseResult(Array.Empty<string>());

        var slugs = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != '@' || !HasLeadingBoundary(content, index))
                continue;

            var candidateStart = index + 1;
            var candidateEnd = candidateStart;
            while (candidateEnd < content.Length && IsSlugCharacter(content[candidateEnd]))
                candidateEnd++;

            if (candidateEnd == candidateStart)
                continue;

            // Do not recognise an ASCII prefix of a larger Unicode word.
            if (candidateEnd < content.Length && IsWordCharacter(content[candidateEnd]))
                continue;

            var candidate = content[candidateStart..candidateEnd].ToLowerInvariant();
            if (SlugValidator.Validate(candidate) != SlugValidationResult.Valid)
                continue;

            if (seen.Add(candidate))
                slugs.Add(candidate);

            index = candidateEnd - 1;
        }

        return new MentionParseResult(slugs);
    }

    private static bool HasLeadingBoundary(string content, int atIndex)
    {
        if (atIndex == 0)
            return true;

        var previous = content[atIndex - 1];
        return previous != '@' && !IsWordCharacter(previous);
    }

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character) || character == '_';

    private static bool IsSlugCharacter(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-' or '_';
}
