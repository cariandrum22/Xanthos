#load "SettingsRoundtrip.fsx"

open System
open SettingsRoundtrip

let check condition message =
    if not condition then
        failwith message

let mutable completed = 0

for originalFlag in [ 0; 1 ] do
    for mode in
        [ "normal"
          "injected"
          "path-injected"
          "denied"
          "path-rejected"
          "path-ignored"
          "flag-ignored"
          "throw-after-change"
          "restore-flag-failed"
          "restore-flag-ignored"
          "restore-path-failed"
          "original-data-changed"
          "nonempty-isolation"
          "primary-and-cleanup-failed" ] do
        let original =
            { Flag = originalFlag
              Path = "original" }

        let mutable state = original
        let writes = ResizeArray<string>()
        let reports = ResizeArray<string>()
        let mutable flagWrites = 0
        let mutable emptyChecks = 0
        let mutable dataChecks = 0

        let operations =
            { Snapshot = fun () -> state
              SamePath = (=)
              SetPath =
                fun path ->
                    writes.Add("path:" + path)

                    if path = "original" then
                        check (state.Flag = originalFlag) "Original path exposed before flag restoration."

                        if mode = "restore-path-failed" || mode = "primary-and-cleanup-failed" then
                            failwith "restore-path-error"

                    if path = "isolated" && mode = "path-rejected" then
                        failwith "path-error"

                    if mode <> "path-ignored" then
                        state <- { state with Path = path }
              SetFlag =
                fun flag ->
                    check (state.Path = "isolated") "Flag setter reached original data."
                    check (emptyChecks >= 2) "Flag setter ran before isolation verification."
                    flagWrites <- flagWrites + 1
                    writes.Add(sprintf "flag:%b" flag)

                    if flagWrites = 1 && (mode = "denied" || mode = "primary-and-cleanup-failed") then
                        failwith "setter-code=-100"

                    if flagWrites = 2 && mode = "restore-flag-failed" then
                        failwith "restore-flag-error"

                    if mode <> "flag-ignored" && not (flagWrites = 2 && mode = "restore-flag-ignored") then
                        state <-
                            { state with
                                Flag = if flag then 1 else 0 }

                    if flagWrites = 1 && mode = "throw-after-change" then
                        failwith "setter-threw-after-change"
              VerifyIsolated =
                fun () ->
                    emptyChecks <- emptyChecks + 1

                    if emptyChecks = 2 && mode = "nonempty-isolation" then
                        failwith "isolation-not-empty"
              VerifyOriginalData =
                fun () ->
                    dataChecks <- dataChecks + 1

                    if mode = "original-data-changed" then
                        failwith "original-data-changed"
              Report = reports.Add }

        let error =
            try
                let injection =
                    match mode with
                    | "injected" -> AfterFlag
                    | "path-injected" -> AfterPath
                    | _ -> NoInjection

                SettingsRoundtrip.run operations "isolated" injection
                None
            with error ->
                Some error

        let success = mode = "normal" || mode = "injected" || mode = "path-injected"
        check (error.IsNone = success) (sprintf "Unexpected outcome: %s flag=%d" mode originalFlag)

        let incompleteRestore =
            List.contains
                mode
                [ "restore-flag-failed"
                  "restore-flag-ignored"
                  "restore-path-failed"
                  "primary-and-cleanup-failed" ]

        if incompleteRestore then
            check (state.Path = "isolated") "Recovery failure did not retain isolated path."
            check (error.Value :? AggregateException) "Recovery failure was not explicit."
        else
            check (state = original) "Settings did not return to their original values."
            check (dataChecks = 1) "Original data was not checked."

        if mode = "path-injected" then
            check (List.ofSeq writes = [ "path:isolated"; "path:original" ]) "Path injection called the flag setter."
            check (flagWrites = 0) "Path-only recovery changed the flag."
            check (reports.Contains "SETTINGS injecting=AfterPath") "Path exception was not injected."

            check
                (reports.Contains "SETTINGS completed=true injected=true injection=AfterPath")
                "Path recovery did not finish."
        elif success then
            check
                (List.ofSeq writes = [ "path:isolated"
                                       sprintf "flag:%b" (originalFlag = 0)
                                       sprintf "flag:%b" (originalFlag <> 0)
                                       "path:original" ])
                "Successful mutation/restoration ordering changed."

        if mode = "injected" then
            check (reports.Contains "SETTINGS injecting=AfterFlag") "Flag exception was not injected."

            check
                (reports.Contains "SETTINGS completed=true injected=true injection=AfterFlag")
                "Flag recovery did not finish."

        if mode = "denied" then
            check (error.Value.Message = "setter-code=-100") "Refusal error was hidden."
            check (flagWrites = 1) "Refusal was retried."

        if mode = "primary-and-cleanup-failed" then
            let errors = (error.Value :?> AggregateException).InnerExceptions
            check (errors.Count = 2) "Primary or recovery error was lost."
            check (errors[0].Message = "setter-code=-100") "Primary refusal was replaced."

        if List.contains mode [ "path-rejected"; "path-ignored"; "nonempty-isolation" ] then
            check (flagWrites = 0) "Flag changed before isolation was established."

        check
            ((reports |> Seq.exists _.StartsWith("SETTINGS completed=true")) = success)
            "Incomplete verification reported completion."

        completed <- completed + 1

printfn "PASS: %d settings ordering and recovery controls; no COM activation." completed
