# Границы продукта и ограничения

GitShare DevCard анализирует **только публичные** данные GitHub. Это осознанное ограничение MVP.

## Что видит система

- Публичные репозитории, звёзды, форки, primary language
- Дерево файлов и манифесты (до лимита глубины)
- Недавние коммиты и внешние PR (если доступны API)
- Опционально: LLM-аудит при настроенном `AI:ApiKey`

## Чего система не видит

| Область | Ограничение |
|---------|-------------|
| Приватные репозитории | Не анализируются без PAT с scope `repo` |
| Коммерческий опыт | Не отражён в открытом Git |
| Code review / soft skills | Не извлекаются из профиля |
| Языковой стек | Эвристика по bytes + вес репо; не замена GitHub Linguist в IDE |
| Уровень разработчика | Калибровка по публичному портфелю; `IsLowConfidence` при слабых сигналах |

## Кэш

- TTL профиля: 24 ч (настраивается `Cache:ProfileTtlHours`)
- `?forceRefresh=true` — полный пересчёт (rate limit на стороне API)
- В UI отображаются `AnalyzedAtUtc` и признак `ServedFromCache`

## Приватные репозитории (требуется ваша настройка)

Поддержка приватных репо **не включена по умолчанию** — нужен осознанный opt-in:

1. Создайте GitHub PAT с scope `repo` (только для своего аккаунта).
2. Передайте токен в API (`GitHub:Token` / user-secrets) — **не коммитьте в репозиторий**.
3. Явно запрашивайте анализ своего профиля с флагом расширенного доступа (когда endpoint будет добавлен).

Без этих шагов приватный код в оценку не попадает.

## Регрессия и калибровка

- Офлайн: `dotnet test tests/GitShare.Api.Tests`
- Live API: `scripts/run-profile-regression.ps1` (нужны запущенный API, `GITHUB_TOKEN`, опционально `AI_API_KEY`)
- Экспорт снимков: `scripts/export-profile-fixtures.ps1 -OnlyUser dontrushit`

## Self-dogfood

Профиль `dontrushit` / репозиторий `GitShare_DevCard` — эталон fullstack monorepo в `scripts/profile-matrix.json` (когорта `self-dogfood`).
