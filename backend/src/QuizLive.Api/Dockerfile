FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./
COPY src/QuizLive.Domain/QuizLive.Domain.csproj src/QuizLive.Domain/
COPY src/QuizLive.Application/QuizLive.Application.csproj src/QuizLive.Application/
COPY src/QuizLive.Infrastructure/QuizLive.Infrastructure.csproj src/QuizLive.Infrastructure/
COPY src/QuizLive.Api/QuizLive.Api.csproj src/QuizLive.Api/

RUN dotnet restore src/QuizLive.Api/QuizLive.Api.csproj

COPY src/ src/
RUN dotnet publish src/QuizLive.Api/QuizLive.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && \
    mkdir -p /app/data/uploads && chown -R app:app /app

USER app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "QuizLive.Api.dll"]
