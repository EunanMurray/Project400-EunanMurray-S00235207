FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Project400API/Project400API.csproj", "Project400API/"]
COPY ["Project400Shared/Project400Shared.csproj", "Project400Shared/"]
COPY ["Project400ServiceDefaults/Project400ServiceDefaults.csproj", "Project400ServiceDefaults/"]
RUN dotnet restore "Project400API/Project400API.csproj"
COPY . .
WORKDIR "/src/Project400API"
RUN dotnet build "Project400API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Project400API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Project400API.dll"]
