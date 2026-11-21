namespace QuizLive.Api.Hubs;

public static class HubGroupNames
{
    public static string Host(Guid sessionId) => $"session:{sessionId}:host";
    public static string Players(Guid sessionId) => $"session:{sessionId}:players";
    public static string Screens(Guid sessionId) => $"session:{sessionId}:screens";
}
