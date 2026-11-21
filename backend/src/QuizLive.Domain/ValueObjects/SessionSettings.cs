namespace QuizLive.Domain.ValueObjects;

public sealed record SessionSettings(
    bool ShowQuestionOnPhones = false,
    bool ShuffleQuestions = false,
    bool ShuffleOptions = false,
    bool StreakBonus = true,
    bool AllowLateJoin = false,
    bool AutoCloseWhenAllAnswered = true,
    bool AutoAdvance = false,
    int AutoAdvanceDelaySeconds = 5);
