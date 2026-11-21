using FluentAssertions;
using QuizLive.Infrastructure.Security;

namespace QuizLive.UnitTests.Security;

public sealed class PinAndTokenTests
{
    [Fact]
    public void CryptoPinGenerator_ProducesSixDigits()
    {
        var gen = new CryptoPinGenerator();
        for (var i = 0; i < 100; i++)
        {
            var pin = gen.GeneratePin();
            pin.Should().HaveLength(6);
            pin.All(char.IsDigit).Should().BeTrue();
        }
    }

    [Fact]
    public void PlayerTokenService_HashesAndVerifiesInConstantTime()
    {
        var svc = new PlayerTokenService();
        var (raw, hash) = svc.GenerateToken();

        raw.Should().NotBeNullOrWhiteSpace();
        svc.HashToken(raw).Should().Be(hash);
        svc.FixedTimeEquals(hash, svc.HashToken(raw)).Should().BeTrue();
        svc.FixedTimeEquals(hash, svc.HashToken("wrong-token")).Should().BeFalse();
    }
}
