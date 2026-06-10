# Profile regression



Автопроверка профилей через API и офлайн-тесты по JSON-снимкам.



## Требования



- API: `cd src/GitShare.Api` → `dotnet run`

- PostgreSQL + `GitHub:Token` (+ опционально `AI:ApiKey`)



## Матрица когорт



`profile-matrix.json` — **15 профилей** в 5 волнах (когорты juniors → enterprise + edge cases).  

Описание: `docs/calibration-cohorts.md`.



## Экспорт снимков для unit-тестов



```powershell

cd scripts



# По кэшу БД (быстро)

.\export-profile-fixtures.ps1 -UseCache



# Полное обновление с GitHub + LLM (долго)

.\export-profile-fixtures.ps1



# Одна когорта / пользователи

.\export-profile-fixtures.ps1 -OnlyUser "uchtenfrinen,melsomino,YuryS9" -UseCache

```



Файлы: `tests/GitShare.Api.Tests/Fixtures/profiles/{username}.json`



## Регрессия API



```powershell

.\run-profile-regression.ps1 -UseCache

.\run-profile-regression.ps1 -OnlyUser "prime31,JakeWharton" -UseCache

.\run-profile-regression.ps1 -DelayBetweenProfilesSeconds 30

```



Отчёт: `regression-report.json`  

Код выхода: число упавших проверок (0 = всё ок).



## Unit-тесты (без API)



```powershell

cd ..\tests\GitShare.Api.Tests

dotnet test

```



Тесты `CohortMatrixTests` гоняют калькулятор по снимкам, если файл есть в `Fixtures/profiles/`.


