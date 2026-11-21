using System.Globalization;
using System.Text;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Sessions;

namespace QuizLive.Application.Players;

public sealed record NicknameValidationResult(
    bool IsValid,
    string DisplayNickname,
    string NormalizedKey,
    string? ErrorCode);

public sealed class NicknameNormalizer(IProfanityFilter profanityFilter)
{
    public NicknameValidationResult NormalizeAndValidate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new(false, string.Empty, string.Empty, ErrorCodes.NicknameInvalid);
        }

        // 1. NFKC normalize
        var nfkc = raw.Normalize(NormalizationForm.FormKC);

        // 2. Strip control, format (zero-width, bidi overrides), and surrogate-free invisible chars
        var sb = new StringBuilder(nfkc.Length);
        foreach (var rune in nfkc.EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                continue;
            }

            sb.Append(rune.ToString());
        }

        // 3. Collapse whitespace and trim
        var collapsed = CollapseWhitespace(sb.ToString()).Trim();
        var graphemeLength = new StringInfo(collapsed).LengthInTextElements;
        if (graphemeLength is < 2 or > 20)
        {
            return new(false, collapsed, string.Empty, ErrorCodes.NicknameInvalid);
        }

        // 4. Case/diacritic/Arabic-variant folding for uniqueness and profanity check
        var key = FoldForComparison(collapsed);
        if (key.Length == 0)
        {
            return new(false, collapsed, string.Empty, ErrorCodes.NicknameInvalid);
        }

        if (profanityFilter.ContainsProfanity(key))
        {
            return new(false, collapsed, key, ErrorCodes.NicknameProfane);
        }

        return new(true, collapsed, key, null);
    }

    public static string FoldForComparison(string input)
    {
        var nfd = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);

        foreach (var ch in nfd)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // Strip Arabic tatweel
            if (ch == '\u0640')
            {
                continue;
            }

            var mapped = ch switch
            {
                '\u0623' or '\u0625' or '\u0622' or '\u0671' => '\u0627', // أ إ آ ٱ -> ا
                '\u0649' => '\u064A', // ى -> ي
                '\u0629' => '\u0647', // ة -> ه
                _ => char.ToLowerInvariant(ch)
            };

            sb.Append(mapped);
        }

        return sb.ToString();
    }

    private static string CollapseWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        var prevSpace = false;
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
            else
            {
                sb.Append(ch);
                prevSpace = false;
            }
        }

        return sb.ToString();
    }
}
