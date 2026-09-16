# R1: subscription-key authentication for the Health Status API

Requirement source: `docs/REQUIREMENT-R1.md`.

## Deviation from the requirement

The requirement document calls for a hard cutover: the Health Status API stops
accepting OAuth2 bearer tokens and accepts only the subscription key. This
change implements the requirement with **one deliberate deviation**: the
Health Status API accepts **either** a valid `Ocp-Apim-Subscription-Key`
header **or** a valid OAuth2 bearer token. This keeps existing bearer-only
callers working while the subscription-key method is rolled out, at the cost
of the API accepting two credential types instead of one. Every other ZSC API
(the device API and anything added later) stays OAuth2-only, unaffected by
this change - the coexistence is scoped to health-status routes exactly.

## Files changed and why

- `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` (new) - the key check on
  its own, independent of ASP.NET Core authentication types, so it is unit
  testable without a host. Reads valid keys from `Zsc:SubscriptionKeys` in
  configuration; returns `Missing` / `Invalid` / `Valid`.
- `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` (new) - the
  `AuthenticationHandler` that terminates the `SubscriptionKey` scheme. Reads
  the header, delegates the decision to `SubscriptionKeyValidator`, and turns
  the result into `NoResult` (no header), `Fail` (wrong key) or `Success`
  (right key).
- `src/Zsc.CommonRoutes/ZscHealthRoutePolicy.cs` (new) - the single definition
  of what a "health-status route" is (`/api/v1/health/*` or
  `/internal/health/*`), shared by the policy scheme selector and by the unit
  tests, so the scoping decision is made in exactly one place.
- `src/Zsc.CommonRoutes/ZscAuth.cs` - now registers three schemes instead of
  one: `Bearer` (JwtBearer, unchanged validation parameters), `SubscriptionKey`
  (the new handler), and `ZscSmart` (`AddPolicyScheme`), which is the
  `DefaultAuthenticateScheme`/`DefaultChallengeScheme`. Its
  `ForwardDefaultSelector` picks `SubscriptionKey` only when the request both
  carries the header and targets a health-status route; every other request -
  including a health-status request with no header, and every request to any
  other route regardless of headers - goes to `Bearer`. The authorization
  `FallbackPolicy` (`RequireAuthenticatedUser`) is unchanged in effect; it now
  runs against `ZscSmart`.
- `src/Zsc.CommonRoutes/ZscHeaders.cs` - added the `Ocp-Apim-Subscription-Key`
  constant so every service and the header-forwarding code share one spelling.
- `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` - forwards the subscription
  key header downstream alongside `Authorization` and the correlation id, so a
  caller who authenticated to the gateway with a key still has it when the
  request reaches the Health Status API four hops later.
- `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` - added the subscription
  key header to the edge allowlist; without this the gateway strips it before
  anything downstream ever sees it.
- `src/Zsc.ApiGateway/appsettings.json`, `src/Zsc.Interceptor/appsettings.json`,
  `src/Zsc.Bff/appsettings.json`, `src/Zsc.HealthStatus/appsettings.json`,
  `src/Zsc.DeviceApi/appsettings.json` - each now provisions
  `Zsc:SubscriptionKeys: ["zsc-demo-subscription-key-001"]`. No service's
  `Program.cs` was touched to call `AddAuthentication`/`AddJwtBearer` directly;
  they already call `AddZscPlatformAuth`, which now installs all three
  schemes and the smart selector for every one of them. The key is
  provisioned everywhere (not just health-status) because each service
  validates independently - the BFF and Interceptor re-check the caller for
  themselves rather than trusting an upstream hop, per the existing
  architecture (see `docs/ARCHITECTURE.md`), and the same is true for
  subscription-key requests now that health-status routes exist behind them.
- `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` - the case that
  pinned "an OAuth2 bearer alone is no longer accepted" now pins the opposite
  (`still_accepts_an_oauth2_bearer_alone`, expects 200), with a comment
  pointing at the coexistence decision above. The unknown-key case now uses
  the literal `wrong-key-000` from the acceptance criteria.
- `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` (new) - unit
  tests for `Missing`, `Invalid`, `Valid`, case sensitivity, the demo key, and
  `wrong-key-000`.
- `tests/Zsc.CommonRoutes.Tests/ZscHealthRoutePolicyTests.cs` (new) - unit
  tests for the route predicate: both health-status prefixes recognised,
  non-health routes rejected, and case insensitivity.
- `scripts/probe_auth.py` (new) - the nine-case probe script used to prove the
  before/after behaviour through the real chain.
- `scripts/run-e2e.sh` (new) - wraps `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests
  --nologo` so the end-to-end run is one command.

`scripts/smoke.sh` needed no change: it was already written for the R1 target
behaviour (subscription key -> 200 on health routes, bearer -> 200 on health
routes, subscription key -> 401 on the device API).

## Header propagation path

```
caller --(Ocp-Apim-Subscription-Key or Authorization: Bearer)--> api-gateway
  EdgeHeaderPolicyMiddleware allowlists both headers, drops everything else
  --> interceptor (TokenForwardingHandler re-attaches both headers to the
      outbound request to bff)
  --> bff (TokenForwardingHandler re-attaches both headers to the outbound
      request to health-status or device-api)
  --> health-status / device-api
      ZscAuth's "ZscSmart" policy scheme picks SubscriptionKey or Bearer
      per request, per the rules above
```

Each hop re-authenticates the caller itself (AddZscPlatformAuth is called by
every service); nothing is decided once at the edge and trusted downstream.

## Configuration key

`Zsc:SubscriptionKeys` - an array of provisioned keys, read by
`SubscriptionKeyValidator.FromConfiguration`. Every service's
`appsettings.json` provisions exactly `zsc-demo-subscription-key-001`. No key
is hard-coded in source.

## How and where a wrong key is rejected

A request presenting `wrong-key-000` (or any other value not in
`Zsc:SubscriptionKeys`) is authenticated by the first service it reaches that
terminates the `SubscriptionKey` scheme for that route - in this chain, that
is whichever hop is first asked for a health-status path, i.e. the
api-gateway itself is a pure forwarder with no auth of its own; the first hop
that runs `AddZscPlatformAuth`'s pipeline against the request is the
Interceptor (port 5100), which is the platform's authentication boundary. Its
`SubscriptionKeyAuthenticationHandler.HandleAuthenticateAsync` calls
`SubscriptionKeyValidator.Validate("wrong-key-000")`, gets back `Invalid`, and
returns `AuthenticateResult.Fail(...)`. The `ZscSmart` policy scheme has no
other scheme to fall back to for a health-status route with the header
present, so the failure surfaces as 401 at the Interceptor and the request
never reaches the BFF, health-status or device-api. This was confirmed by the
probe (`results/after-probe.txt`): a request with `wrong-key-000` to
`/api/v1/health/zsc/status` and `/zls/status` returns 401 with an empty body.

A missing header takes a different path: `HandleAuthenticateAsync` returns
`NoResult`, but this only matters when the request reaches the
`SubscriptionKey` scheme at all - and `ZscSmart`'s `ForwardDefaultSelector`
only forwards there when the header is present. A health-status request with
no credentials at all is forwarded to `Bearer`, which likewise has nothing to
authenticate, and the `FallbackPolicy` rejects it with 401.

## Coexistence policy

On health-status routes (`/api/v1/health/*`, `/internal/health/*`): a valid
subscription key **or** a valid OAuth2 bearer authenticates the caller. A
request with both is not a defined edge case the platform cares about -
`ZscSmart` forwards to `SubscriptionKey` whenever the header is present on a
health-status route, so the key wins if both are present and valid; a wrong
key is still a hard failure at that scheme, it does not fall through to
`Bearer` even if a valid bearer is also present. This is deliberate: a bad key
is a caller error, not "no credentials", and should not be silently forgiven
because a different credential also happened to be sent. On every other
route (e.g. the device API): only `Bearer` is ever selected; a subscription
key is never inspected there.

## Rollback to a hard cutover

To move from this coexistence policy to the requirement's literal hard
cutover: change `ZscAuth.AddZscPlatformAuth`'s registration so that, for
health-status routes, the `FallbackPolicy` (or a route-specific policy) is
built with `RequireAuthenticatedUser()` restricted to
`AuthenticationSchemes = new[] { SubscriptionKeyScheme }` rather than
`ZscSmart`'s bearer fallback - or simplest, change `ZscHealthRoutePolicy`'s
callers so `ForwardDefaultSelector` always returns `SubscriptionKeyScheme` for
a health-status path regardless of headers (dropping the "AND the header is
present" clause), which makes a bearer-only request on that route resolve to
the `SubscriptionKey` scheme, get `NoResult`, and be rejected by the
`FallbackPolicy` with no scheme left to try. Then update
`tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`'s
`Platform_health_status_still_accepts_an_oauth2_bearer_alone` test back to
asserting 401, matching the original `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone`.
No other file needs to change: the subscription key handler, validator, route
policy, header allowlisting and forwarding are all cutover-agnostic.
