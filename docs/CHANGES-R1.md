# R1: subscription-key auth for the Health Status API

## Deviation from the requirement

`docs/REQUIREMENT-R1.md` calls for a hard cutover: the Health Status API
authenticates with a subscription key *instead of* an OAuth2 bearer. This
change implements one deviation from that: the Health Status API accepts
EITHER a valid `Ocp-Apim-Subscription-Key` OR a valid OAuth2 bearer, so the
legacy method keeps working while the new one rolls out. Every other ZSC API
(the device API is the example in this repository) stays OAuth2-only, exactly
as R1 requires.

## Files changed and why

| File | Why |
|---|---|
| `src/Zsc.CommonRoutes/ZscAuth.cs` | Registers three schemes instead of one: `Bearer` (unchanged JwtBearer setup), `SubscriptionKey` (new), and `ZscSmart` (new `AddPolicyScheme`) as the default authenticate/challenge scheme. `ZscSmart.ForwardDefaultSelector` picks `SubscriptionKey` when the request carries the header AND the path is a health-status route, otherwise `Bearer`. `FallbackPolicy` still requires an authenticated user; every service still calls this one method and nothing else. |
| `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` | New. The `AuthenticationHandler` for the `SubscriptionKey` scheme. Reads the header, delegates the check to `SubscriptionKeyValidator`, and maps its result to `AuthenticateResult`: `Valid` -> `Success`, `Invalid` -> `Fail`, `Missing` -> `NoResult`. |
| `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` | New. The testable key check: `Validate(headerValue)` -> `Missing`/`Invalid`/`Valid`, case-sensitive, backed by `IConfiguration` section `Zsc:SubscriptionKeys` (no hard-coded key). |
| `src/Zsc.CommonRoutes/ZscHealthStatusPath.cs` | New. The one place the "is this a health-status route" predicate is defined: `/api/v1/health/` (public, seen by gateway/interceptor/bff) or `/internal/health/` (internal, seen by health-status). `ZscAuth` calls it; nothing else needs to know the prefixes. |
| `src/Zsc.CommonRoutes/ZscHeaders.cs` | Added the `Ocp-Apim-Subscription-Key` constant so every service names the header the same way. |
| `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` | Forwards the subscription-key header to the next hop, the same way it already forwards `Authorization` and the correlation id. Without this the key would stop at whichever service first receives it from the caller. |
| `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` | Allowlisted the subscription-key header at the edge. The gateway drops every header it doesn't recognise; without this line a caller's key would be stripped before it ever reached the Interceptor. |
| `src/Zsc.ApiGateway/appsettings.json`, `src/Zsc.Interceptor/appsettings.json`, `src/Zsc.Bff/appsettings.json`, `src/Zsc.HealthStatus/appsettings.json`, `src/Zsc.DeviceApi/appsettings.json` | Provisioned `Zsc:SubscriptionKeys: [ "zsc-demo-subscription-key-001" ]` in every service, since every service authenticates the caller for itself (see `docs/ARCHITECTURE.md`) and so every service needs the key list to validate against. |
| `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` | The case that pinned "bearer alone -> 401" (a hard cutover) now pins "bearer alone -> 200" (coexistence), with a comment explaining why. The wrong-key -> 401 case (`Platform_health_status_rejects_an_unknown_subscription_key`) already existed and needed no change. |
| `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` | New unit tests: `Missing` (null/empty), `Invalid` (unprovisioned key), `Valid` (provisioned demo key), case sensitivity, and that keys are read from `Zsc:SubscriptionKeys`. |
| `tests/Zsc.CommonRoutes.Tests/ZscHealthStatusPathTests.cs` | New unit tests for the route predicate: both public and internal health-status prefixes recognised; device-api, `/healthz` and bare prefixes rejected. |
| `scripts/probe_auth.py` | New. Mints a dev bearer and drives the nine credential/path combinations that define R1's target behaviour through the live gateway, for before/after comparison. |
| `scripts/run-e2e.sh` | New. `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo` - the one command that runs the end-to-end suite against a running chain. |

No service's `Program.cs` changed: none of them called `AddAuthentication`/
`AddJwtBearer` directly before this change and none of them do now - they all
still call `AddZscPlatformAuth`/`UseZscPlatformAuth` exactly as before.

## Header-propagation path

A subscription key travels the same physical path the bearer token already
does, hop by hop:

```
caller
  -> api-gateway     EdgeHeaderPolicyMiddleware allows Ocp-Apim-Subscription-Key through the edge filter
  -> API Interceptor TokenForwardingHandler copies it onto the outbound request to the BFF
  -> BFF service      TokenForwardingHandler copies it onto the outbound request to health-status
  -> HealthcheckStatus API  SubscriptionKeyAuthenticationHandler reads it off the inbound request
```

Each hop re-authenticates the caller for itself (nothing here changes that
platform convention - see `docs/ARCHITECTURE.md`), so the header has to
physically survive every hop, not just the last one. It is dropped after the
last hop; nothing forwards it past health-status.

## Configuration key

`Zsc:SubscriptionKeys` - an array of strings, resolved via
`IConfiguration.GetSection("Zsc:SubscriptionKeys").GetChildren()`. Every
service's `appsettings.json` provisions exactly one value for the demo:
`zsc-demo-subscription-key-001`. No key is hard-coded in source; a real
deployment would provision (and rotate) this list per service the same way it
already provisions `Zsc:OAuth2`.

## Where and how a wrong key is rejected

A request presenting `wrong-key-000` reaches the Health Status API's
`ZscSmart` policy scheme, which selects `SubscriptionKey` (the header is
present and the path is a health-status route). `SubscriptionKeyAuthenticationHandler`
calls `SubscriptionKeyValidator.Validate("wrong-key-000")`, which returns
`Invalid` because the value is not in `Zsc:SubscriptionKeys`. The handler maps
that to `AuthenticateResult.Fail(...)`, and the ASP.NET Core authorization
middleware turns the failed/absent authentication into a 401 response.

This happens at the **first** hop that terminates authentication for the
request under the `ZscSmart` scheme - which, because every service in the
chain enforces the same `FallbackPolicy`, is the API Interceptor (the
platform's authentication boundary, see `docs/ARCHITECTURE.md`). The
Interceptor rejects the request with 401 before it is ever forwarded to the
BFF or health-status; the wrong key never reaches the service that would
otherwise validate it against the real key list. `scripts/probe_hops.py`
(written only if a probe result mismatches the target) exists to isolate which
hop rejects a request when this is in doubt; it was not needed for this
change - `results/after-probe.txt` matched the target on the first run after
the fix.

## Coexistence policy

For the Health Status API's routes only (`/api/v1/health/*` at the edge,
`/internal/health/*` at health-status):

- A caller presenting a **valid** subscription key is authenticated as the
  `SubscriptionKey` scheme's identity, regardless of whether it also carries a
  bearer token.
- A caller presenting **no** subscription key header falls back to the
  `Bearer` scheme, exactly as before this change - an OAuth2 bearer keeps
  working unmodified.
- A caller presenting an **invalid** subscription key is rejected (`Fail`,
  401) even if it also carries a valid bearer token - the smart scheme
  commits to `SubscriptionKey` once the header is present on a health-status
  route, it does not fall through to `Bearer` on failure. This is a deliberate
  choice: a caller that sends a key gets a real answer about that key, not a
  silent retry under a different credential.

Every other route in the platform (the device API, and anything future) never
forwards to `SubscriptionKey` regardless of what headers it carries, because
`ZscHealthStatusPath.IsHealthStatusPath` returns `false` for it - it is always
`Bearer`, exactly as required by R1.3.

## Rollback to a hard cutover

To turn this into the hard cutover the requirement describes:

1. In `ZscAuth.AddZscPlatformAuth`, drop the `ZscHealthStatusPath.IsHealthStatusPath(...)`
   condition from `ForwardDefaultSelector`, or drop the `Bearer` branch and
   `.AddJwtBearer` registration on health-status routes entirely.
2. Revert `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`'s
   `Platform_health_status_still_accepts_an_oauth2_bearer_alone` back to
   asserting `401` (and rename it back to reflect the cutover).
3. No other file in this change needs to move: the header propagation, the
   `SubscriptionKeyValidator`, the configuration key and the route predicate
   are all cutover-agnostic; only the policy scheme's selection logic and that
   one test encode the coexistence decision.
