namespace QuizLive.Domain.Enums;

public enum SessionStatus
{
    Lobby = 0,
    QuestionOpen = 1,
    QuestionClosed = 2,
    Leaderboard = 3,
    Paused = 4,
    Finished = 5,
    Abandoned = 6
}
