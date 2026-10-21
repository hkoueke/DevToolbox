# Contract: Azure DevOps Server REST surface consumed by varcompare

**Consumed, not exposed. Read-only.** Implemented behind `IVariableGroupGateway` (see
`application-ports.md`) so nothing above Infrastructure sees a URL, a status code, or a JSON shape.

**Target**: on-premises **Azure DevOps Server**, e.g. `https://devops.entreprise.local`
**Collection segment**: required on every route, e.g. `DefaultCollection`
**api-version**: `7.1` on every request (configurable)
**Authentication**: **integrated Windows credentials** — `UseDefaultCredentials = true`,
`PreAuthenticate = true`, `AllowAutoRedirect = false`. No `Authorization` header is ever constructed by this
app.

**Every request in this contract is a `GET`.** There is no `POST`, `PUT`, `PATCH`, or `DELETE` anywhere in
the tool (FR-018b), which is what makes full retry semantics safe (research R8).

---

## 1. List projects — FR-002b

```http
GET /{collection}/_apis/projects?api-version=7.1&$top=100&continuationToken={token}
```

`200` → `{ count, value: [ { id, name, description, state, visibility } ] }`
Paged via the `x-ms-continuationtoken` **response header**; follow to exhaustion.

---

## 2. List variable groups in a project — FR-003

```http
GET /{collection}/{project}/_apis/distributedtask/variablegroups?api-version=7.1
```

Optional: `groupName` (supports `*`), `$top`, `continuationToken`, `queryOrder`.

`200` → `{ count, value: [ VariableGroup ] }`. Only `id`, `name`, `description`, `type`, `isShared` are used
at list time; `variables` is ignored here.

---

## 3. Get one variable group — FR-006

```http
GET /{collection}/{project}/_apis/distributedtask/variablegroups/{groupId}?api-version=7.1
```

`200` → `VariableGroup`:

```jsonc
{
  "id": 42,
  "name": "app-prod",
  "description": "Production settings",
  "type": "Vsts",                       // or "AzureKeyVault"
  "isShared": false,
  "modifiedOn": "2026-08-14T09:11:02.1Z",
  "variables": {
    "Api__BaseUrl": { "value": "https://api.example.com", "isSecret": false, "isReadOnly": false },
    "Api__Key":     { "value": null, "isSecret": true,  "isReadOnly": false },   // secret ⇒ value is null
    "Feature__X":   { "value": "",   "isSecret": false, "isReadOnly": true  }
  },
  "variableGroupProjectReferences": [ /* present, not mapped — only needed for updates, which we never do */ ]
}
```

**Reading rules** (→ `CellState`): missing key ⇒ `Absent` · `isSecret: true` ⇒ `PresentSecret` ·
`value == ""` ⇒ `PresentEmpty` · non-empty ⇒ `PresentWithValue` · `type == "AzureKeyVault"` ⇒ every entry is
additionally key-vault-sourced (FR-009).

`modifiedOn` is surfaced for display ("as at …") only. It guards nothing — there is no write to guard.

---

## 4. URL construction rules

Every segment is validated and escaped before use (FR-031c):

```
{BaseUrl}/{collection}/{project}/_apis/distributedtask/variablegroups/{groupId}?api-version={v}
   │          │            │                                             └ int, no escaping needed
   │          │            └ Guid when known, else Uri.EscapeDataString(name)
   │          └ validated: 1–64 chars, no separators, no ".."
   └ absolute, HTTPS unless AllowInsecureHttp is explicitly set; trailing slash enforced
```

Note the optional virtual directory some Server installations use (`https://host/tfs/DefaultCollection`).
The collection is taken relative to the configured base URL — never assumed to be the first path segment.

---

## 5. Error mapping

The gateway translates transport reality into domain outcomes; no `HttpRequestException` and no status code
escapes it.

| Status / condition | Domain outcome | Shown to the developer |
|---|---|---|
| `401` | `AuthenticationFailed` | "the server rejected your Windows credentials" + the server and collection tried (FR-032). **Never** a credential prompt |
| `403` | `PermissionDenied` | "you do not have permission to read this group" (FR-029) |
| `404` on group | `GroupNotFound` | resume path offers to drop it or reselect (FR-039d) |
| `429` / `503` + `Retry-After` | retried by the pipeline | surfaced as "waiting, the service is throttling" (FR-037) |
| retry budget exhausted | `ServiceUnavailable` | FR-036 → FR-038 |
| circuit open | `ServiceUnavailable` | "the server appears to be unavailable" (FR-038) |
| DNS / connect failure | `ServerUnreachable` | distinct from a permission failure — "off the network or VPN down?" |
| TLS failure | `ServerUntrusted` | reported plainly; certificate validation is never disabled (FR-031b) |
| `3xx` | `RedirectRefused` | redirects are off so credentials never reach another host (FR-028d) |
| cancellation | `Cancelled` | FR-040 |

**Never surfaced to the console**: raw response bodies, stack traces, full URLs with query strings, or any
credential material (Principle III).
