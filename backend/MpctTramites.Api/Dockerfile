FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MpctTramites.Api.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser && mkdir -p /app/storage && chown -R appuser /app
USER appuser
COPY --from=build --chown=appuser:appuser /app .
EXPOSE 8080
ENTRYPOINT ["dotnet","MpctTramites.Api.dll"]
