namespace Zsc.CommonRoutes;

/// <summary>
/// Validates subscription key header values against configured valid keys.
/// Separated from the handler for testability.
/// </summary>
public sealed class SubscriptionKeyValidator
{
    private readonly SubscriptionKeyAuthenticationOptions _options;

    public enum ValidationResult
    {
        /// <summary>No header was provided</summary>
        Missing,
        /// <summary>Header was provided but key is not valid</summary>
        Invalid,
        /// <summary>Header was provided and key is valid</summary>
        Valid
    }

    public SubscriptionKeyValidator(SubscriptionKeyAuthenticationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Validates a subscription key header value.
    /// </summary>
    public ValidationResult Validate(string? headerValue)
    {
        if (string.IsNullOrEmpty(headerValue))
        {
            return ValidationResult.Missing;
        }

        // Check if the key is in the configured valid set (case-sensitive)
        if (_options.ValidKeys.Contains(headerValue))
        {
            return ValidationResult.Valid;
        }

        return ValidationResult.Invalid;
    }
}
