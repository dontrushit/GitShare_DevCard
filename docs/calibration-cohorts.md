# Когорты калибровки уровня

Эталонные GitHub-профили для регрессии `ProgrammerLevelEvaluator` и API-аудита.

## Когорта 1 — Juniors / студенты

| Username | Ожидание | Заметка |
|----------|----------|---------|
| uchtenfrinen | trainee | C#/WPF/console, 0★ — без prod/PR не выше trainee |
| melsomino | trainee | академия, Delphi/C/Java |
| YuryS9 | trainee | учебные лабораторные |

## Когорта 2 — GameDev / mobile

| Username | Ожидание | Заметка |
|----------|----------|---------|
| prime31 | middle, senior | Unity/C# ветеран |
| JakeWharton | principal, lead | Android OSS |
| ashfurrow | senior, lead | iOS/Swift |

## Когорта 3 — DevOps / infra

| Username | Ожидание | Заметка |
|----------|----------|---------|
| geerlingguy | senior, lead | Ansible/YAML |
| alexellis | senior, lead | K8s, Go |
| jessfraz | senior, lead | Docker, Go |

## Когорта 4 — Enterprise

| Username | Ожидание | Заметка |
|----------|----------|---------|
| davidfowl | principal, lead | .NET architect |
| EgorBo | senior, lead, principal | .NET runtime |
| tiangolo | principal, lead | FastAPI |
| taylorotwell | principal, lead | Laravel |

## Прогон

```powershell
# API должен быть запущен (src/GitShare.Api → dotnet run)
cd scripts
.\export-profile-fixtures.ps1 -UseCache   # первый раз без -UseCache
.\run-profile-regression.ps1 -UseCache

cd ..\tests\GitShare.Api.Tests
dotnet test
```

`anchor: true` в `profile-matrix.json` — профиль не «растёт» со временем; при падении теста чиним код, а не ожидание.
