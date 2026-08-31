# GitShare DevCard

[![CI](https://github.com/dontrushit/GitShare_DevCard/actions/workflows/ci.yml/badge.svg)](https://github.com/dontrushit/GitShare_DevCard/actions/workflows/ci.yml)

A site that looks at a public GitHub profile: languages, repos, how the code is put together, rough level. UI in RU/EN.

Stack: ASP.NET Core 8 + React. Audit text can go through GitHub Models. No key is fine: it just reads the files.

## Run it

You need .NET 8, Node 20+, Postgres, and a GitHub PAT.

```powershell
cd src\GitShare.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gitshare;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "GitHub:Token" "ghp_..."
dotnet user-secrets set "AI:ApiKey" "ghp_..."
dotnet run

cd src\GitShare.Client
npm install
npm run dev
```

UI: http://localhost:5173
API: http://localhost:5188

Docker: copy `.env.example` to `.env`, then `docker compose up --build`. App is at http://localhost:8081.

Tests: `dotnet test tests/GitShare.Api.Tests`

Deploy notes: [docs/production.md](docs/production.md).
