FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
ENV VITE_API_URL=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src/backend
COPY backend/MpctTramites.Api/MpctTramites.Api.csproj ./
RUN dotnet restore
COPY backend/MpctTramites.Api/ ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:10000
COPY --from=backend-build /app ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot
RUN mkdir -p /app/storage && chown -R app:app /app
USER app
EXPOSE 10000
ENTRYPOINT ["dotnet", "MpctTramites.Api.dll"]
