namespace Xanthos.UnitTests

open System
open System.IO
open System.Text.Json

/// Keep only mutations necessary to reproduce the same assertion failure.
module internal GeneratedFailure =
    let minimize (baseline: byte[]) (input: byte[]) fails =
        let result = Array.copy input
        let mutable index = 0

        while index < result.Length do
            if result[index] <> baseline[index] then
                let original = result[index]
                result[index] <- baseline[index]

                if fails result then
                    index <- -1
                else
                    result[index] <- original

            index <- index + 1

        result

    let preserve id seed iteration (baseline: byte[]) (input: byte[]) verify (error: exn) =
        let fails bytes =
            try
                verify bytes
                false
            with candidate ->
                candidate.GetType() = error.GetType() && candidate.Message = error.Message

        let minimal = minimize baseline input fails

        let directory =
            match Environment.GetEnvironmentVariable "XANTHOS_FAILURE_DIRECTORY" with
            | null
            | "" -> Path.Combine(AppContext.BaseDirectory, "TestResults", "generated-failures")
            | path -> path

        Directory.CreateDirectory directory |> ignore

        let path =
            Path.Combine(directory, $"{id}-{seed}-{iteration}-{Guid.NewGuid():N}.json")

        let layout = RecordOracle.layout id

        let changedFields =
            layout.Fields
            |> Array.filter (fun field ->
                layout.Fields |> Array.exists (fun child -> child.Parent = Some field.Id) |> not)
            |> Array.collect (fun field ->
                RecordOracle.positions layout field
                |> List.indexed
                |> List.choose (fun (occurrence, offset) ->
                    let before = baseline[offset .. offset + field.Length - 1]
                    let after = minimal[offset .. offset + field.Length - 1]
                    let original = input[offset .. offset + field.Length - 1]

                    if before = after && before = original then
                        None
                    else
                        Some
                            {| field = field.Id
                               name = field.Name
                               occurrence = occurrence
                               position = offset + 1
                               length = field.Length
                               beforeBase64 = Convert.ToBase64String before
                               originalAfterBase64 = Convert.ToBase64String original
                               afterBase64 = Convert.ToBase64String after |})
                |> List.toArray)

        let mutations =
            Array.zip baseline minimal
            |> Array.indexed
            |> Array.choose (fun (index, (before, after)) ->
                if before = after then
                    None
                else
                    Some
                        {| position = index + 1
                           before = int before
                           after = int after |})

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                {| recordId = id
                   seed = seed
                   iteration = iteration
                   baselineBase64 = Convert.ToBase64String baseline
                   inputBase64 = Convert.ToBase64String input
                   minimalInputBase64 = Convert.ToBase64String minimal
                   mutations = mutations
                   exceptionType = error.GetType().FullName
                   message = error.Message
                   changedFields = changedFields
                   reproduced = fails minimal
                   minimization = "one-byte deletion minimal relative to baseline; same exception type and message" |},
                JsonSerializerOptions(WriteIndented = true)
            )
        )

        path
