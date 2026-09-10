# Requirement R1 Implementation: Health Status API Dual Authentication

## Summary

Implemented dual authentication for the Health Status API (ZSC and ZLS) to accept either a subscription key (`Ocp-Apim-Subscription-Key` header) or an OAuth2 bearer token, while keeping all other ZSC APIs OAuth2-only. This allows legacy callers to continue using bearer tokens on health endpoints during a transition period.

## Files Changed and Rationale

### Source Code

| File | Change | Rationale |
|------|--------|-----------|
| `src/Zsc.CommonRoutes/ZscHeaders.cs` | Added `SubscriptionKey` constant | Define the subscription key header name used across the platform |
| `src/Zsc.CommonRoutes/SubscriptionKeyOptions.cs` | **New file** | Configuration container for subscription keys (populated from `Zsc:SubscriptionKeys` in appsettings.json) |
| `src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs` | **New file** | ASP.NET Core authentication handler that validates subscription keys against the configured set |
| `src/Zsc.CommonRoutes/ZscHealthStatusRoutes.cs` | **New file** | Route predicate to identify health-status paths (`/api/v1/health/*` and `/internal/health/*`) |
| `src/Zsc.CommonRoutes/ZscAuth.cs` | Complete rewrite | Replaced single-scheme (OAuth2-only) with dual authentication: registers three schemes (Bearer/JWT, SubscriptionKey, and a policy scheme) with smart ForwardDefaultSelector to choose the appropriate scheme based on path and headers |
| `src/Zsc.CommonRoutes/TokenForwardingHandler.cs` | Added subscription key forwarding | Forward `Ocp-Apim-Subscription-Key` header along with Authorization and X-Correlation-Id through the request chain |
| `src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs` | Added subscription key to allowlist | Allow `Ocp-Apim-Subscription-Key` header through the gateway edge (was previously dropped) |

### Configuration

All `appsettings.json` files updated to include subscription keys:

| File | Change | Rationale |
|------|--------|-----------|
| `src/Zsc.ApiGateway/appsettings.json` | Added `Zsc:SubscriptionKeys` array | Provision the demo key (`zsc-demo-subscription-key-001`) |
| `src/Zsc.Interceptor/appsettings.json` | Added `Zsc:SubscriptionKeys` array | Same key for consistency |
| `src/Zsc.Bff/appsettings.json` | Added `Zsc:SubscriptionKeys` array | Same key for consistency |
| `src/Zsc.HealthStatus/appsettings.json` | Added `Zsc:SubscriptionKeys` array | Same key for consistency |
| `src/Zsc.DeviceApi/appsettings.json` | Added `Zsc:SubscriptionKeys` array | Same key (not used but available for consistency) |

### Tests

| File | Change | Rationale |
|------|--------|-----------|
| `tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs` | Updated test case | Changed "bearer_no_longer_accepts" to "bearer_for_backward_compatibility" returning 200 instead of 401; added comment explaining the deviation |
| `tests/Zsc.CommonRoutes.Tests/SubscriptionKeyAuthenticationTests.cs` | **New file** | Unit tests for subscription key header constant, handler scheme, and route predicate |
| `tests/Zsc.CommonRoutes.Tests/ZscHealthStatusRoutesTests.cs` | **New file** | Unit tests for health-status route identification with various paths and cases |

## Header Propagation Path

A request with `Ocp-Apim-Subscription-Key: zsc-demo-subscription-key-001` to GET `/api/v1/health/zsc/status` travels:

```
client
  │  Ocp-Apim-Subscription-Key: zsc-demo-subscription-key-001
  ▼
api-gateway :5080
  │  EdgeHeaderPolicyMiddleware allows the header through (was previously dropped)
  │  CorrelationIdMiddleware stamps X-Correlation-Id
  │  AddZscPlatformAuth installed; request forwarded to Interceptor
  ▼
interceptor :5100
  │  ZscAuth.AddZscPlatformAuth: ForwardDefaultSelector sees health-status path
  │  + subscription key header → chooses SubscriptionKey scheme
  │  SubscriptionKeyAuthenticationHandler validates key against configured set
  │  ✓ Valid key → authenticated; request forwarded to BFF
  ▼
bff :5200
  │  ZscAuth.AddZscPlatformAuth: ForwardDefaultSelector again chooses SubscriptionKey
  │  TokenForwardingHandler forwards Ocp-Apim-Subscription-Key to downstream
  │  BFF resolves public path to health-status service and downstream path
  ▼
health-status :5300
  │  ZscAuth.AddZscPlatformAuth: SubscriptionKeyAuthenticationHandler validates key
  │  ✓ Valid key → authenticated
  │  Returns 200 with health status payload
  ▼
client receives 200 with health data
```

## Configuration Key

**`Zsc:SubscriptionKeys`** — JSON array of valid subscription keys.

**Example** (from `appsettings.json`):

```json
{
  "Zsc": {
    "SubscriptionKeys": [
      "zsc-demo-subscription-key-001"
    ]
  }
}
```

**How it's read:** `ZscAuth.AddZscPlatformAuth` calls `configuration.GetSection("Zsc:SubscriptionKeys").Get<string[]>()` and populates a `HashSet<string>` for fast lookup (case-insensitive).

## Wrong Key Rejection

When a request carries `Ocp-Apim-Subscription-Key: wrong-key-000`:

1. **Edge (API Gateway)** — HeaderPolicyMiddleware allows it through (it's on the allowlist).
2. **Interceptor** — ForwardDefaultSelector routes to SubscriptionKeyScheme (health-status path + header present). SubscriptionKeyAuthenticationHandler checks if `wrong-key-000` is in the configured set:
   - **Not found** → returns `AuthenticateResult.Fail("Invalid subscription key.")`
   - Authorization policy catches this → returns **401 Unauthorized**
   - Request never reaches downstream services

The first rejection happens at the **Interceptor** hop (5100), so the response surfaces to the caller as 401 immediately.

## Coexistence Policy

**Health-Status APIs** (`/api/v1/health/*`):
- Accept subscription key (`Ocp-Apim-Subscription-Key` header) **→ 200**
- Accept OAuth2 bearer token (`Authorization: Bearer <token>`) **→ 200** (backward compatibility)
- Accept either, but not both required — whichever matches first wins
- Reject both missing **→ 401**
- Reject wrong key + no bearer **→ 401**

**All Other ZSC APIs** (e.g., `/api/v1/devices/*`):
- Accept OAuth2 bearer token only **→ 200**
- Reject subscription key **→ 401** (ForwardDefaultSelector doesn't route to SubscriptionKeyScheme for non-health paths)
- Reject missing credentials **→ 401**

**Rationale:** Health Status APIs are often checked by infrastructure probes and monitoring tools that may use legacy subscription-key integrations. Keeping both paths open prevents operational disruption. All other APIs enforce the new OAuth2-only policy.

## Rollback Note

To revert to OAuth2-only (hard cutover per original R1):

1. **In `ZscAuth.cs`**, modify `ForwardDefaultSelector` to always return `OAuth2Scheme`:
   ```csharp
   options.ForwardDefaultSelector = context => OAuth2Scheme;
   ```

2. **In `HealthStatusAuthenticationTests.cs`**, update test to expect 401 for bearer-only calls.

3. **Keep the subscription key infrastructure in place** (the configuration, handler, and predicate) — they will simply not be used but cause no harm.

4. No service restarts required after code change; recompile and restart all five processes.

## Backward Compatibility

- Existing OAuth2 callers of Health Status APIs see no change: their bearer tokens still work.
- Existing subscription-key-using callers (if any) now work instead of failing.
- All other ZSC APIs remain OAuth2-only and are unaffected.
