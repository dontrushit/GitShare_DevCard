# GitShare — продакшен

## Чеклист перед выкладкой

- [ ] PostgreSQL (не SQLite) с бэкапами
- [ ] `GITHUB_TOKEN` — PAT с доступом к public repos
- [ ] `AI_API_KEY` — токен GitHub Models (или rule-based без ключа)
- [ ] `Cors__AllowedOrigins` — точный origin фронта (HTTPS)
- [ ] `VITE_API_URL` при сборке клиента — публичный URL API
- [ ] Секреты только через env / vault (не в git)
- [ ] `GET /health` и `/health/ready` в мониторинге
- [ ] Rate limit настроен под нагрузку (`RateLimiting__ProfilePermitLimit`)

## Переменные окружения (API)

| Переменная | Обязательно | Описание |
|------------|-------------|----------|
| `ASPNETCORE_ENVIRONMENT` | да | `Production` |
| `ConnectionStrings__DefaultConnection` | да | PostgreSQL connection string |
| `Cors__AllowedOrigins__0` | да | URL фронта, напр. `https://app.example.com` |
| `GitHub__Token` | рекомендуется | GitHub PAT |
| `AI__ApiKey` | опционально | Без ключа — rule-based аудит |
| `AI__ModelId` | нет | По умолчанию `gpt-4o` |
| `Cache__ProfileTtlHours` | нет | TTL кэша профиля (часы) |
| `Database__ApplyMigrationsOnStartup` | нет | `true` в Docker; для K8s можно `false` + init job |
| `RateLimiting__ProfilePermitLimit` | нет | Запросов `/api/profile` на IP в минуту |

## Docker Compose

```bash
cp .env.example .env
docker compose up -d --build
docker compose logs -f api
```

## Сборка без Compose

**API:**

```bash
docker build -f docker/api/Dockerfile -t gitshare-api .
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=...;..." \
  -e Cors__AllowedOrigins__0="https://your-ui.example.com" \
  -e GitHub__Token="ghp_..." \
  gitshare-api
```

**Client:**

```bash
docker build -f docker/client/Dockerfile \
  --build-arg VITE_API_URL=https://api.example.com \
  -t gitshare-client .
```

## Миграции БД

При `Database__ApplyMigrationsOnStartup=true` миграции применяются при старте API.

Вручную:

```bash
cd src/GitShare.Api
dotnet ef database update
```

## Локализация аудита

- `GET /api/profile/{user}?locale=ru` — русский AI/rule-based текст
- `GET /api/profile/{user}?locale=en` — английский
- Кэш раздельный по локали (memory + PostgreSQL)

## Мониторинг

- `GET /health` — liveness
- `GET /health/ready` — readiness (включая БД)
- Логи: стандартный `ILogger`; необязательно подключить Serilog/OpenTelemetry

## Известные ограничения

- `ProgrammerLevel.Rationale` с API пока на русском; заголовок уровня переводит UI
- Первый анализ пользователя занимает секунды (GitHub + LLM)
- Публичный endpoint без авторизации — защита через rate limit и WAF

## CI

GitHub Actions: `.github/workflows/ci.yml` — `dotnet test` + client build с `VITE_API_URL`.
