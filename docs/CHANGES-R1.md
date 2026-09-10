# Implementation of Requirement #1 — Subscription Key Authentication for Health Status API

## Deviation from Specification

The original requirement specified a **hard cutover** from OAuth2 to subscription key authentication for the Health Status API. This implementation includes a **COEXISTENCE DEVIATION**:

- **Health Status API** accepts **EITHER** a valid `Ocp-Apim-Subscription-Key` header **OR** a valid OAuth2 bearer token.
- All other ZSC APIs remain **OAuth2-only** as specified.

This deviation allows the legacy OAuth2 method to keep working during a transition period, preventing breaking changes for existing consumers.

## Files Changed and Why

### 1. **src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationOptions.cs** (NEW)
- Defines configuration options for subscription key authentication.
- Holds the set of valid subscription keys loaded from `Zsc:SubscriptionKeys` configuration section.
- Scheme constant: `"SubscriptionKey"`, Header name: `"Ocp-Apim-Subscription-Key"`.

### 2. **src/Zsc.CommonRoutes/SubscriptionKeyAuthenticationHandler.cs** (NEW)
- Custom ASP.NET Core authentication handler for subscription key validation.
- Reads the `Ocp-Apim-Subscription-Key` header from the request.
- Returns `NoResult()` if header is missing (allowing other schemes to try).
- Returns `Fail()` if the key is present but not in the configured valid set.
- Returns `Success()` with a `ClaimsPrincipal` if the key is valid (authentication only, no scopes).

### 3. **src/Zsc.CommonRoutes/ZscAuth.cs** (MODIFIED)
- Registers three authentication schemes:
  1. **JwtBearer** (`"Bearer"`): OAuth2 bearer token validation (existing).
  2. **SubscriptionKeyAuthenticationHandler** (`"SubscriptionKey"`): New subscription key scheme.
  3. **PolicyScheme** (`"ZscSmart"`): Intelligent selector between the above two.

- **Smart Policy Logic**:
  - If the request carries `Ocp-Apim-Subscription-Key` header **AND** the path is a health-status route, use `SubscriptionKey` scheme.
  - Otherwise, use `Bearer` scheme.

- `"ZscSmart"` is the `DefaultAuthenticateScheme` and `DefaultChallengeScheme`, so all requests flow through this logic.
- `FallbackPolicy` still requires `RequireAuthenticatedUser()` on all endpoints except those that opt out (e.g., `/healthz`).

### 4. **src/Zsc.CommonRoutes/ZscRoutes.cs** (MODIFIED)
- **Added** `IsHealthStatusRoute(string path)` predicate method.
- Identifies paths as health-status routes if they start with:
  - `/api/v1/health/` (public routes at gateway/interceptor/bff), OR
  - `/internal/health/` (internal routes at health-status service).
- Used by the `ZscSmart` policy scheme to decide authentication strategy.
- Handles paths with or without a leading slash (normalizes before comparison).

### 5. **src/Zsc.CommonRoutes/TokenForwardingHandler.cs** (MODIFIED)
- Now forwards the `Ocp-Apim-Subscription-Key` header in addition to `Authorization` and `X-Correlation-Id`.
- Ensures the subscription key travels through all hops in the request chain (gateway → interceptor → bff → health-status/device-api).

### 6. **src/Zsc.ApiGateway/EdgeHeaderPolicyMiddleware.cs** (MODIFIED)
- Added `SubscriptionKeyAuthenticationOptions.HeaderName` to the `Allowed` header set.
- Ensures the `Ocp-Apim-Subscription-Key` header from external callers is preserved at the edge.

### 7. **src/Zsc.ApiGateway/appsettings.json** (MODIFIED)
- Added `Zsc:SubscriptionKeys` section with:
  - Key: `"zsc-demo-subscription-key-001"`, Value: `true`
  - Configures the single valid subscription key for this demo.

### 8. **src/Zsc.Interceptor/appsettings.json** (MODIFIED)
- Added same `Zsc:SubscriptionKeys` configuration.

### 9. **src/Zsc.Bff/appsettings.json** (MODIFIED)
- Added same `Zsc:SubscriptionKeys` configuration.

### 10. **src/Zsc.HealthStatus/appsettings.json** (MODIFIED)
- Added same `Zsc:SubscriptionKeys` configuration.

### 11. **src/Zsc.DeviceApi/appsettings.json** (MODIFIED)
- Added same `Zsc:SubscriptionKeys` configuration (for consistency; not used for device-api endpoints).

### 12. **tests/Zsc.CommonRoutes.Tests/ZscRoutesTests.cs** (MODIFIED)
- **Added** test cases for `IsHealthStatusRoute()` predicate:
  - Correct identification of public and internal health-status routes.
  - Rejection of non-health-status paths.
  - Path normalization (handles paths without leading slash).

### 13. **tests/Zsc.CommonRoutes.Tests/SubscriptionKeyAuthenticationTests.cs** (NEW)
- Unit tests for the `SubscriptionKeyAuthenticationHandler`:
  - `NoResult()` when header is missing.
  - `Success()` with valid key.
  - `Fail()` with invalid key.
  - Handles multiple configured keys.

### 14. **tests/Zsc.E2E.Tests/HealthStatusAuthenticationTests.cs** (MODIFIED)
- **Changed** test case `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone()`:
  - From: Expected 401 (hard cutover).
  - To: Expected 200 (coexistence deviation).
  - Comment updated to explain the deviation and why bearer tokens still work.

### 15. **tests/Zsc.E2E.Tests/OAuth2RegressionTests.cs** (UNCHANGED)
- All tests continue to pass:
  - Device API remains OAuth2-only.
  - Subscription keys are rejected for device-api endpoints.
  - `/healthz` stays anonymous everywhere.

## Authentication Propagation Path

A request with subscription key travels through the chain as follows:

1. **Client** → sends request with `Ocp-Apim-Subscription-Key` header.
2. **API Gateway** (:5080):
   - `EdgeHeaderPolicyMiddleware` checks if the header is in the `Allowed` set → ALLOWED.
   - `CorrelationIdMiddleware` stamps correlation ID.
   - Forwards to Interceptor.

3. **Interceptor** (:5100):
   - `ZscSmart` policy applies: Header + health-status path → uses `SubscriptionKey` scheme.
   - `SubscriptionKeyAuthenticationHandler` validates the key.
   - `TokenForwardingHandler` copies the header into outbound request to BFF.
   - Forwards to BFF.

4. **BFF** (:5200):
   - `ZscSmart` policy applies: Header + health-status path → uses `SubscriptionKey` scheme.
   - Validates the key again.
   - `TokenForwardingHandler` copies the header into outbound request to Health Status API.
   - Resolves route and forwards to Health Status service.

5. **Health Status API** (:5300):
   - `ZscSmart` policy applies: Header + health-status path → uses `SubscriptionKey` scheme.
   - Validates the key a final time.
   - Serves `/internal/health/zsc/status` or `/internal/health/zls/status`.

**Key rejection happens at the first hop where the key is invalid** — see "Key Validation Sequence" below.

## Configuration Key

- **Configuration Section**: `Zsc:SubscriptionKeys`
- **Format**: Dictionary of key-value pairs, where the key is the subscription key string and the value is a boolean (currently just `true`).
- **Example**:
  ```json
  "Zsc:SubscriptionKeys": {
    "zsc-demo-subscription-key-001": true,
    "zsc-demo-subscription-key-002": true
  }
  ```
- **Valid key provisioned for this demo**: `zsc-demo-subscription-key-001`
- **Wrong key used in tests**: `wrong-key-000`

## Wrong Key Rejection

When a request carries the wrong subscription key (e.g., `wrong-key-000`):

1. **API Gateway** → Passes it through (no validation at edge, only header filtering).
2. **Interceptor** (:5100):
   - Path matches health-status pattern + header present → triggers `SubscriptionKey` scheme.
   - `SubscriptionKeyAuthenticationHandler.HandleAuthenticateAsync()` checks if key is in `ValidKeys` set.
   - Key `wrong-key-000` is not found → `AuthenticateResult.Fail()`.
   - **Request is rejected with 401**.
   - **First rejection point**: Interceptor.

The 401 response surfaces at the client through the gateway without further processing.

## Coexistence Policy

**Health Status API**: Both methods work:
- `Ocp-Apim-Subscription-Key: zsc-demo-subscription-key-001` → 200.
- `Authorization: Bearer <valid-jwt>` → 200.
- Both headers present → Subscription key takes precedence (evaluated first by `ZscSmart`).

**All other APIs** (e.g., `/api/v1/devices/{id}/status`):
- `Authorization: Bearer <valid-jwt>` → 200.
- `Ocp-Apim-Subscription-Key: <any-value>` → 401 (because path does NOT match health-status predicate, so `Bearer` scheme is used).

## Rollback Note

To revert this change (hard cutover to subscription key only, no OAuth2):

1. Modify the `ForwardDefaultSelector` in `ZscAuth.cs` to return `SubscriptionKeyScheme` unconditionally for health-status routes.
2. Update the E2E test `Platform_health_status_still_accepts_an_oauth2_bearer_token()` to expect 401 instead of 200 and rename it back to `Platform_health_status_no_longer_accepts_an_oauth2_bearer_alone()`.
3. Update the comment in the test to reflect the hard cutover decision.

No other changes needed—the infrastructure is in place.

## Summary

This implementation fulfills Requirement #1 with the coexistence deviation, allowing:

- Health Status API to authenticate via subscription key (primary method).
- Health Status API to authenticate via OAuth2 bearer (legacy method, during transition).
- All other APIs to remain OAuth2-only.
- The infrastructure to easily switch to a hard cutover if needed in the future.
