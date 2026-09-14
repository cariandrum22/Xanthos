namespace Xanthos.Testing

open System

/// Fast runs retain stable inputs; Stress runs record an explicit seed in invocation.json.
type ReplayPropertyAttribute() as this =
    inherit FsCheck.Xunit.PropertyAttribute()

    do
        if Environment.GetEnvironmentVariable "XANTHOS_TEST_PROFILE" = "Stress" then
            let replay = Environment.GetEnvironmentVariable "XANTHOS_PROPERTY_REPLAY"

            if String.IsNullOrWhiteSpace replay then
                invalidOp "Stress properties require XANTHOS_PROPERTY_REPLAY; use run-test-profile.ps1."

            this.Replay <- replay
            this.MaxTest <- 1000
        else
            this.Replay <- "104729,130363"
