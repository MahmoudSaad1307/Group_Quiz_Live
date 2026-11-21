using QuizLive.Application.Abstractions;
using QuizLive.Application.Players;

namespace QuizLive.Infrastructure.Profanity;

public sealed class WordListProfanityFilter : IProfanityFilter
{
    private readonly HashSet<string> _blocked;

    public WordListProfanityFilter()
    {
        _blocked = new HashSet<string>(StringComparer.Ordinal);
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Profanity");
        LoadFile(Path.Combine(baseDir, "words.en.txt"));
        LoadFile(Path.Combine(baseDir, "words.ar.txt"));
    }

    public bool ContainsProfanity(string normalizedText)
    {
        var leetNormalized = normalizedText
            .Replace('0', 'o')
            .Replace('1', 'i')
            .Replace('3', 'e')
            .Replace('4', 'a')
            .Replace('5', 's')
            .Replace('7', 't');

        var tokens = leetNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (_blocked.Contains(token))
            {
                return true;
            }
        }

        var compact = string.Concat(tokens);
        return _blocked.Any(w => w.Length >= 4 && compact.Contains(w, StringComparison.Ordinal));
    }

    private void LoadFile(string path)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            _blocked.Add(NicknameNormalizer.FoldForComparison(trimmed));
        }
    }
}
