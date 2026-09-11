# R1 — subscription-key auth for the Health Status API (OAuth2 kept)

## Summary

The ZSC/ZLS Health Status API now accepts **either** a valid
`Ocp-Apim-Subscription-Key` **or** a valid OAuth2 bearer token. Every other
ZSC API (the device API is the one in this repository) stays OAuth2-only,
exactly as before. This is the one deliberate deviation from
`docs/REQUIREMENT-R1.md`'s "hard cutover" decision — see "Coexistence
policy" below for why.

## Files changed and why

| File | Why |
|---|---|
| `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` (new) | Testable key check, independent of ASP.NET Core authentication. `Validate(headerValue)` returns `Missing` / `Invalid` / `Valid`. Reads provisioned keys from `IConfiguration` section `Zsc:SubscriptionKeys` — no key is hard-coded. |
| `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` (new) | Terminates the `SubscriptionKey` authentication scheme. Reads the `Ocp-Apim-Subscription-Key` header, calls `SubscriptionKeyValidator`, and maps the outcome to `AuthenticateResult`: missing/empty header → `NoResult()`, unrecognised key → `Fail(...)`, valid key → `Success(...)`. |
| `src/Zsc.CommonRoutes/ZscHealthStatusRoutePolicy.cs` (new) | Defines, once, what "a health-status route" means: a path starting with `/api/v1/health/` (gateway, interceptor, bff) or `/internal/health/` (health-status). Used by the scheme selector and unit-tested directly. |
| `src/Zsc.CommonRoutes/ZscHeaders.cs` | Added `SubscriptionKey = "Ocp-Apim-Subscription-Key"` alongside the existing `Authorization` and `CorrelationId` constants, so it is named once and used everywhere it needs to travel. |
| `src/Zsc.CommonRoutes/ZscAuth.cs` | `AddZscPlatformAuth` now registers three schemes: `Bearer` (unchanged JwtBearer), `SubscriptionKey` (new handler), and `ZscSmart` (`AddPolicyScheme`), which is now the default authenticate/challenge scheme. `ZscSmart`'s `ForwardDefaultSelector` returns `SubscriptionKey` only when the request both carries the subscription-key header and is on a health-status route; every other request — including a health-status request with no key header — goes to `Bearer`. The authorization `FallbackPolicy` still just requires an authenticated user against `ZscSmart`, so authentication is still a single process-wide decision at startup; no service declares per-route authorization metadata. |
| `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` | Added `Ocp-Apim-Subscription-Key` to the edge allowlist. Without this the gateway strips the header before it enters the internal chain, and no downstream service ever sees it. |
| `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` | Forwards the subscription-key header onto the outbound `HttpClient` alongside `Authorization` and `X-Correlation-Id`. Without this the header dies at whichever hop first makes an outbound call, even though the gateway let it in. |
| `src/Zsc.ApiGateway/appsettings.json`, `src/Zsc.Interceptor/appsettings.json`, `src/Zsc.Bff/appsettings.json`, `src/Zsc.HealthStatus/appsettings.json`, `src/Zsc.DeviceApi/appsettings.json` | Each now provisions `Zsc:SubscriptionKeys: ["zsc-demo-subscription-key-001"]`. Every service registers `SubscriptionKeyValidator`, so every service needs the section even though only health-status's own process ever ends up serving `SubscriptionKey`-authenticated requests for real; the gateway/interceptor/bff processes construct the validator as part of `AddZscPlatformAuth` regardless of whether their own routes use it. |
| `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` (new) | Unit tests for `SubscriptionKeyValidator`: missing, invalid, valid, the provisioned demo key, the `wrong-key-000` value from the acceptance brief, case sensitivity (uppercasing the demo key must not validate), and the empty-provisioning edge case. |
| `tests/Zsc.CommonRoutes.Tests/ZscHealthStatusRoutePolicyTests.cs` (new) | Unit tests for `ZscHealthStatusRoutePolicy.IsHealthStatusRoute`: both prefixes, both the public and internal shapes, a representative set of non-matching paths (`/healthz`, the device route), and that the check is case-insensitive on the path. |
| `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` | `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone` (expected `401`) renamed to `Platform_health_status_still_accepts_an_oauth2_bearer_alone` and now asserts `200`, with a comment pointing at this document and at the "Decisions taken" table in `docs/REQUIREMENT-R1.md`. The other three cases (valid key → 200, wrong key → 401, no credentials → 401) were already present and are unchanged. |
| `scripts/probe_auth.py` (new) | stdlib-only probe of the nine target cases from the task brief: mints a token via `POST /dev/token`, then calls both health routes with the valid key, the wrong key, no credentials and a bearer alone, plus the device route with key-alone and bearer, plus `/healthz`. Used for the before/after evidence in `results/`. |
| `scripts/run-e2e.sh` (new) | `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo` — the one-line wrapper the task asked for, so the E2E run is reproducible without remembering the environment variable. |

`scripts/smoke.sh` was not changed: its assertions already encoded the R1
target behaviour (subscription key → 200 on both health routes, bearer →
200 on both health routes, subscription key → 401 on the device route) —
they were failing on the two subscription-key checks before this change and
pass now. `docs/REQUIREMENT-R1.md` was not changed: it documents the
brief as received and the hard-cutover decision as originally taken; the
one place the deviation is recorded as a decision is this document.

## Header-propagation path

```
client
  │  Ocp-Apim-Subscription-Key: zsc-demo-subscription-key-001   (or Authorization: Bearer <jwt>)
  ▼
api-gateway :5080
  │  EdgeHeaderPolicyMiddleware now allowlists Ocp-Apim-Subscription-Key -
  │  previously it would have been dropped here before the header ever left
  │  the edge. The gateway still authenticates nothing itself.
  ▼
interceptor :5100
  │  ZscSmart selects a scheme per request: subscription key + health-status
  │  path -> SubscriptionKey scheme; otherwise -> Bearer. Whichever scheme
  │  wins, TokenForwardingHandler copies Authorization, X-Correlation-Id AND
  │  Ocp-Apim-Subscription-Key onto the outbound call to bff - so a header
  │  this hop did not itself need to authenticate still reaches the next one.
  ▼
bff :5200
  │  Same ZscSmart selection, same forwarding, onward to health-status (or
  │  device-api, where the key is never a valid credential).
  ▼
health-status :5300                        device-api :5400
  ZscSmart selects SubscriptionKey only     ZscSmart never selects
  on /internal/health/{zsc|zls}/status      SubscriptionKey - device-api's
  when the key header is present;           routes are not health-status
  SubscriptionKeyAuthenticationHandler       routes (ZscHealthStatusRoutePolicy),
  terminates it there.                       so a key alone -> 401 there too.
```

Every hop between the gateway and health-status has to carry the header even
though only health-status's own `SubscriptionKeyAuthenticationHandler`
terminates it — the same reason the OAuth2 bearer has always had to travel
the whole chain (each process validates for itself, per
`docs/ARCHITECTURE.md`). `EdgeHeaderPolicyMiddleware` and
`TokenForwardingHandler` are the two places a new header has to be taught to
the platform; missing either one means a request with a valid key would
still get rejected at the edge or partway down, which is exactly the failure
mode step 3 of the verification plan below is there to catch.

## Configuration key

`Zsc:SubscriptionKeys` — a JSON array of provisioned keys, read by
`SubscriptionKeyValidator` in every service that calls
`AddZscPlatformAuth`. The demo provisions exactly one key in every service's
`appsettings.json`:

```json
"Zsc": {
  "SubscriptionKeys": ["zsc-demo-subscription-key-001"]
}
```

No key is hard-coded in source; `SubscriptionKeyValidator`'s constructor
that takes an `IEnumerable<string>` directly exists only so unit tests can
supply keys without standing up configuration.

## Where and how a wrong key is rejected

A request with an unrecognised key (e.g. `wrong-key-000`) is rejected at the
**first hop that terminates authentication for the route it is calling** —
which, for the health-status routes, is `health-status:5300` itself, because
that is the only process whose `SubscriptionKeyAuthenticationHandler`
actually runs against the caller's original header; gateway/interceptor/bff
forward the request without evaluating credentials themselves (per
`docs/ARCHITECTURE.md`, "Where authentication is decided" — that has not
changed). Mechanically: `SubscriptionKeyValidator.Validate` returns
`Invalid`, the handler returns `AuthenticateResult.Fail(...)`, `ZscSmart`
had already forwarded to the `SubscriptionKey` scheme because the route is a
health-status route and the header was present, and the authorization
`FallbackPolicy` then rejects the unauthenticated request with `401`. The
401 travels back up through bff → interceptor → gateway untouched, exactly
as any other downstream 401 does.

For a route that is **not** a health-status route (the device API), the
subscription key is never even considered: `ZscHealthStatusRoutePolicy`
returns `false` for `/api/v1/devices/.../status`, so `ZscSmart` always
selects `Bearer` there regardless of what headers the caller sent, and a
subscription key (right or wrong) is simply not a credential the `Bearer`
scheme understands — `401`.

`scripts/probe_hops.py`, if step 3 of the verification plan had needed it,
would have sent the key-only request directly to `:5300`, `:5200`, `:5100`
and `:5080` in that order to find the first hop returning 401 for a valid
key — the standard way to localise a "which of the four processes forgot to
forward/accept the header" bug. It was not needed this time: every
after-probe case matched the target on the first pass (see
`results/after-probe.txt`), so no such script was written.

## Coexistence policy (the deviation from a hard cutover)

`docs/REQUIREMENT-R1.md` explicitly names the hard cutover as a decision,
not a fact handed down by the requirement text, and calls out the additive
alternative as "a one-line difference in the policy and a change to one
acceptance test":

> Is OAuth2 retained on the Health Status API during a transition? **No —
> hard cutover.** [...] The additive alternative (accept either) is a
> one-line difference in the policy and a change to one acceptance test.

This change takes that additive alternative. The Health Status API accepts
either credential; every other ZSC API accepts only OAuth2, unchanged. The
`ZscSmart` policy scheme's `ForwardDefaultSelector` is the "one-line
difference in policy" (`ZscAuth.SelectScheme`); the one changed acceptance
test is `HealthStatusAuthenticationTests.Platform_health_status_still_accepts_an_oauth2_bearer_alone`.
The rationale for taking the deviation, in this task's own words: it lets
the legacy OAuth2 method keep working so existing callers are not broken the
moment this change ships, while still delivering R1.1/R1.2 (subscription-key
auth on the Health Status API) and R1.3 (every other API stays OAuth2-only)
exactly as specified.

## Rollback to a hard cutover

To restore the hard cutover the doc originally specified:

1. In `src/Zsc.CommonRoutes/ZscAuth.cs`, change `SelectScheme` so that a
   health-status route with a **missing or invalid** subscription key still
   resolves to `SubscriptionKeyScheme` (never `OAuth2Scheme`) — i.e. drop the
   `hasSubscriptionKey` condition for health-status routes entirely; a
   caller with a bearer and no key would then hit the `SubscriptionKey`
   handler, get `NoResult()` (no header), and fall through the fallback
   policy to `401`.
2. In `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`, revert
   `Platform_health_status_still_accepts_an_oauth2_bearer_alone` to assert
   `401` again (and restore its original name and comment).
3. Re-run `scripts/smoke.sh` and `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests`
   — the "OAuth2 (current behaviour)" checks for the two health routes in
   `smoke.sh` would need updating from `200` to `401` at that point, since
   they currently assert the coexistence behaviour.

Nothing else in this change needs to be undone for a hard cutover: the
`SubscriptionKeyValidator`, the handler, the route policy, the
configuration key, and the header allowlisting/forwarding are all still
correct under a hard cutover — only the selector's condition and the two
test expectations it drives change.

## Verification (before/after, real counts)

Before any code change, with the five services running on the pre-change
build:

- `results/before-probe.txt` — the nine target cases against the unmodified
  platform: both health routes reject the valid key, the wrong key and no
  credentials with `401`, and accept a bearer alone with `200` (the
  then-current OAuth2-only behaviour); the device route accepts a bearer and
  rejects a key alone; `/healthz` is anonymous.
- `results/before-smoke.txt` — `scripts/smoke.sh`: 6 of 8 checks pass; the
  two subscription-key checks on the health routes fail with `401` where
  `200` was expected (the smoke script already encodes the R1 target
  behaviour, so it correctly fails before the change).
- `results/before-unit-tests.txt` — `dotnet test`: 19 passed, 0 failed,
  8 skipped (E2E, gated behind `ZSC_E2E=1`).

After the code change, with a Release build and all five services stopped
and restarted from that build:

- `results/after-unit-tests.txt` — `dotnet test`: 38 passed, 0 failed,
  8 skipped (E2E). The increase from 19 to 38 is the 19 new unit tests in
  `SubscriptionKeyValidatorTests` and `ZscHealthStatusRoutePolicyTests`.
- `results/after-probe.txt` — the same nine cases: both health routes now
  return `200` for the valid key and for a bearer alone, and `401` for the
  wrong key and for no credentials; the device route still returns `401`
  for a key alone and `200` for a bearer; `/healthz` is still anonymous.
  Every case matches the target behaviour in the task brief.
- `results/after-smoke.txt` — `scripts/smoke.sh`: all 8 checks pass, 0
  failures.
- `results/after-e2e.txt` — `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests`:
  12 passed, 0 skipped, 0 failed.
- `results/after-diff-stat.txt` — `git diff --stat` against the pre-change
  tree.

See `results/R1-Report.pdf` for the same evidence assembled into a single
document, including a per-test-case table and the raw contents of every
`results/*.txt` file.
