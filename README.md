# GitShare DevCard

[![CI](https://github.com/dontrushit/GitShare_DevCard/actions/workflows/ci.yml/badge.svg)](https://github.com/dontrushit/GitShare_DevCard/actions/workflows/ci.yml)

Веб-сервис для анализа публичного GitHub-профиля: карточка разработчика, архитектурный аудит репозиториев и оценка уровня (RU/EN).  
Аудит сочетает статический разбор репозиториев (дерево файлов, сигнатуры стека, evidence по коду) с генерацией текста через **LLM** (GitHub Models). Без ключа модели работает rule-based fallback.

## Что умеет

- DevCard — метрики портфеля, языковой стек, активность
- Архитектурный паспорт топ-проектов: стек, долг, вопросы для интервью
- Оценка уровня от trainee до principal с пояснением (текст — LLM или эвристика)
- Слияние ответа модели с фактами из кода: структура и KeyFiles только из evidence
- Кэш в PostgreSQL, rate limiting, деплой через Docker

## Быстрый старт

```powershell
# API
cd src\GitShare.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gitshare;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "GitHub:Token" "ghp_..."
dotnet user-secrets set "AI:ApiKey" "ghp_..."
dotnet run

# Client
cd src\GitShare.Client
npm install
npm run dev
```

Клиент: http://localhost:5173 · API: http://localhost:5188

## Docker

```bash
cp .env.example .env
docker compose up --build
```

| Сервис | URL |
|--------|-----|
| UI | http://localhost:8081 |
| API | http://localhost:8080 |
| Health | http://localhost:8080/health |

Подробнее: [docs/production.md](docs/production.md)

## Тесты

```bash
dotnet test tests/GitShare.Api.Tests
cd src/GitShare.Client && npm run build
```

Для сборки клиента задайте `VITE_API_URL` (например `http://localhost:8080`).

## Структура

| Путь | Описание |
|------|----------|
| `src/GitShare.Api` | ASP.NET Core 8 API |
| `src/GitShare.Client` | React + Vite SPA |
| `tests/GitShare.Api.Tests` | Unit-тесты |
| `scripts/` | Регрессия и калибровка профилей |
| `docker/` | Образы API и клиента |

## Лицензия

[MIT](LICENSE)
