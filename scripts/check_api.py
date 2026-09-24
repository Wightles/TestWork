"""HTTP-проверки работающего API: python3 scripts/check_api.py [base_url]."""

import base64
import json
import sys
import urllib.error
import urllib.request


BASE_URL = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://localhost:8090"
FIELDS = {
    "is_error", "error_code", "error_message", "elements_count", "emails_count",
    "url", "decrypted_plain_text", "elements_attr_list", "emails_list",
}


def b64(value):
    return base64.b64encode(value).decode("ascii")


# Шифротекст получен независимо через OpenSSL AES-256-ECB, без padding.
# Исходный текст содержит завершающий пробел, который API должен сохранить.
VALID = {
    "selector": "a",
    "attribute": "href",
    "url_b64": b64(b"https://example.com/page"),
    "page_b64": b64(
        '<a href="/one">Первый</a><a>Второй</a>'
        '<div>test@example.com test@example.com other@sub.example.org</div>'.encode("utf-8")
    ),
    "key_bytes_b64": b64(bytes(range(32))),
    "encrypted_text_bytes_b64": "+tLSKjNm8Q/vvRtoNH4P1A==",
}
checks = 0


def check(payload, status, code="", raw=False):
    global checks
    request = urllib.request.Request(
        BASE_URL + "/api/process",
        data=payload if raw else json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    try:
        response = urllib.request.urlopen(request, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode("utf-8")
        result = json.loads(body)
        assert response.status == status, (response.status, body)
        assert result["error_code"] == code, result
        assert result["is_error"] == (0 if status == 200 else 1), result
        assert set(result) == FIELDS, result
        assert '\n  "' in body, body
    checks += 1
    return result


result = check(VALID, 200)
assert result["url"] == "https://example.com/page"
assert result["decrypted_plain_text"] == "Hello, TestJob! "
assert result["elements_count"] == 2
assert result["elements_attr_list"] == ["/one", ""]
assert result["emails_count"] == 3
assert result["emails_list"] == ["test@example.com", "test@example.com", "other@sub.example.org"]
result = check({**VALID, "selector": "div > a"}, 200)
assert result["elements_count"] == 0 and result["elements_attr_list"] == []
result = check({**VALID, "selector": "a[href]"}, 200)
assert result["elements_count"] == 1 and result["elements_attr_list"] == ["/one"]
result = check({**VALID, "page_b64": b64(b"<p>No links or emails</p>")}, 200)
assert result["elements_count"] == result["emails_count"] == 0
assert result["emails_list"] == []

check({}, 400, "EMPTY_SELECTOR")
for field, code in [
    ("selector", "EMPTY_SELECTOR"), ("attribute", "EMPTY_ATTRIBUTE"),
    ("url_b64", "MISSING_URL"), ("page_b64", "MISSING_PAGE"),
    ("key_bytes_b64", "MISSING_KEY"), ("encrypted_text_bytes_b64", "MISSING_ENCRYPTED_TEXT"),
]:
    for value in (None, "", "   "):
        check({**VALID, field: value}, 400, code)
    check({k: v for k, v in VALID.items() if k != field}, 400, code)

for field, code in [
    ("url_b64", "INVALID_URL_BASE64"), ("page_b64", "INVALID_PAGE_BASE64"),
    ("key_bytes_b64", "INVALID_KEY_BASE64"),
    ("encrypted_text_bytes_b64", "INVALID_ENCRYPTED_TEXT_BASE64"),
]:
    check({**VALID, field: "!!!"}, 400, code)
check({**VALID, "key_bytes_b64": b64(bytes(16))}, 400, "INVALID_KEY_LENGTH")
check({**VALID, "encrypted_text_bytes_b64": b64(bytes(15))}, 400, "INVALID_ENCRYPTED_TEXT_LENGTH")
check({**VALID, "selector": "a["}, 400, "INVALID_SELECTOR")
check({**VALID, "url_b64": b64(b"not a url")}, 400, "INVALID_URL")
check({**VALID, "url_b64": b64(b"file:///etc/hosts")}, 400, "INVALID_URL")
check({**VALID, "url_b64": b64(b"\xff")}, 400, "INVALID_URL_UTF8")
check({**VALID, "page_b64": b64(b"\xff")}, 400, "INVALID_PAGE_UTF8")
for body in (b"{", b"null", b"[]", b"", b'{"selector":123}'):
    check(body, 400, "INVALID_JSON", raw=True)

with urllib.request.urlopen(BASE_URL + "/api/swagger/index.html", timeout=15) as response:
    assert response.status == 200 and b"swagger-ui" in response.read()
    checks += 1
with urllib.request.urlopen(BASE_URL + "/api/swagger/v1/swagger.json", timeout=15) as response:
    spec = json.load(response)
    assert "post" in spec["paths"]["/api/process"]
    assert set(spec["components"]["schemas"]["ProcessRequest"]["properties"]) == set(VALID)
    checks += 1
print(f"PASS: {checks} HTTP checks (processing, validation, JSON, Swagger).")
