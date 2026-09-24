# TestJob

REST API на ASP.NET Core 10 для обработки HTML-страницы из JSON-запроса.

API декодирует URL и HTML из Base64, выбирает элементы по CSS-селектору,
собирает значения указанного атрибута, ищет email, расшифровывает текст через
AES-256 ECB и сохраняет найденные элементы в PostgreSQL.

## Стек

- ASP.NET Core 10
- FluentValidation
- AngleSharp
- Dapper
- PostgreSQL 18
- Docker Compose

## Запуск

```bash
docker compose up -d --wait
```

После запуска:

- API: http://localhost:8090
- Swagger: http://localhost:8090/api/swagger
- pgAdmin: http://localhost:8080

pgAdmin открывается без ввода пароля. Подключение к базе уже добавлено как
`TestJob PostgreSQL 18`.

Остановить контейнеры:

```bash
docker compose down
```

Данные PostgreSQL остаются в volume. Для полного удаления данных используйте
`docker compose down -v`.

## API

Основной метод:

```text
POST /api/process
```

Тело запроса:

```json
{
  "selector": "a[href]",
  "attribute": "href",
  "url_b64": "...",
  "encrypted_text_bytes_b64": "...",
  "key_bytes_b64": "...",
  "page_b64": "..."
}
```

Ответ содержит:

- количество найденных элементов;
- список значений атрибутов;
- найденные email;
- расшифрованный текст;
- раскодированный URL;
- поля ошибки, если запрос некорректный.

## База данных

Таблица создаётся при первом запуске:

```sql
elements (
    id bigint generated always as identity primary key,
    attribute_value text not null,
    html text not null
)
```

В таблицу сохраняются значение атрибута и полный HTML найденного элемента.

## Проверка

```bash
python3 scripts/check_api.py
python3 scripts/check_storage.py
```

В корне проекта лежат результаты обработки входных файлов задания:

- `json_result_1.txt`
- `json_result_2.txt`
