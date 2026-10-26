"""
Builds the Postman collection from the API's OpenAPI document.

    python tools/docs/postman.py <openapi.json> docs/backend/postman/SuperApp.postman_collection.json

Get the OpenAPI document from a running API (development): https://localhost:7254/openapi/v1.json

The collection:
  - one folder per API area (auth, pitches, bookings, partner, admin, ceo...), every endpoint with an example body
    built from its schema and its path / query parameters;
  - variables: baseUrl, agentKey (n8n / AI agents), factoryDevice + factorySession (factory staff);
  - a collection script that fetches a fresh anti-forgery token before every change (POST / PUT / PATCH / DELETE) and
    sends it as X-CSRF-TOKEN, so signing in and then calling anything just works (Postman keeps the cookies).
"""
import json
import re
import sys

PRE_REQUEST = """// Every change needs a fresh anti-forgery token (it is tied to the signed-in person)
if (pm.request.method !== 'GET' && !pm.request.headers.has('X-Api-Key')) {
  pm.sendRequest({ url: pm.collectionVariables.get('baseUrl') + '/api/v1/auth/csrf', method: 'GET' }, (err, res) => {
    if (!err && res.code === 200) {
      pm.request.headers.upsert({ key: 'X-CSRF-TOKEN', value: res.json().data.token });
    }
  });
}"""

SAMPLES = {"string": "text", "integer": 1, "number": 1, "boolean": False}
FORMATS = {"date": "2026-11-01", "date-time": "2026-11-01T18:00:00Z", "uuid": "00000000-0000-0000-0000-000000000000", "email": "player@superapp.test"}


def resolve(spec, schema):
    while isinstance(schema, dict) and "$ref" in schema:
        schema = spec["components"]["schemas"][schema["$ref"].split("/")[-1]]
    return schema or {}


def sample(spec, schema, depth=0):
    """An example value for a schema; nested objects stop at depth 4."""
    schema = resolve(spec, schema)
    for key in ("oneOf", "anyOf", "allOf"):
        if key in schema:
            options = [s for s in schema[key] if resolve(spec, s).get("type") != "null"]
            return sample(spec, options[0] if options else {}, depth)
    kind = schema.get("type")
    if isinstance(kind, list):
        kind = next((k for k in kind if k != "null"), None)
    if "enum" in schema:
        return schema["enum"][0]
    if kind == "array":
        return [] if depth > 3 else [sample(spec, schema.get("items", {}), depth + 1)]
    if kind == "object" or "properties" in schema:
        if depth > 3:
            return {}
        return {name: sample(spec, prop, depth + 1) for name, prop in schema.get("properties", {}).items()}
    if kind in ("integer", "number") and isinstance(schema.get("pattern"), str):
        return 1
    return FORMATS.get(schema.get("format"), SAMPLES.get(kind, None))


def folder_of(path):
    parts = [p for p in path.split("/") if p and not p.startswith("{")]
    if parts[:2] == ["api", "v1"]:
        parts = parts[2:]
    if not parts:
        return "other"
    return f"{parts[0]} / {parts[1]}" if parts[0] in ("admin", "ceo", "partner", "seller", "factory") and len(parts) > 1 else parts[0]


def request_item(spec, path, method, op):
    params = [resolve(spec, p) for p in op.get("parameters", [])]
    url_path = re.sub(r"\{([^}:]+)(:[^}]*)?\}", r":\1", path)
    url = {
        "raw": "{{baseUrl}}" + url_path,
        "host": ["{{baseUrl}}"],
        "path": [p for p in url_path.split("/") if p],
        "variable": [{"key": p["name"], "value": str(sample(spec, p.get("schema", {})) or "")} for p in params if p.get("in") == "path"],
        "query": [{"key": p["name"], "value": str(sample(spec, p.get("schema", {})) or ""), "disabled": True} for p in params if p.get("in") == "query"],
    }
    headers = [{"key": "Accept", "value": "application/json"}]
    if path.startswith("/api/v1/agent"):
        headers.append({"key": "X-Api-Key", "value": "{{agentKey}}"})
    if path.startswith("/api/v1/factory"):
        headers += [{"key": "X-Factory-Device", "value": "{{factoryDevice}}"}, {"key": "X-Factory-Session", "value": "{{factorySession}}"}]
    request = {"method": method.upper(), "header": headers, "url": url, "description": op.get("summary") or op.get("description") or ""}
    content = op.get("requestBody", {}).get("content", {})
    if "application/json" in content:
        request["body"] = {"mode": "raw", "raw": json.dumps(sample(spec, content["application/json"].get("schema", {})), indent=2),
                           "options": {"raw": {"language": "json"}}}
    elif "multipart/form-data" in content:
        props = resolve(spec, content["multipart/form-data"].get("schema", {})).get("properties", {})
        request["body"] = {"mode": "formdata", "formdata": [
            {"key": k, "type": "file" if resolve(spec, v).get("format") == "binary" or resolve(spec, v).get("type") == "array" else "text", "value": ""}
            for k, v in props.items()]}
    name = f"{method.upper()} {path.replace('/api/v1', '')}"
    return {"name": name, "request": request}


def nest(folders):
    """'admin / news' goes into an 'admin' folder with a 'news' subfolder; plain areas stay top level."""
    top = {}
    for name, items in sorted(folders.items()):
        if " / " in name:
            area, sub = name.split(" / ", 1)
            top.setdefault(area, {"name": area, "item": []})["item"].append({"name": sub, "item": items})
        else:
            top.setdefault(name, {"name": name, "item": []})["item"].extend(items)
    return list(top.values())


def build(spec):
    folders = {}
    for path, ops in sorted(spec["paths"].items()):
        for method, op in ops.items():
            if method in ("get", "post", "put", "patch", "delete"):
                folders.setdefault(folder_of(path), []).append(request_item(spec, path, method, op))
    return {
        "info": {
            "name": "Super App API",
            "description": "Generated by tools/docs/postman.py from the API's OpenAPI document. Sign in with POST /auth/sign-in first "
                           "(Postman keeps the cookie); the anti-forgery token is fetched for you. Use test accounts only.",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
        },
        "event": [{"listen": "prerequest", "script": {"type": "text/javascript", "exec": PRE_REQUEST.split("\n")}}],
        "variable": [
            {"key": "baseUrl", "value": "https://localhost:7254"},
            {"key": "agentKey", "value": ""},
            {"key": "factoryDevice", "value": ""},
            {"key": "factorySession", "value": ""},
        ],
        "item": nest(folders),
    }


if __name__ == "__main__":
    source, target = sys.argv[1], sys.argv[2]
    with open(source, encoding="utf-8") as f:
        spec = json.load(f)
    collection = build(spec)
    with open(target, "w", encoding="utf-8", newline="\n") as f:
        json.dump(collection, f, indent=2, ensure_ascii=False)
        f.write("\n")
    count = sum(1 for f in collection["item"] for i in f["item"] for _ in (i["item"] if "item" in i else [i]))
    print(f"{count} requests in {len(collection['item'])} folders -> {target}")
