using QuizLive.Domain.Entities;

namespace QuizLive.Application.Abstractions;

public interface IPasswordHasherService
{
    string HashPassword(Admin admin, string password);
    bool VerifyPassword(Admin admin, string password);
}
