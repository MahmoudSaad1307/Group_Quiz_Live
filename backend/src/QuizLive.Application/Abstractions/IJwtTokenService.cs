using QuizLive.Domain.Entities;

namespace QuizLive.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateAdminToken(Admin admin);
}
