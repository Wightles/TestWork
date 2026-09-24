# TestJob — обработка HTML через REST API

Решение [тестового задания](https://github.com/adm-devsec/TestJob).

Планируемый стек: ASP.NET Core 10, FluentValidation, AngleSharp, Dapper,
PostgreSQL 18 и Docker Compose.

API будет принимать JSON, декодировать URL и HTML из Base64, извлекать
элементы по CSS-селектору и email-адреса, сохранять элементы в PostgreSQL
и расшифровывать текст с помощью AES-256 ECB без padding.

## Структура

```text
src/TestJob.Api/
├── Controllers/    # HTTP-контроллер
├── Models/         # Модели запроса, ответа и валидация
└── Services/       # Обработка данных и работа с БД
docker/
├── postgres/       # Инициализация БД
└── pgadmin/        # Настройка подключения к БД
```

Файлы `.gitkeep` сохраняют пока пустые папки в Git.
Корневой `compose.yml` будет добавлен на этапе настройки Docker.

## Этапы

1. Подготовить структуру проекта — выполнено.
2. Создать ASP.NET Core проект, модели, валидацию, контроллер и Swagger — выполнено.
3. Реализовать обработку HTML, поиск email, дешифрование и обработку ошибок.
4. Добавить сохранение через Dapper и Docker Compose с PostgreSQL и pgAdmin.
5. Проверить оба входных примера и некорректные запросы, сохранить фактические
   ответы в `json_result_1.txt` и `json_result_2.txt`, описать запуск.

## Текущее состояние

Создан API на .NET 10 с `POST /api/process`, моделями и FluentValidation.
JSON использует имена полей в snake_case и форматирование с отступами.
Валидация проверяет обязательные поля, Base64, длину ключа AES-256 и шифротекста.
Некорректный запрос возвращает HTTP 400 и объект ответа с `is_error: 1`.
При нескольких ошибках валидации возвращается первая.

Обработка данных ещё не реализована: корректный запрос пока возвращает HTTP 501
с кодом `NOT_IMPLEMENTED`. Сервис, БД и Docker Compose появятся на следующих этапах.
Каждый следующий этап оформляется отдельным коммитом.

Проверка второго этапа: `git diff --check` проходит. Сборка и HTTP-проверки
пока не подтверждены: в среде разработки недоступна установка SDK 10,
а попытка сборки в Docker остановилась на восстановлении зависимостей.
Перед следующим этапом необходимо завершить сборку и проверить API.

## Локальный запуск

Требуется **.NET SDK 10**.

```bash
dotnet restore src/TestJob.Api/TestJob.Api.csproj
dotnet run --project src/TestJob.Api --urls http://localhost:8090
```

Если SDK 10 не установлен, из корня проекта можно запустить API через Docker:

```bash
docker run --rm -p 127.0.0.1:8090:8090 \
  -v "$PWD:/workspace" -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run --project src/TestJob.Api --urls http://0.0.0.0:8090
```

Swagger UI: <http://localhost:8090/api/swagger>.
Спецификация: <http://localhost:8090/api/swagger/v1/swagger.json>.

Пример проверки валидации:

```bash
curl -i http://localhost:8090/api/process \
  -H 'Content-Type: application/json' \
  -d '{}'
```

Ожидается HTTP 400 с `error_code: "EMPTY_SELECTOR"`.
