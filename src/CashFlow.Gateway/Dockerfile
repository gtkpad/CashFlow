FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/CashFlow.ServiceDefaults/*.csproj", "src/CashFlow.ServiceDefaults/"]
COPY ["src/CashFlow.Domain/*.csproj", "src/CashFlow.Domain/"]
COPY ["src/CashFlow.Gateway/*.csproj", "src/CashFlow.Gateway/"]
RUN dotnet restore "src/CashFlow.Gateway/CashFlow.Gateway.csproj"
COPY . .
RUN dotnet publish "src/CashFlow.Gateway/CashFlow.Gateway.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Gateway.dll"]
