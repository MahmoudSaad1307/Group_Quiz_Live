using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using QuizLive.Application.Sessions;

namespace QuizLive.Api.Observability;

public sealed class QuizLiveMetrics : IDisposable
{
    private readonly Meter _meter = new("QuizLive", "1.0.0");
    private readonly Histogram<double> _answerLatencyMs;

    public QuizLiveMetrics(SessionManager sessionManager)
    {
        _meter.CreateObservableGauge("quizlive_sessions_active", () => sessionManager.GetAllSessionIds().Count);
        _meter.CreateObservableGauge("quizlive_players_connected", () =>
        {
            var count = 0;
            foreach (var id in sessionManager.GetAllSessionIds())
            {
                if (sessionManager.TryGetById(id, out var rt))
                {
                    count += rt.Players.Values.Count(p => !p.IsKicked && p.IsConnected);
                }
            }
            return count;
        });
        _answerLatencyMs = _meter.CreateHistogram<double>("quizlive_answer_latency_ms");
    }

    public void RecordAnswerLatency(double ms) => _answerLatencyMs.Record(ms);

    public void Dispose() => _meter.Dispose();

    public static string RenderPrometheusText(SessionManager sessionManager)
    {
        var sessions = sessionManager.GetAllSessionIds();
        var connectedPlayers = 0;
        foreach (var id in sessions)
        {
            if (sessionManager.TryGetById(id, out var rt))
            {
                connectedPlayers += rt.Players.Values.Count(p => !p.IsKicked && p.IsConnected);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("# TYPE quizlive_sessions_active gauge");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"quizlive_sessions_active {sessions.Count}"));
        sb.AppendLine("# TYPE quizlive_players_connected gauge");
        sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"quizlive_players_connected {connectedPlayers}"));
        return sb.ToString();
    }
}
