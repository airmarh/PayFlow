# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer-cache friendliness
COPY ["PayFlow.API/PayFlow.API.csproj",             "PayFlow.API/"]
COPY ["PayFlow.Application/PayFlow.Application.csproj", "PayFlow.Application/"]
COPY ["PayFlow.Infrastructure/PayFlow.Infrastructure.csproj", "PayFlow.Infrastructure/"]
COPY ["PayFlow.Domain/PayFlow.Domain.csproj",       "PayFlow.Domain/"]

RUN dotnet restore "PayFlow.API/PayFlow.API.csproj"

# Copy remaining source
COPY . .
WORKDIR "/src/PayFlow.API"
RUN dotnet build "PayFlow.API.csproj" -c Release -o /app/build

# ── Publish stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "PayFlow.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PayFlow.API.dll"]
