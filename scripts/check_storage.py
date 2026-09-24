"""Проверка API и сохранения в БД; по умолчанию используется Docker Compose."""

import argparse
import base64
import json
import subprocess
import urllib.error
import urllib.request
import uuid


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--base-url", default="http://localhost:8090")
parser.add_argument("--database-url", help="PostgreSQL URI для локального psql вместо Docker")
args = parser.parse_args()
psql = (["psql", args.database_url] if args.database_url else
        ["docker", "compose", "exec", "-T", "postgres", "psql", "-U", "testjob", "-d", "testjob"])


def sql(statement):
    return subprocess.run(
        psql + ["-X", "-A", "-t", "-v", "ON_ERROR_STOP=1", "-c", statement],
        check=True, capture_output=True, text=True, timeout=30,
    ).stdout.strip()


def post(payload, expected_status):
    request = urllib.request.Request(
        args.base_url.rstrip("/") + "/api/process", data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json"},
    )
    try:
        response = urllib.request.urlopen(request, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        result = json.load(response)
        assert response.status == expected_status, result
        return result


marker = "storage-" + uuid.uuid4().hex
attribute = f"/{marker}?name=O'Reilly&lang=ru"
html = f'<a href="/{marker}?name=O\'Reilly&amp;lang=ru">Первый</a><a>Второй</a>'
payload = {
    "selector": "a", "attribute": "href",
    "url_b64": base64.b64encode(b"https://example.com").decode(),
    "page_b64": base64.b64encode(html.encode()).decode(),
    "key_bytes_b64": base64.b64encode(bytes(range(32))).decode(),
    "encrypted_text_bytes_b64": "+tLSKjNm8Q/vvRtoNH4P1A==",
}
before = int(sql("SELECT coalesce(max(id), 0) FROM elements"))
result = post(payload, 200)
assert result["is_error"] == 0 and result["elements_count"] == 2
assert result["elements_attr_list"] == [attribute, ""]
rows = json.loads(sql(
    "SELECT coalesce(json_agg(t ORDER BY id), '[]') FROM "
    f"(SELECT id, attribute_value, html FROM elements WHERE id > {before}) t"
))
assert len(rows) == 2, rows
assert rows[0]["attribute_value"] == attribute, rows
assert rows[0]["html"] == html.split("</a>")[0] + "</a>", rows
assert rows[1]["attribute_value"] == "" and rows[1]["html"] == "<a>Второй</a>", rows
assert rows[0]["id"] < rows[1]["id"]

count = int(sql("SELECT count(*) FROM elements"))
post({**payload, "key_bytes_b64": "invalid"}, 400)
post({**payload, "selector": "a["}, 400)
result = post({**payload, "selector": "img"}, 200)
assert result["elements_count"] == 0
assert int(sql("SELECT count(*) FROM elements")) == count

# Повторный запрос добавляет новые строки: дедупликации в задании нет.
post(payload, 200)
assert int(sql("SELECT count(*) FROM elements")) == count + 2
print("PASS: attributes, full HTML, UTF-8, SQL parameters, identity, invalid/empty requests, repeated request.")
print("В тестовой БД оставлены 4 строки; запускайте проверку без параллельных запросов.")
