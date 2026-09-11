# Requirement #1 — implementation notes

This is the change log for `docs/REQUIREMENT-R1.md`, and the one deviation
taken from it.

## The deviation from the requirement document

`docs/REQUIREMENT-R1.md`, "Decisions taken", chose a hard cutover: after the
change, an OAuth2 bearer alone would no longer open the Health Status API.
**This implementation deviates from that.** The Health Status API (both ZSC
and ZLS) accepts **either** a valid `Ocp-Apim-Subscription-Key` **or** a valid
OAuth2 bearer. Every other ZSC API (device status, and anything added later
that does not opt in) stays OAuth2-only, exactly as R1.3 requires.

Reason: a hard cutover breaks every caller still using a bearer token the
moment the change ships, with no overlap window. Accepting either credential
lets consumers move to the subscription key on their own schedule while the
key becomes available; nothing about that requires OAuth2 to be turned off on
day one. The two acceptance tests this touches are called out below.

## Files changed, and why

| File | Why |
|---|---|
| `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` | New. Compares a header value against the provisioned keys (`Zsc:SubscriptionKeys`). Kept independent of ASP.NET Core's authentication types so it is unit-testable on its own; returns `Missing` / `Invalid` / `Valid` rather than a bool so the caller (the auth handler) can turn `Missing` into `NoResult` and `Invalid` into `Fail` - they are not the same failure. |
| `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` | New. The ASP.NET Core `AuthenticationHandler` for the `SubscriptionKey` scheme. Reads `Ocp-Apim-Subscription-Key`, delegates the comparison to `SubscriptionKeyValidator`, and maps the result to `NoResult`/`Fail`/`Success`. |
| `src/Zsc.CommonRoutes/ZscHealthStatusRoute.cs` | New. `IsHealthStatusRoute(path)` - the one place that says which paths are "the Health Status API" (`/api/v1/health/...` at the edge, `/internal/health/...` at health-status). Every service and the policy scheme below share this one predicate instead of each re-deriving it. |
| `src/Zsc.CommonRoutes/ZscAuth.cs` | Rewrote `AddZscPlatformAuth`. Registers three schemes: `Bearer` (JwtBearer, unchanged), `SubscriptionKey` (the new handler), and `ZscSmart` (an `AddPolicyScheme` whose `ForwardDefaultSelector` picks `SubscriptionKey` when the request carries the header **and** the path is a Health Status route, otherwise `Bearer`). `ZscSmart` is now the default authenticate/challenge scheme and the `FallbackPolicy`'s scheme - this is what makes coexistence a per-route decision without any service declaring per-route authorization metadata. |
| `src/Zsc.CommonRoutes/ZscHeaders.cs` | Added `SubscriptionKey` (`Ocp-Apim-Subscription-Key`) to the platform's known header list, next to `Authorization` and `X-Correlation-Id`. |
| `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` | Forwards the subscription key header downstream, the same way it already forwards `Authorization` and `X-Correlation-Id`. Without this the key would stop at whichever hop first attached the handler and never reach health-status. |
| `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` | Added the subscription key header to the edge allowlist. Without this the gateway strips it from every inbound request before the chain sees it (see `docs/ARCHITECTURE.md`, "Where headers are narrowed"). |
| `src/Zsc.ApiGateway/appsettings.json`, `src/Zsc.Bff/appsettings.json`, `src/Zsc.Interceptor/appsettings.json`, `src/Zsc.HealthStatus/appsettings.json`, `src/Zsc.DeviceApi/appsettings.json` | Added `Zsc:SubscriptionKeys: ["zsc-demo-subscription-key-001"]`. Every service provisions the same one key because every service installs `AddZscPlatformAuth` and therefore hosts all three schemes, even device-api and the gateway, which never actually get a subscription-keyed request in this demo's routing. Keeping the section present everywhere means no service is a special case, and a future new health-adjacent service just works. |
| `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` | New. `Missing`, `Invalid` (both the general case and the documented `wrong-key-000`), `Valid` (the demo key), and case sensitivity. |
| `tests/Zsc.CommonRoutes.Tests/ZscHealthStatusRouteTests.cs` | New. Pins which paths `IsHealthStatusRoute` does and does not recognise - the public and internal health prefixes, and that device/healthz paths are not swept in. |
| `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` | The case pinning "bearer alone -> 401" (`Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone`) is now `Platform_health_status_still_accepts_an_oauth2_bearer_alone`, asserting 200 - this is the coexistence deviation, called out in a comment on the test itself. The wrong-key case (`Platform_health_status_rejects_an_unknown_subscription_key`) already existed with an unprovisioned key; it now uses the documented `wrong-key-000` value, per the brief's "add a wrong-key -> 401 case if missing" - none was missing, so the existing one was pointed at the specific value instead of adding a duplicate. |

No changes were needed to `src/Zsc.HealthStatus/Program.cs`, `src/Zsc.Interceptor/Program.cs`, `src/Zsc.Bff/Program.cs`, `src/Zsc.ApiGateway/Program.cs` or `src/Zsc.DeviceApi/Program.cs` - none of them call `AddAuthentication`/`AddJwtBearer` directly; they all go through `AddZscPlatformAuth`, so the new schemes and the smart selector became available to every hop by changing `ZscAuth.cs` alone.

## Header-propagation path

```
client --Ocp-Apim-Subscription-Key: <key>-->
  api-gateway :5080   EdgeHeaderPolicyMiddleware keeps the header (now allowlisted)
  --forward-->
  interceptor :5100   TokenForwardingHandler copies it onto the outbound request
  --forward-->
  bff :5200           TokenForwardingHandler copies it again
  --forward-->
  health-status :5300 SubscriptionKeyAuthenticationHandler reads it, ZscSmart selected
                       this scheme because the path matched ZscHealthStatusRoute
```

Same three hops (gateway allowlist, then two `TokenForwardingHandler` copies)
that `Authorization` already took - the subscription key was added next to it
at each of those three places rather than inventing a new mechanism.

## Configuration key

`Zsc:SubscriptionKeys` - a JSON array of provisioned keys, read by
`SubscriptionKeyValidator`. The demo provisions exactly one:
`zsc-demo-subscription-key-001`, in every service's `appsettings.json`. No key
is hard-coded in source; `SubscriptionKeyValidator`'s only non-test
constructor takes an `IConfiguration` and reads this section.

## Where and how a wrong key is rejected

`wrong-key-000` (or any value not in `Zsc:SubscriptionKeys`) reaches
health-status - the gateway allowlists the header, both `TokenForwardingHandler`
hops copy it - and is rejected **at health-status itself**, the service that
terminates the scheme. `SubscriptionKeyAuthenticationHandler.HandleAuthenticateAsync`
calls `SubscriptionKeyValidator.Validate`, which returns `Invalid` for an
unprovisioned key; the handler turns that into `AuthenticateResult.Fail(...)`,
and the `FallbackPolicy`'s `RequireAuthenticatedUser()` turns the failed
authentication into a 401. That 401 is returned untouched through bff,
interceptor and the gateway back to the caller (see `docs/ARCHITECTURE.md`,
"The downstream status code and body are returned untouched at each hop").

A caller with **no credentials at all** on a Health Status route also gets a
401, for a different technical reason: `SubscriptionKeyAuthenticationHandler`
returns `NoResult` (not `Fail`) when the header is absent, so the `ZscSmart`
policy scheme's authentication attempt for that request resolves to "not
authenticated" via `Bearer` (also absent) rather than a `SubscriptionKey`
failure specifically - either way, `RequireAuthenticatedUser()` still rejects
it with 401.

## Coexistence policy

- **Health Status API only** (`/api/v1/health/zsc/status`, `/api/v1/health/zls/status`,
  and their `/internal/health/...` counterparts): a valid subscription key OR
  a valid bearer authenticates the caller. `ZscHealthStatusRoute.IsHealthStatusRoute`
  is the single predicate deciding which paths this applies to.
- **Every other ZSC API** (device status, and anything else): OAuth2 bearer
  only. A subscription key on `/api/v1/devices/.../status` is not even
  considered - `ZscAuth`'s `ForwardDefaultSelector` only forwards to the
  `SubscriptionKey` scheme when the path matches `IsHealthStatusRoute`, so a
  non-health-status request always authenticates against `Bearer`, and a
  request that presents only a subscription key on such a route has no bearer
  either and is rejected 401.
- `/healthz` on every service stays anonymous everywhere, unaffected by any of
  this - `ZscLiveness.MapZscLiveness` calls `.AllowAnonymous()`, which bypasses
  the `FallbackPolicy` (and therefore `ZscSmart`) entirely.

## Rollback to a hard cutover

To restore the original requirement document's hard cutover (bearer no longer
works on Health Status routes), two edits undo the coexistence:

1. In `ZscAuth.ForwardDefaultSelector`, make health-status routes always select
   `SubscriptionKeyScheme` (drop the `carriesSubscriptionKey &&` condition) -
   a caller on a health-status route with no key at all then authenticates
   against `SubscriptionKey`, which returns `NoResult` for a missing header,
   and still 401s (no scheme succeeds), but a caller who now presents a bearer
   instead of a key is also routed to `SubscriptionKey`, which ignores the
   bearer and returns `NoResult` too, so bearer-alone goes back to 401 exactly
   as the original requirement specifies.
2. In `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`, flip
   `Platform_health_status_still_accepts_an_oauth2_bearer_alone` back to
   asserting `Unauthorized`, renaming it to reflect the cutover (e.g. back to
   `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone`).

No configuration or header-propagation change is needed either way - the key
travels the chain and is provisioned identically under both policies; only
which scheme the policy scheme selects changes.
