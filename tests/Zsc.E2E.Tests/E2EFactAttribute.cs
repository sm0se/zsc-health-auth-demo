namespace Zsc.E2E.Tests;

// End-to-end tests need the five services running (scripts/run-all.sh, or the
// agent's `run` tool under kenCode). They are skipped unless ZSC_E2E=1, so a
// plain `dotnet test` on a clean checkout stays meaningful.
//
//     ZSC_E2E=1 dotnet test
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute() => Skip = E2E.SkipReason;
}

public sealed class E2ETheoryAttribute : TheoryAttribute
{
    public E2ETheoryAttribute() => Skip = E2E.SkipReason;
}

internal static class E2E
{
    public static string? SkipReason =>
        Environment.GetEnvironmentVariable("ZSC_E2E") == "1"
            ? null
            : "Set ZSC_E2E=1 and start the services (scripts/run-all.sh) to run end-to-end tests.";
}
