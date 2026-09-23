# R1 implementation notes

## Files changed

- `src/Zsc.CommonRoutes/ZscAuth.cs` registers Bearer, SubscriptionKey, and `ZscSmart` for every service, selects the handler by route and key-header presence, and retains the authenticated-user fallback policy.
- `src/Zsc.CommonRoutes/HealthStatusRoutePredicate.cs` defines the shared public and internal health-status path predicate.
- `src/Zsc.CommonRoutes/SubscriptionKeyValidator.cs` classifies missing, invalid, and valid keys using ordinal (case-sensitive) comparison.
- `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` returns NoResult for a missing header, fails invalid keys, and creates an authenticated principal for configured keys.
- `src/Zsc.CommonRoutes/ZscHeaders.cs` centralizes the subscription-key header name.
- `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` carries the subscription-key header to downstream services along with the existing forwarded identity headers.
- `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` permits `Ocp-Apim-Subscription-Key` at the edge.
- `src/Zsc.ApiGateway/appsettings.json`, `src/Zsc.Interceptor/appsettings.json`, `src/Zsc.Bff/appsettings.json`, `src/Zsc.HealthStatus/appsettings.json`, and `src/Zsc.DeviceApi/appsettings.json` each provision exactly the demo key under `Zsc:SubscriptionKeys`.
- `tests/Zsc.CommonRoutes.Tests/HealthStatusRoutePredicateTests.cs` tests route matches and non-matches.
- `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs` tests missing, invalid, exact/case-sensitive, demo-key and wrong-key results.
- `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` changes bearer-only health access to 200 with a transition rationale and checks `wrong-key-000` returns 401.
- `scripts/probe_auth.py` captures the nine requested gateway auth cases using only Python's `urllib` standard library.
- `scripts/run-e2e.sh` runs the E2E suite with `ZSC_E2E=1`.
- `results/before-probe.txt`, `results/before-smoke.txt`, and `results/before-unit-tests.txt` preserve the before captures; corresponding `after-*` results preserve after captures.
- `results/after-diff-stat.txt` captures the requested diff summary; `results/report-spec.json` is the PDF input spec; `results/R1-Report.pdf` is the evidence report.

## Header propagation and validation

The gateway edge allowlist retains the incoming subscription header. Gateway and each downstream authentication boundary use the same `ZscSmart` policy scheme. The gateway's `TokenForwardingHandler` forwards the key to the Interceptor; the Interceptor forwards it to the BFF; the BFF forwards it to Health Status. The accepted URL prefixes are `/api/v1/health/` (gateway, Interceptor and BFF) and `/internal/health/` (Health Status). At each hop the selector chooses `SubscriptionKey` only when the request has the key header and is a health-status route. `Zsc:SubscriptionKeys` is the configuration section; the provisioned value in each service is `zsc-demo-subscription-key-001`.

A bad key is rejected at the first authentication boundary receiving it, the API Interceptor after the gateway forwards it. The handler's validator returns Invalid, authentication fails, and the standard challenge is 401; the request does not reach BFF. A missing key header returns NoResult and, without a bearer, the authenticated-user fallback policy challenges with 401.

## Coexistence and scope

This implementation deliberately differs from the requirement document's hard cutover: both a valid subscription key and a valid OAuth2 bearer are accepted for health-status routes. A request with a key header is dispatched to the subscription-key scheme at that hop; bearer-only requests use the Bearer scheme. Other paths always select Bearer even if a subscription key is supplied, so device and all other APIs remain OAuth2-only. `/healthz` remains explicitly anonymous.

## Rollback to hard cutover

To return to the documented hard cutover, keep the key header routing for health-status routes but change the bearer-only health-status E2E expectations back to 401 and have `ZscSmart` select the subscription-key scheme for every health-status request (including when the header is absent). This removes OAuth2 as an accepted Health Status credential without changing authentication for other APIs. Update the accompanying transition comment and re-run the suites.
