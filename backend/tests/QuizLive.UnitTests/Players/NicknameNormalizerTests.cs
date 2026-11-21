using FluentAssertions;
using QuizLive.Application.Abstractions;
using QuizLive.Application.Players;
using QuizLive.Application.Sessions;

namespace QuizLive.UnitTests.Players;

public sealed class NicknameNormalizerTests
{
    private sealed class FakeProfanityFilter(params string[] blocked) : IProfanityFilter
    {
        private readonly HashSet<string> _blocked = [.. blocked];
        public bool ContainsProfanity(string normalizedText) => _blocked.Any(normalizedText.Contains);
    }

    [Fact]
    public void NormalizeAndValidate_StripsControlAndBidiOverrides()
    {
        var sut = new NicknameNormalizer(new FakeProfanityFilter());
        var res = sut.NormalizeAndValidate("  \u202EAlice\u200B   Bob\t ");

        res.IsValid.Should().BeTrue();
        res.DisplayNickname.Should().Be("Alice Bob");
        res.NormalizedKey.Should().Be("alice bob");
    }

    [Fact]
    public void NormalizeAndValidate_FoldsArabicVariantsAndDiacritics()
    {
        var sut = new NicknameNormalizer(new FakeProfanityFilter());
        var r1 = sut.NormalizeAndValidate("أَحْمَدْ");
        var r2 = sut.NormalizeAndValidate("احمد");

        r1.IsValid.Should().BeTrue();
        r2.IsValid.Should().BeTrue();
        r1.NormalizedKey.Should().Be(r2.NormalizedKey);
    }

    [Fact]
    public void NormalizeAndValidate_RejectsTooShortOrTooLongByGraphemes()
    {
        var sut = new NicknameNormalizer(new FakeProfanityFilter());
        sut.NormalizeAndValidate("A").IsValid.Should().BeFalse();
        sut.NormalizeAndValidate(new string('x', 21)).IsValid.Should().BeFalse();

        // 20 emoji graphemes should be accepted
        var emojis = string.Concat(Enumerable.Repeat("😀", 20));
        sut.NormalizeAndValidate(emojis).IsValid.Should().BeTrue();
    }

    [Fact]
    public void NormalizeAndValidate_RejectsProfanity()
    {
        var sut = new NicknameNormalizer(new FakeProfanityFilter("badword"));
        var res = sut.NormalizeAndValidate("BadWord123");

        res.IsValid.Should().BeFalse();
        res.ErrorCode.Should().Be(ErrorCodes.NicknameProfane);
    }
}
