namespace Zsc.CommonRoutes;

public enum SubscriptionKeyValidationResult
{
    Missing,
    Invalid,
    Valid,
}

public sealed class SubscriptionKeyValidator(IEnumerable<string> validKeys)
{
    private readonly HashSet<string> _validKeys = new(validKeys, StringComparer.Ordinal);

    public SubscriptionKeyValidationResult Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return SubscriptionKeyValidationResult.Missing;
        }

        return _validKeys.Contains(headerValue)
            ? SubscriptionKeyValidationResult.Valid
            : SubscriptionKeyValidationResult.Invalid;
    }
}
