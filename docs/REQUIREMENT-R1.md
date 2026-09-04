# Requirement #1 — Health Status API: OAuth2 → subscription key

## The ask, as received

> Currently the Health Status API for ZSC and ZLS is accessible through Oauth2
> token, this has to be changed to subscription key based authentication. The
> other ZSC API's continue to be Oauth2 based except Health status API.
>
> The routing pattern is `API Interceptor service -> BFF service -> Common routes
> -> HealthcheckStatus API`
>
> Repos to be shared: Health check, API gateway, BFF, interceptor service.

## Requirements

| # | Requirement |
|---|---|
| R1.1 | The Health Status API for **ZSC** authenticates via subscription key instead of OAuth2. |
| R1.2 | The Health Status API for **ZLS** likewise. |
| R1.3 | All other ZSC APIs remain OAuth2-authenticated. The change is scoped to the Health Status API alone. |
| R1.4 | The change is implemented across the documented chain: API Interceptor service → BFF service → Common routes → HealthcheckStatus API. |
| R1.5 | Four repositories are in scope: Health check, API gateway, BFF, Interceptor service. |
| R1.6 | The two schemes coexist on the same ingress: authentication becomes a per-route policy rather than a global one. |

## Decisions taken

The client's brief leaves several things open. This repository resolves them so
that "done" is testable. Each is a decision, not a fact about ZEISS's system, and
each would need confirming on the architecture call.

| Question | Decision here |
|---|---|
| What is a "subscription key"? | The Azure API Management convention: an `Ocp-Apim-Subscription-Key` request header carrying an opaque provisioned key. |
| Is OAuth2 retained on the Health Status API during a transition? | **No — hard cutover.** After the change a bearer token alone does not open the Health Status API. This is the literal reading of "has to be changed to". The additive alternative (accept either) is a one-line difference in the policy and a change to one acceptance test. |
| Is the "API gateway" a fifth component, or another name for the Interceptor? | **A fifth component**, upstream of the Interceptor. It is where the platform meets untrusted callers. |
| Where are valid keys held? | Configuration, under `Zsc:SubscriptionKeys`, in whichever service terminates the scheme. The demo provisions exactly one key: `zsc-demo-subscription-key-001`. Issuance, rotation and revocation are out of scope. |
| Does the key authorise, or only authenticate? | Authenticate only. No scopes, quotas or per-consumer rate limits. |

## Acceptance criteria

Executable, in `tests/Zsc.E2E.Tests`, run against the five services actually
running — not in-process:

```bash
scripts/run-all.sh
ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests
```

**`HealthStatusAuthenticationTests` — currently failing, must pass:**

| Request through the gateway | Expected |
|---|---|
| `GET /api/v1/health/zsc/status` with a valid `Ocp-Apim-Subscription-Key` | 200, payload `platform: "ZSC"` |
| `GET /api/v1/health/zls/status` with a valid `Ocp-Apim-Subscription-Key` | 200, payload `platform: "ZLS"` |
| either, with an unknown subscription key | 401 |
| either, with no credentials | 401 |
| either, with an OAuth2 bearer and no subscription key | 401 |

**`OAuth2RegressionTests` — currently passing, must stay passing:**

| Request through the gateway | Expected |
|---|---|
| `GET /api/v1/devices/{id}/status` with an OAuth2 bearer | 200 |
| `GET /api/v1/devices/{id}/status` with a subscription key | 401 |
| `GET /api/v1/devices/{id}/status` with no credentials | 401 |
| `GET /healthz` | 200 |

And, unchanged: plain `dotnet test` on a clean checkout stays green — the
in-process suites in `tests/Zsc.HealthStatus.Tests`, `tests/Zsc.Interceptor.Tests`
and `tests/Zsc.CommonRoutes.Tests` describe behaviour that must survive, so where
the change invalidates one of them, update it deliberately and say why.

`scripts/smoke.sh` asserts the same contract in curl form against the running
chain, and is the quickest way to see where in the chain a request is being
stopped.

## Scope

In scope: `src/Zsc.ApiGateway`, `src/Zsc.Interceptor`, `src/Zsc.Bff`,
`src/Zsc.CommonRoutes`, `src/Zsc.HealthStatus`, and the test projects.

Out of scope: key issuance/rotation/revocation, an inventory of who currently
calls the Health Status API, rate limiting, TLS, and anything about
`src/Zsc.DeviceApi` other than it continuing to work exactly as it does now.
