using QuizLive.Application.Results;

namespace QuizLive.Application.Abstractions;

public interface ISessionResultsService
{
    Task<SessionResultsDto?> GetResultsAsync(Guid sessionId, Guid hostId, CancellationToken ct = default);
}

public interface IResultsExporter
{
    byte[] ExportCsv(SessionResultsDto results);
    byte[] ExportXlsx(SessionResultsDto results);
}
