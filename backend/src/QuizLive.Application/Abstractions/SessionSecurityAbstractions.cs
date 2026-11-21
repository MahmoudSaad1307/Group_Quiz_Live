namespace QuizLive.Application.Abstractions;

public interface IPinGenerator
{
    string GeneratePin();
}

public interface IPlayerTokenService
{
    (string RawToken, string TokenHash) GenerateToken();
    string HashToken(string rawToken);
    bool FixedTimeEquals(string hashA, string hashB);
}

public interface IProfanityFilter
{
    bool ContainsProfanity(string normalizedText);
}

public interface IScreenTokenService
{
    string CreateScreenToken(Guid sessionId);
    bool TryValidateScreenToken(string token, out Guid sessionId);
}
