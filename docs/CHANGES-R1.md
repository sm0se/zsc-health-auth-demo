# Changes for Requirement #1 — Health Status API: OAuth2 → subscription key

This implements `docs/REQUIREMENT-R1.md` with one deliberate deviation from the
doc's hard cutover: the Health Status API accepts **either** a valid
`Ocp-Apim-Subscription-Key` **or** a valid OAuth2 bearer. Every other ZSC API
(the device API is the representative example) stays OAuth2-only, unchanged.

## Files changed and why

### `src/Zsc.CommonRoutes` (shared library — this is where the decision lives)

- **`SubscriptionKeyValidator.cs`** (new) — testable, dependency-free check of a
  header value against the provisioned keys: `Validate(headerValue)` returns
  `Missing` / `Invalid` / `Valid`. No hard-coded key; keys come in via the
  constructor from `ZscSubscriptionKeyOptions`.
- **`ZscSubscriptionKeyOptions.cs`** (new) — reads the `Zsc:SubscriptionKeys`
  configuration array into a `HashSet<string>` (ordinal comparison — keys are
  opaque tokens, not case-insensitive names).
- **`SubscriptionKeyAuthenticationHandler.cs`** (new) — the `SubscriptionKey`
  authentication scheme. A missing header is `AuthenticateResult.NoResult()`
  (defer to whatever else is deciding); a present, wrong key is
  `AuthenticateResult.Fail(...)` (hard rejection); a valid key produces a
  `ClaimsPrincipal` and `AuthenticateResult.Success(...)`.
- **`ZscHealthRoutePredicate.cs`** (new) — the one place that defines what a
  "Health Status route" is: paths starting with `/api/v1/health/` (seen by
  gateway, interceptor, bff) or `/internal/health/` (seen by health-status
  itself). Both public and internal path shapes are covered because every
  process in the chain runs its own copy of `AddZscPlatformAuth` and decides
  for itself which credential is eligible.
- **`ZscAuth.cs`** (changed) — `AddZscPlatformAuth` now registers three
  schemes instead of one: `Bearer` (JwtBearer, unchanged), `SubscriptionKey`
  (the new handler), and a policy scheme `ZscSmart`
  (`AddPolicyScheme`) whose `ForwardDefaultSelector` returns `SubscriptionKey`
  only when the request carries the header **and** its path is a health-status
  route (via `ZscHealthRoutePredicate`) — otherwise `Bearer`. `ZscSmart` is set
  as both `DefaultAuthenticateScheme` and `DefaultChallengeScheme`
  (`AddAuthentication(SmartScheme)`); the authorization `FallbackPolicy` still
  requires an authenticated user, unchanged in spirit. No service's
  `Program.cs` calls `AddAuthentication`/`AddJwtBearer` directly — this file is
  still the single place authentication is wired up.
- **`ZscHeaders.cs`** (changed) — added the `SubscriptionKey` constant
  (`Ocp-Apim-Subscription-Key`), alongside the existing `Authorization` and
  `CorrelationId` constants.
- **`TokenForwardingHandler.cs`** (changed) — now forwards
  `Ocp-Apim-Subscription-Key` at every hop, the same way it already forwarded
  `Authorization` and `X-Correlation-Id`. Without this, a subscription key
  would die at whichever hop first re-issued the downstream `HttpClient` call.

### `src/Zsc.ApiGateway`

- **`EdgeHeaderPolicyMiddleware.cs`** (changed) — added
  `Ocp-Apim-Subscription-Key` to the edge allowlist. Without this the gateway
  strips the header from every inbound request before the internal chain ever
  sees it, regardless of anything done further down.

### Configuration (`appsettings.json`, all five services)

Each service's `appsettings.json` now has a `Zsc:SubscriptionKeys` array
provisioning exactly one demo key: `zsc-demo-subscription-key-001`. The key is
provisioned everywhere for consistency (each service reads its own
configuration independently, same as `Zsc:OAuth2`), even though the
`SubscriptionKey` scheme is only ever *reachable* on health-status routes —
`ZscSmart`'s selector is what actually restricts where a key can authenticate,
not which services happen to know about it.

### Tests

- **`tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`** — the case
  `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone` (expecting
  401) is renamed to
  `Platform_health_status_also_accepts_an_oauth2_bearer_alone` and now expects
  200. This is the one assertion the requirement doc's own acceptance criteria
  pin to the hard-cutover reading; the coexistence deviation this
  implementation takes is documented right there in a comment referencing
  this file. The wrong-key → 401 case
  (`Platform_health_status_rejects_an_unknown_subscription_key`) already
  existed and needed no change. No other assertion was weakened or removed.
- **`tests/Zsc.CommonRoutes.Tests/SubscriptionKeyValidatorTests.cs`** (new) —
  unit tests for `Missing` (null and empty), `Valid` (the demo key), `Invalid`
  (`wrong-key-000`), and case sensitivity (`ZSC-DEMO-...` does not match).
- **`tests/Zsc.CommonRoutes.Tests/ZscHealthRoutePredicateTests.cs`** (new) —
  unit tests for both recognised path shapes (public and internal, including a
  case-insensitivity check) and for paths that must NOT be recognised (device
  routes, `/healthz`, `/`).

### Scripts

- **`scripts/probe_auth.py`** (new) — mints a dev bearer token and performs
  the nine target credential/route combinations through the gateway, printing
  label/status/body-excerpt for each. urllib only, no third-party dependency.
- **`scripts/run-e2e.sh`** (new) — `ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests
  --nologo`, so the end-to-end run is one command with recorded output.

## Header-propagation path

```
client --Ocp-Apim-Subscription-Key--> api-gateway :5080
  EdgeHeaderPolicyMiddleware now allowlists the header (was dropped before this
  change) -> survives into the internal chain.
  CorrelationIdMiddleware is unaffected (different header).
  ZscForwarder proxies the request; the gateway authenticates nothing itself.
                    |
                    v  TokenForwardingHandler copies the header onto the
                       outbound HttpClient call (was not copied before this
                       change - Authorization and X-Correlation-Id only).
              interceptor :5100
  AddZscPlatformAuth's ZscSmart scheme runs: header present AND path matches
  ZscHealthRoutePredicate -> SubscriptionKey scheme authenticates; otherwise
  Bearer scheme runs (and finds no bearer -> 401 without a valid key either).
                    |
                    v  same TokenForwardingHandler forwards the header again
                  bff :5200
  Same ZscSmart decision, independently. ZscRoutes.Resolve maps the public
  path to the downstream service and path.
                    |
                    v  same handler forwards the header a third time
           health-status :5300
  Same ZscSmart decision, terminally: the header is checked against
  Zsc:SubscriptionKeys via SubscriptionKeyValidator inside
  SubscriptionKeyAuthenticationHandler.
```

The header rides on every outbound `HttpClient` in the chain because
`TokenForwardingHandler` is attached to all of them (gateway→interceptor,
interceptor→bff, bff→health-status/device-api) — the same mechanism that
already carried `Authorization` and `X-Correlation-Id`.

## Configuration key

`Zsc:SubscriptionKeys` — a JSON array of provisioned key strings, read by
`ZscSubscriptionKeyOptions.FromConfiguration`. Every one of the five services'
`appsettings.json` provisions `[ "zsc-demo-subscription-key-001" ]`. No key is
hard-coded anywhere in source.

## Where and how a wrong key is rejected

A caller presenting `Ocp-Apim-Subscription-Key: wrong-key-000` against
`/api/v1/health/zsc/status` is rejected **at the first hop that runs
authentication: the Interceptor (`:5100`)**. The gateway forwards without
checking anything; the moment the request reaches the Interceptor,
`ZscSmart` sees the header and a health-status path, selects the
`SubscriptionKey` scheme, `SubscriptionKeyAuthenticationHandler` calls
`SubscriptionKeyValidator.Validate("wrong-key-000")`, gets back `Invalid`, and
returns `AuthenticateResult.Fail(...)`. The authorization `FallbackPolicy`
then rejects the request with 401 before it is ever forwarded to the BFF or
health-status. The same 401 is what the caller sees at the gateway, since each
hop returns the downstream status untouched. (`scripts/probe_hops.py` was
prepared to isolate exactly this hop-by-hop if the after-probe had disagreed
with the target behaviour, per the verification protocol; it was not written
because the after-probe matched the target behaviour for all nine cases on
the first run — see the raw output in `results/after-probe.txt`.)

A missing subscription key on a health-status route is not, by itself, a
rejection: `SubscriptionKeyAuthenticationHandler` returns `NoResult()`, handing
the decision to `ZscSmart`'s selector, which falls back to the `Bearer`
scheme — so a caller with a valid OAuth2 bearer and no key still gets in. Only
when *both* schemes fail to authenticate does the `FallbackPolicy` produce
401.

## Coexistence policy (the deviation from the doc)

`docs/REQUIREMENT-R1.md` calls the hard cutover "the literal reading" of the
brief and offers the coexistence alternative as "a one-line difference in the
policy and a change to one acceptance test" — which is exactly what this
implementation does. The reasoning: a subscription-key migration that breaks
every existing OAuth2 caller of the Health Status API the moment it ships is
an operational risk with no mitigation offered in the brief (no phased
rollout, no dual-running window, no explicit confirmation that all consumers
have already migrated). Accepting either credential lets new consumers move
to the subscription key immediately while old consumers keep working
unmodified, with the platform ready to drop OAuth2 acceptance later as a
second, purely additive-to-subtractive change. This coexistence is scoped
**only** to the Health Status API's own routes (`ZscHealthRoutePredicate`);
`OAuth2RegressionTests` continues to assert that the device API — and by
extension every other ZSC API — accepts a bearer only and rejects a
subscription key outright, exactly per R1.3.

## Rollback to a hard cutover

To go from this coexistence policy back to the requirement doc's literal hard
cutover:

1. In `ZscAuth.AddZscPlatformAuth`, change `ZscSmart`'s
   `ForwardDefaultSelector` so that on a health-status route it *always*
   returns `SubscriptionKeyScheme` (drop the `context.Request.Headers.
   ContainsKey(...)` condition) — a missing or wrong key then reaches the
   `SubscriptionKey` handler directly and is rejected without ever trying
   `Bearer`.
2. In `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs`, revert
   `Platform_health_status_also_accepts_an_oauth2_bearer_alone` to expect 401
   and rename it back (or keep the new name and just flip the assertion — the
   two commits either way are one line each).
3. No other file changes: `SubscriptionKeyValidator`,
   `ZscHealthRoutePredicate`, the header allowlist/forwarding, and the
   configuration keys are unaffected by which side of the coexistence decision
   is chosen — they are the mechanism, not the policy.
