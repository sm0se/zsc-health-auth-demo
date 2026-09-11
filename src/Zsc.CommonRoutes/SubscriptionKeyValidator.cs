namespace Zsc.CommonRoutes;

// The outcome of checking a request's subscription key value. Kept distinct
// from a bool because the authentication handler needs to tell "there was no
// key to check" (NoResult, let another scheme have a go) apart from "there was
// a key and it was wrong" (Fail, this request is rejected).
public enum SubscriptionKeyValidationResult
{
    Missing,
    Invalid,
    Valid,
}

// Checks a request's Ocp-Apim-Subscription-Key value against the keys
// provisioned in configuration (Zsc:SubscriptionKeys - see
// ZscSubscriptionKeyOptions). Comparison is ordinal: keys are opaque tokens, not
// case-insensitive identifiers, so "ZSC-DEMO-..." is not the same key as
// "zsc-demo-...".
public sealed class SubscriptionKeyValidator(IReadOnlySet<string> validKeys)
{
    public SubscriptionKeyValidationResult Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyValidationResult.Missing;
        }

        return validKeys.Contains(headerValue)
            ? SubscriptionKeyValidationResult.Valid
            : SubscriptionKeyValidationResult.Invalid;
    }
}
