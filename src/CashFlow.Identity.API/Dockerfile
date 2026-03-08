FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/CashFlow.ServiceDefaults/*.csproj", "src/CashFlow.ServiceDefaults/"]
COPY ["src/CashFlow.Domain/*.csproj", "src/CashFlow.Domain/"]
COPY ["src/CashFlow.Identity.API/*.csproj", "src/CashFlow.Identity.API/"]
RUN dotnet restore "src/CashFlow.Identity.API/CashFlow.Identity.API.csproj"
COPY . .
RUN dotnet publish "src/CashFlow.Identity.API/CashFlow.Identity.API.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CashFlow.Identity.API.dll"]
