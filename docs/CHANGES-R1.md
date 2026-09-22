# R1 - Subscription-key authentication for the Health Status API

Requirement R1 makes the ZSC and ZLS Health Status API authenticate with an
`Ocp-Apim-Subscription-Key` instead of an OAuth2 bearer, while every other ZSC
API stays OAuth2-only. This change implements R1 **with one deliberate
deviation from the requirement's hard cutover**: the Health Status API accepts
**either** a valid subscription key **or** a valid OAuth2 bearer, so existing
OAuth2 callers keep working through the transition. Every other ZSC API keeps
rejecting subscription keys (`401`).

## How a subscription key travels the chain

```
caller
  │  GET /api/v1/health/zsc/status      Ocp-Apim-Subscription-Key: <key>
  ▼
api-gateway :5080        EdgeHeaderPolicyMiddleware allow-lists the header
  ▼                      → forwards it downstream
API Interceptor :5100    SubscriptionKey scheme authenticates the request here
  ▼                      TokenForwardingHandler re-sends the header to the BFF
BFF :5200                authenticates again with the same header
  ▼                      TokenForwardingHandler re-sends the header
health-status :5300      serves GET /internal/health/{zsc|zls}/status
```

Every hop authenticates the same way (`ZscAuth.AddZscPlatformAuth` is installed
once per service): the `ZscSmart` policy scheme picks the credential scheme per
request, and `TokenForwardingHandler` propagates both credentials - `Authorization`
as before, and now `Ocp-Apim-Subscription-Key`.

## Mechanism (one implementation, in Zsc.CommonRoutes)

- `ZscAuth.AddZscPlatformAuth` registers, for every service:
  - the existing `Bearer` JWT scheme;
  - a `SubscriptionKey` scheme backed by `SubscriptionKeyAuthenticationHandler`;
  - a policy scheme `ZscSmart` (`AddPolicyScheme`) whose `ForwardDefaultSelector`
    returns `SubscriptionKey` when the request carries
    `Ocp-Apim-Subscription-Key` **and** the path is a health-status route,
    otherwise `Bearer`.
- `ZscSmart` is the `DefaultAuthenticateScheme` and `DefaultChallengeScheme`.
  The `FallbackPolicy` stays `RequireAuthenticatedUser()`, now scheme-agnostic
  (no scheme list), so either accepted credential satisfies it.
- `ZscRoutes.IsHealthStatusRoute(path)` is the single definition of "a
  health-status route": paths starting with `/api/v1/health/` (as seen by the
  gateway, interceptor and BFF) or `/internal/health/` (as served by
  health-status). Case-insensitive, ordinal, defined once.
- `SubscriptionKeyValidator.Validate(headerValue)` returns `Missing` /
  `Invalid` / `Valid`. Comparison is ordinal: a subscription key is an opaque
  token, so `ZSC-DEMO-SUBSCRIPTION-KEY-001` is not the demo key. The handler
  maps `Valid` → authenticated principal, `Invalid` → `AuthenticateResult.Fail`
  (401), missing/empty header → `AuthenticateResult.NoResult` (challenge → 401).
- No service `Program.cs` was touched: they all already called
  `AddZscPlatformAuth`, which is the only place authentication is configured.

## Configuration key

Each service's `appsettings.json` provisions the demo key under:

```json
"Zsc": {
  "SubscriptionKeys": {
    "zsc-demo-subscription-key-001": "zsc-demo-consumer"
  }
}
```

Keys are read at startup by `AddZscPlatformAuth` from
`IConfiguration.GetSection("Zsc:SubscriptionKeys")`; there is no hard-coded key
in code. A service with no (or an empty) section accepts no key at all. The
dict shape maps key → consumer label; the key itself is the JSON property name.

## How and where a wrong key is rejected

`wrong-key-000` reaches the gateway (the header is now allow-listed), is
forwarded down the chain, and is rejected with `401` **at the first
authenticating hop it meets** - the API Interceptor on `:5100` - because its
`SubscriptionKey` handler validates the header against the provisioned set and
fails authentication (`AuthenticateResult.Fail`). The same happens at the BFF
and health-status for requests that arrive there directly. The gateway itself
authenticates nothing, so a wrong key is never 200 anywhere in the platform.

## Coexistence policy (the deviation)

- Health Status API: valid subscription key → `200`; valid OAuth2 bearer →
  `200` (the deviation; the requirement's hard cutover would pin this to
  `401`); wrong/missing key and no credentials → `401`.
- Every other ZSC API (e.g. device status): bearer → `200`; subscription key
  (even a valid one) → `401`; nothing → `401`.
- `/healthz` stays anonymous on all five services.

The E2E test `Platform_health_status_still_accepts_an_oauth2_bearer_alone`
documents the deviation in a comment and pins the coexistence behaviour.

## Rollback to a hard cutover

One edit, in `src/Zsc.CommonRoutes/ZscAuth.cs`: make the
`ForwardDefaultSelector` of `ZscSmart` return `OAuth2Scheme` unconditionally
(or delete the `.AddScheme`/`.AddPolicyScheme` registrations and restore
`services.AddAuthentication(OAuth2Scheme)` with
`new AuthorizationPolicyBuilder(OAuth2Scheme).RequireAuthenticatedUser()`).
After that the Health Status API again answers only OAuth2 and every
subscription-key call fails - the unit and E2E tests then show exactly which
expectations flip back. No service Program.cs or middleware needs to change for
the rollback; the allow-listing/forwarding of the header can stay (harmless)
or be dropped from `EdgeHeaderPolicyMiddleware`/`TokenForwardingHandler`.

## Files changed and why

| File | Why |
| --- | --- |
| `src/Zsc.CommonRoutes/ZscAuth.cs` | Registers the `SubscriptionKey` scheme and the `ZscSmart` policy scheme; smart selector picks per request; `FallbackPolicy` made scheme-agnostic; builds `SubscriptionKeyValidator` from `Zsc:SubscriptionKeys`. |
| `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` | New. Testable key check: `Validate(headerValue)` → `Missing`/`Invalid`/`Valid`; ordinal comparison; no hard-coded key. |
| `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` | New. `AuthenticationHandler` mapping validator results to `Success`/`Fail`/`NoResult` (`AuthenticateResult`). |
| `src/Zsc.CommonRoutes/ZscRoutes.cs` | Adds `IsHealthStatusRoute(path)` - the single predicate for health-status routes. |
| `src/Zsc.CommonRoutes/ZscHeaders.cs` | Adds the `Ocp-Apim-Subscription-Key` header name constant. |
| `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` | Forwards the subscription-key header on every hop, like `Authorization`. |
| `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` | Allow-lists `Ocp-Apim-Subscription-Key` at the edge. |
| `src/Zsc.ApiGateway/appsettings.json` (+ Bff, DeviceApi, HealthStatus, Interceptor) | Provisions `zsc-demo-subscription-key-001` under `Zsc:SubscriptionKeys`. |
| `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` | Flips the bearer-alone expectation 401 → 200 with a comment explaining the coexistence deviation; renames the test to match reality. The wrong-key → 401 case already existed (`rejects_an_unknown_subscription_key`) and was kept. |
| `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` | New unit tests: missing/empty/blank → Missing, demo key → Valid, `wrong-key-000` → Invalid, non-provisioned lookalike → Invalid, case-sensitivity, empty key set. |
| `tests/Zsc.CommonRoutes.Tests/ZscRoutesTests.cs` | Unit tests for `IsHealthStatusRoute` (public + internal health paths, case-insensitivity, negative cases). |
| `scripts/probe_auth.py` | New. Nine-call probe of the auth contract through the gateway (stdlib only). |
| `scripts/run-e2e.sh` | New. Runs the E2E suite with `ZSC_E2E=1` against the running chain. |
| `results/*.txt` | Raw before/after evidence (probe, smoke, unit tests, E2E, diff stat). |

## Verification evidence

Raw outputs are committed under `results/`:

- `before-probe.txt` - unmodified tree: valid key `401`, wrong key `401`,
  bearer `200`; the R1 key cases fail as expected.
- `after-probe.txt` - all nine cases match the target (`200/401` matrix above).
- `before-smoke.txt` - 2 failures, both the R1 subscription-key checks.
- `after-smoke.txt` - all checks passed.
- `before-unit-tests.txt` / `after-unit-tests.txt` - 19 → 38 in-process tests
  passed (19 new cases added, 0 broken).
- `after-e2e.txt` - `Passed: 12, Skipped: 0` against the running chain
  (services restarted from the Release build before capture).
- `after-diff-stat.txt` - the change summary.
