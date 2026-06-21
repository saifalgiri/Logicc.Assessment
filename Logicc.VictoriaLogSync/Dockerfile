FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Logicc.VictoriaLogSync/Logicc.VictoriaLogSync.csproj", "Logicc.VictoriaLogSync/"]
COPY ["Logicc.AuditLogLib/Logicc.AuditLogLib.csproj", "Logicc.AuditLogLib/"]
RUN dotnet restore "Logicc.VictoriaLogSync/Logicc.VictoriaLogSync.csproj"
COPY . .
RUN dotnet publish "Logicc.VictoriaLogSync/Logicc.VictoriaLogSync.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Logicc.VictoriaLogSync.dll"]
