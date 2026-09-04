# zsc-health-auth-demo — "before" architecture (ZEISS Requirement #1)

A synthetic, ZSC-flavoured .NET 8 monorepo built to demo ZEISS Requirement #1 —
migrating the **Health Status API for ZSC and ZLS** from OAuth2 bearer tokens to
**subscription-key authentication**, while every other ZSC API stays on OAuth2.

This is **not** ZEISS's real codebase. ZEISS has not shared it. This is a small,
real, compilable, *runnable* stand-in that reproduces the same request chain and
the same coupling, so a live coding agent has something concrete to change and
something concrete to prove it against.

Sibling repo: [`zsc-demo`](https://github.com/sm0se/zsc-demo) is the same idea for
Requirement #2 (routing simplification / removing the common library).

## The chain

The client describes the routing pattern as:

> `API Interceptor service -> BFF service -> Common routes -> HealthcheckStatus API`

with a fifth repo, "API gateway", named alongside it. Here the gateway sits
upstream of the Interceptor, so one request travels:

```
client
  └─► api-gateway :5080        edge header policy, correlation id, no auth of its own
        └─► interceptor :5100  ← the platform's authentication boundary (OAuth2)
              └─► bff :5200    resolves the path against Common routes
                    └─► health-status :5300   /internal/health/{zsc|zls}/status
                    └─► device-api    :5400   /internal/devices/{id}/status
```

`Zsc.CommonRoutes` is "Common routes": the route table, the shared authentication
setup every service installs, and the handler that carries a caller's context
from one hop to the next.

## Layout

| Project | Port | Role |
|---|---|---|
| `src/Zsc.ApiGateway` | 5080 | Public front door. Narrows inbound headers to an edge allowlist, stamps `X-Correlation-Id`, forwards to the Interceptor. Also hosts a development token endpoint standing in for the tenant's authorization server. |
| `src/Zsc.Interceptor` | 5100 | The authentication boundary. Installs the platform's OAuth2 scheme and a **global** authorization fallback policy, so every path it forwards needs a valid bearer token. |
| `src/Zsc.Bff` | 5200 | Resolves the public path against the common route table and forwards to whichever service owns it. Validates the bearer again for itself. |
| `src/Zsc.HealthStatus` | 5300 | **The HealthcheckStatus API.** Serves the ZSC and ZLS Health Status endpoints, aggregating component health. OAuth2-authenticated today. |
| `src/Zsc.DeviceApi` | 5400 | A representative "other ZSC API". It exists so that a change to health-status can be shown *not* to have touched anything else. |
| `src/Zsc.CommonRoutes` | — | Common routes: route table, service registry, shared auth setup, header propagation, DTOs. |

Tests: `tests/Zsc.CommonRoutes.Tests`, `tests/Zsc.HealthStatus.Tests`,
`tests/Zsc.Interceptor.Tests` (in-process) and `tests/Zsc.E2E.Tests` (real HTTP
through the running chain).

## Public API surface

| Route | Answered by | Authentication today |
|---|---|---|
| `GET /api/v1/health/zsc/status` | health-status | OAuth2 bearer |
| `GET /api/v1/health/zls/status` | health-status | OAuth2 bearer |
| `GET /api/v1/devices/{deviceId}/status` | device-api | OAuth2 bearer |
| `POST /dev/token` | api-gateway | anonymous (development only) |
| `GET /healthz` | every service | anonymous |

`/healthz` is **process liveness**, not a product API. The ZSC/ZLS *Health Status
API* is `/api/v1/health/{platform}/status`, served by `Zsc.HealthStatus` through
the full chain. Requirement #1 is about the latter; `/healthz` is unaffected.

## Running it

Requires the .NET 8 SDK. Nothing else — no database, no identity provider, no
network access.

```bash
dotnet build                  # all six projects
dotnet test                   # 19 pass, 8 end-to-end tests skipped

scripts/run-all.sh            # start all five services, wait for liveness
scripts/smoke.sh              # curl the whole chain and assert every status code
scripts/stop-all.sh
```

Without a local SDK:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test
```

A request end to end:

```bash
TOKEN=$(curl -sS -X POST http://127.0.0.1:5080/dev/token | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
curl -sS -H "Authorization: Bearer $TOKEN" http://127.0.0.1:5080/api/v1/health/zsc/status
```

## The end-to-end suite is red on purpose

`tests/Zsc.E2E.Tests` is the executable specification of Requirement #1. It only
runs when the services are up:

```bash
scripts/run-all.sh
ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests
```

On this unmodified repository that reports **8 passed, 4 failed**. The four
failures are `HealthStatusAuthenticationTests` — the Health Status API does not
accept a subscription key yet. The eight passes are `OAuth2RegressionTests`,
which must *stay* green: they are what keeps the change scoped.

**Done means `ZSC_E2E=1 dotnet test` reports 12 passed, 0 failed, and plain
`dotnet test` is still green.** `scripts/smoke.sh` reports the same thing in
curl form: 2 failures now, 0 when the requirement is met.

The requirement, the decisions taken on the client's open questions, and the
acceptance criteria are in [`docs/REQUIREMENT-R1.md`](docs/REQUIREMENT-R1.md).
How a request actually flows and where headers are narrowed is in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## A note on the signing key

`Zsc:OAuth2:SigningKey` is committed in plain sight in every `appsettings.json`.
It is a demo value for a symmetric-key token the repository issues to itself so
the chain runs offline. It is not a credential to anything.
