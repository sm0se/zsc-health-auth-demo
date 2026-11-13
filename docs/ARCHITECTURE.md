# How a request travels

## The hops

```
client
  │  Authorization: Bearer <jwt>
  │  X-Correlation-Id: <optional>
  ▼
api-gateway :5080
  │  1. EdgeHeaderPolicyMiddleware drops every inbound header that is not on the
  │     edge allowlist (Authorization, X-Correlation-Id, Accept, Accept-Encoding,
  │     Content-Type, Content-Length, Host, User-Agent).
  │  2. CorrelationIdMiddleware stamps X-Correlation-Id if the caller sent none,
  │     and echoes it on the response.
  │  3. Forwards GET /api/v1/** to the Interceptor. The gateway authenticates
  │     nothing itself.
  ▼
interceptor :5100
  │  AddZscPlatformAuth installed the OAuth2 bearer scheme AND an authorization
  │  FallbackPolicy requiring an authenticated user. Every path this process
  │  serves is therefore behind OAuth2, including everything it forwards.
  │  A request without a valid bearer is rejected 401 and never reaches the BFF.
  │  Forwards GET /api/v1/** to the BFF.
  ▼
bff :5200
  │  Same AddZscPlatformAuth, so it validates the bearer again for itself.
  │  ZscRoutes.Resolve maps the public path to (downstream service, downstream
  │  path); an unregistered path is 404. Forwards to the resolved service.
  ▼
health-status :5300                        device-api :5400
  Same AddZscPlatformAuth.                 Same AddZscPlatformAuth.
  /internal/health/{zsc|zls}/status        /internal/devices/{id}/status
  Probes device-api's /healthz for the
  ZSC component list.
```

The downstream status code and body are returned untouched at each hop, so a 401
raised four hops down surfaces to the caller as a 401.

## Where authentication is decided

`ZscAuth.AddZscPlatformAuth` in `src/Zsc.CommonRoutes/ZscAuth.cs` installs the
OAuth2 bearer scheme and sets `AuthorizationOptions.FallbackPolicy` to "require
an authenticated user". That is the default for the whole platform: every
endpoint without its own authorization metadata needs an OAuth2 bearer.

The Health Status API is the one exception. `ZscAuth.AddZscSubscriptionKey`
adds the `Ocp-Apim-Subscription-Key` scheme and a named policy,
`SubscriptionKeyPolicy`, which evaluates only that scheme. Health Status
requires it on `/internal/health/{zsc|zls}/status`, so a bearer token alone does
not open those routes (R1). Which public routes use the key is recorded once in
`ZscRoutes` (`ZscRoute.SubscriptionKey`).

- The interceptor and BFF map those key-flagged public paths as anonymous
  pass-throughs ahead of their OAuth2 catch-all. They do not check the key; the
  owning service does. A health request with no key is therefore forwarded and
  rejected by health-status with 401, not by the interceptor.
- Every other route stays OAuth2. The only endpoints outside the bearer policy
  are the `/healthz` liveness endpoints (`ZscLiveness`) and the key-flagged
  health routes.
- The key is validated only by health-status, against `Zsc:SubscriptionKeys`.

Four processes install the OAuth2 policy independently (interceptor, bff,
health-status, device-api), so a bearer is validated on each hop it reaches.

## Where headers are narrowed

Two places, and both drop anything they do not recognise:

1. **`EdgeHeaderPolicyMiddleware`** (`src/Zsc.ApiGateway`). An inbound header
   that is not on the gateway's edge allowlist is removed from the request before
   it enters the internal chain. A header the platform has no use for does not
   reach the Interceptor.

2. **`TokenForwardingHandler`** (`src/Zsc.CommonRoutes`). Attached to every
   outbound `HttpClient` in the chain. It copies the headers named in
   `ZscHeaders` — `Authorization` and `X-Correlation-Id` — from the inbound
   request onto the outbound one. Nothing else is copied, at any hop.

Both are deliberate: the platform forwards what it knows about. They are also the
reason a new header does not reach a downstream service on its own — a change at
the edge alone is not enough to get one to `health-status`, four hops down.

## Configuration

Each service reads its own `appsettings.json`:

- `Urls` — the address it binds.
- `Zsc:OAuth2` — `Issuer`, `Audience`, `SigningKey`. Symmetric-key HS256, so the
  chain runs with no identity provider. `DevTokenIssuer` mints tokens with the
  same values; the gateway exposes it at `POST /dev/token` when
  `Zsc:EnableDevTokenEndpoint` is true.
- `Zsc:Services` — the neighbours that service calls, by name. `ZscServiceRegistry`
  resolves a name to a base address; the names come from `ZscRoutes`.

## What is deliberately not here

No database, no message broker, no service discovery, no TLS, no container
build. The point of this repository is the request chain and where policy is
decided along it — everything else is stubbed so it runs anywhere with only the
.NET 8 SDK.
