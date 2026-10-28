FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/HackerNews.Api/HackerNews.Api.csproj", "src/HackerNews.Api/"]
RUN dotnet restore "src/HackerNews.Api/HackerNews.Api.csproj"
COPY . .
WORKDIR "/src/src/HackerNews.Api"
RUN dotnet build "HackerNews.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HackerNews.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HackerNews.Api.dll"]