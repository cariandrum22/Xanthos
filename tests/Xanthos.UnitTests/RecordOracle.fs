namespace Xanthos.UnitTests

open System
open System.IO
open System.Text
open System.Text.Json

/// Expectations and fixture positions come only from the spreadsheet inventory.
module RecordOracle =
    type Field =
        { Id: string
          Position: int
          Length: int
          Repeat: int
          Parent: string option
          Initial: string
          Name: string }

    type Layout =
        { Id: string
          Length: int
          Fields: Field array }

    let encoding =
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)

    let layout id =
        use doc =
            JsonDocument.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "record-layouts.json"))
            )

        let r =
            doc.RootElement.GetProperty("records").EnumerateArray()
            |> Seq.find (fun r -> r.GetProperty("id").GetString() = id)

        { Id = id
          Length = r.GetProperty("length").GetInt32()
          Fields =
            r.GetProperty("fields").EnumerateArray()
            |> Seq.map (fun f ->
                { Id = f.GetProperty("id").GetString()
                  Position = f.GetProperty("position").GetInt32()
                  Length = f.GetProperty("length").GetInt32()
                  Repeat = f.GetProperty("repeat").GetInt32()
                  Parent =
                    if f.GetProperty("parent").ValueKind = JsonValueKind.Null then
                        None
                    else
                        Some(f.GetProperty("parent").GetString())
                  Initial = f.GetProperty("initialValue").ToString()
                  Name = f.GetProperty("name").GetString() })
            |> Seq.toArray }

    let field layout id =
        layout.Fields |> Array.find (fun f -> f.Id = id)

    let private offsetCache =
        Runtime.CompilerServices.ConditionalWeakTable<
            Layout,
            Collections.Concurrent.ConcurrentDictionary<string, int array>
         >()

    let private offsets layout (f: Field) =
        let cache =
            offsetCache.GetValue(layout, fun _ -> Collections.Concurrent.ConcurrentDictionary<string, int array>())

        // Build uncached ancestors first so nested repeated fields never recurse on the stack.
        let pending = Collections.Generic.Stack<Field>()
        let mutable current = Some f

        while current.IsSome do
            let node = current.Value

            if cache.ContainsKey node.Id then
                current <- None
            else
                pending.Push node
                current <- node.Parent |> Option.map (field layout)

        while pending.Count > 0 do
            let node = pending.Pop()

            cache.GetOrAdd(
                node.Id,
                fun _ ->
                    let bases =
                        match node.Parent with
                        | None -> [| 0 |]
                        | Some id -> cache[id]

                    [| for offset in bases do
                           for i in 0 .. node.Repeat - 1 do
                               yield offset + node.Position - 1 + i * node.Length |]
            )
            |> ignore

        cache[f.Id]

    let positions layout f = offsets layout f |> Array.toList

    let write layout id occurrence (raw: string) (data: byte[]) =
        let f = field layout id
        let bytes = encoding.GetBytes raw

        if bytes.Length <> f.Length then
            failwithf "Oracle field %s.%s needs %d bytes, got %d" layout.Id id f.Length bytes.Length

        bytes.CopyTo(data, (offsets layout f)[occurrence])

    let text layout id occurrence (data: byte[]) =
        let f = field layout id
        encoding.GetString(data, (offsets layout f)[occurrence], f.Length)

    let blank layout =
        let data = Array.create layout.Length 32uy

        for f in layout.Fields do
            if not (layout.Fields |> Array.exists (fun other -> other.Parent = Some f.Id)) then
                let pattern =
                    if f.Initial = "0" then [| 48uy |]
                    elif f.Initial = "Ｓ" then encoding.GetBytes "　"
                    else [| 32uy |]

                for position in positions layout f do
                    for i in 0 .. f.Length - 1 do
                        data[position + i] <- pattern[i % pattern.Length]

        write layout "1" 0 layout.Id data
        write layout "2" 0 "1" data
        write layout "3" 0 "20260912" data
        data[layout.Length - 2] <- 13uy
        data[layout.Length - 1] <- 10uy
        data

    let padded length (text: string) =
        let count = encoding.GetByteCount text

        if count > length then
            failwith "Fixture text is too long."

        text + String(' ', length - count)

    /// Read the public model; expected bytes still come only from the spreadsheet oracle.
    let modelText (model: obj) (path: string) =
        let mutable current = model

        for part in path.Split('.') do
            let bracket = part.IndexOf('[')
            let name = if bracket < 0 then part else part.Substring(0, bracket)
            let property = current.GetType().GetProperty(name)

            if isNull property then
                failwithf "Missing public model property %s in %s" name path

            current <- property.GetValue current

            if bracket >= 0 then
                let index = Int32.Parse(part.Substring(bracket + 1, part.Length - bracket - 2))
                current <- (current :?> Array).GetValue index

        match current with
        | :? string as text -> text
        | :? Xanthos.OfficialCode as code -> Xanthos.Codes.raw code
        | _ -> current.GetType().GetProperty("Raw").GetValue(current) :?> string
