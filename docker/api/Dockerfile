FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/GitShare.Api/GitShare.Api.csproj", "GitShare.Api/"]
RUN dotnet restore "GitShare.Api/GitShare.Api.csproj"
COPY src/GitShare.Api/ GitShare.Api/
RUN dotnet publish "GitShare.Api/GitShare.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GitShare.Api.dll"]
