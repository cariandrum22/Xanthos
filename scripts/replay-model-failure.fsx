// dotnet fsi scripts/replay-model-failure.fsx <test assembly> <saved JSON>
open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text.Json

let main () =
    let args = fsi.CommandLineArgs |> Array.skip 1

    if args.Length <> 2 then
        failwith "Expected test assembly and saved model failure JSON."

    let assemblyPath = Path.GetFullPath args[0]
    let directory = Path.GetDirectoryName assemblyPath

    AssemblyLoadContext.Default.add_Resolving (fun _ name ->
        let path = Path.Combine(directory, name.Name + ".dll")

        if File.Exists path then
            AssemblyLoadContext.Default.LoadFromAssemblyPath path
        else
            null)

    let assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath assemblyPath
    use payload = JsonDocument.Parse(File.ReadAllText args[1])

    let trace =
        payload.RootElement.GetProperty("minimal").EnumerateArray()
        |> Seq.map _.GetInt32()
        |> Seq.toArray

    let model = assembly.GetType("Xanthos.FunctionalScenarioTests.SessionModel", true)

    let verify =
        model.GetMethod("verify", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)

    try
        verify.Invoke(null, [| box trace |]) |> ignore
        printfn "PASS: saved %d-operation trace succeeds on the supplied assembly." trace.Length
    with :? TargetInvocationException as error ->
        eprintfn "%s: %s" (error.InnerException.GetType().FullName) error.InnerException.Message
        exit 1

main ()
