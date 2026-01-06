namespace Xanthos.Core

open System
open System.Globalization
open System.Text
open System.Collections.Concurrent
open System.Runtime.CompilerServices

/// <summary>
/// Text encoding and decoding utilities for JV-Link data processing.
/// </summary>
/// <remarks>
/// <para>
/// JV-Link returns data encoded in Shift-JIS (code page 932), which is the standard
/// encoding for Japanese text in legacy Windows applications. This module provides
/// functions to decode Shift-JIS to .NET strings and encode back for transmission.
/// </para>
/// <para>
/// <strong>Caching:</strong> Frequently decoded/encoded strings are cached in memory
/// to improve performance when processing large volumes of data. Cache size is limited
/// to prevent unbounded memory growth (max 1024 entries per cache).
/// </para>
/// <para>
/// <strong>Environment Variables:</strong>
/// <list type="bullet">
/// <item><c>XANTHOS_DISABLE_TEXT_CACHE</c> - Set to any non-empty value to disable caching.
/// Useful for memory-constrained environments or debugging.</item>
/// </list>
/// </para>
/// </remarks>
module Text =

    /// <summary>Lazy-initialized Shift-JIS encoding with exception fallback.</summary>
    let private shiftJisEncoding =
        lazy
            (Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
             Encoding.GetEncoding("shift_jis", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback))

    /// <summary>Lenient Shift-JIS (code page 932) encoding used for best-effort recovery.</summary>
    let private shiftJisEncodingLenient =
        lazy
            (Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
             Encoding.GetEncoding(932))

    /// <summary>Strict UTF-8 encoding for fallback decoding.</summary>
    let private utf8Strict = lazy (new UTF8Encoding(false, true))

    /// <summary>Whether text caching is disabled via XANTHOS_DISABLE_TEXT_CACHE environment variable.</summary>
    let private cacheDisabled =
        match Environment.GetEnvironmentVariable "XANTHOS_DISABLE_TEXT_CACHE" with
        | null
        | "" -> false
        | _ -> true

    /// <summary>Maximum byte array size to cache for decoding (larger arrays bypass cache).</summary>
    let private maxDecodeCacheSize = 512
    /// <summary>Maximum string length to cache for encoding (longer strings bypass cache).</summary>
    let private maxEncodeCacheLength = 512
    /// <summary>Maximum number of decode cache entries before eviction.</summary>
    let private maxDecodeEntries = 1024
    /// <summary>Maximum number of encode cache entries before eviction.</summary>
    let private maxEncodeEntries = 1024

    /// <summary>LRU-approximating cache for decoded strings.</summary>
    let private decodeCache = ConcurrentDictionary<string, string>()
    /// <summary>LRU-approximating cache for encoded byte arrays.</summary>
    let private encodeCache = ConcurrentDictionary<string, byte[]>()
    /// <summary>FIFO queue tracking decode cache insertion order for eviction.</summary>
    let private decodeKeyQueue = ConcurrentQueue<string>()
    /// <summary>FIFO queue tracking encode cache insertion order for eviction.</summary>
    let private encodeKeyQueue = ConcurrentQueue<string>()

    let private makeDecodeKey (bytes: byte[]) =
        if isNull bytes || bytes.Length = 0 then
            "0"
        else
            let prefix = bytes.Length.ToString("X")
            let payload = Convert.ToBase64String(bytes)
            prefix + ":" + payload

    /// <summary>Evicts oldest cache entries when cache exceeds limit.</summary>
    let private trimCache (queue: ConcurrentQueue<string>) (cache: ConcurrentDictionary<string, 'T>) limit =
        while cache.Count > limit do
            match queue.TryDequeue() with
            | true, key -> cache.TryRemove(key) |> ignore
            | _ -> ()

    /// <summary>
    /// Decodes a Shift-JIS encoded byte array to a .NET string.
    /// </summary>
    /// <param name="bytes">The Shift-JIS encoded byte array. May be null or empty.</param>
    /// <returns>
    /// The decoded string with trailing null characters removed.
    /// Returns <see cref="String.Empty"/> for null or empty input.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This function first attempts Shift-JIS decoding. If that fails (invalid byte sequences),
    /// it falls back to strict UTF-8, then to lenient UTF-8 as a last resort.
    /// </para>
    /// <para>
    /// Results are cached for performance unless:
    /// <list type="bullet">
    /// <item>XANTHOS_DISABLE_TEXT_CACHE is set</item>
    /// <item>The byte array exceeds 512 bytes</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="System.ArgumentException">Never thrown; fallback ensures a result.</exception>
    let decodeShiftJis (bytes: byte[]) =
        if isNull bytes || bytes.Length = 0 then
            String.Empty
        else if cacheDisabled || bytes.Length > maxDecodeCacheSize then
            try
                let text = shiftJisEncoding.Value.GetString bytes
                text.TrimEnd('\u0000')
            with :? DecoderFallbackException ->
                try
                    let text = utf8Strict.Value.GetString bytes
                    text.TrimEnd('\u0000')
                with :? DecoderFallbackException ->
                    Encoding.UTF8.GetString bytes
        else
            let key = makeDecodeKey bytes

            match decodeCache.TryGetValue key with
            | true, cached -> cached
            | _ ->
                let decoded =
                    try
                        let text = shiftJisEncoding.Value.GetString bytes
                        text.TrimEnd('\u0000')
                    with :? DecoderFallbackException ->
                        try
                            let text = utf8Strict.Value.GetString bytes
                            text.TrimEnd('\u0000')
                        with :? DecoderFallbackException ->
                            Encoding.UTF8.GetString bytes

                if decodeCache.TryAdd(key, decoded) then
                    decodeKeyQueue.Enqueue(key)
                    trimCache decodeKeyQueue decodeCache maxDecodeEntries

                decoded

    /// <summary>
    /// Decodes JV-Link Shift-JIS bytes that were mistakenly marshalled as a BSTR string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some JV-Link COM APIs populate out-parameters using Shift-JIS bytes, but the COM marshaller
    /// may expose them to .NET as a UTF-16 string without decoding. This function attempts to
    /// recover the original bytes from common BSTR layouts and decode them as Shift-JIS.
    /// </para>
    /// <para>
    /// If the input already looks like readable Japanese text, it is returned as-is.
    /// </para>
    /// </remarks>
    let decodeShiftJisBstrBytesIfNeeded (text: string) =
        if String.IsNullOrEmpty text then
            text
        else
            let text =
                let nulIndex = text.IndexOf('\u0000')

                if nulIndex >= 0 then text.Substring(0, nulIndex) else text

            if String.IsNullOrEmpty text then
                text
            else
                let trimNuls (s: string) = s.TrimEnd('\u0000')

                let removeEmbeddedNuls (s: string) =
                    if String.IsNullOrEmpty s then
                        s
                    else
                        s.Replace("\u0000", "")

                let tryDecodeShiftJisStrictWithTrimming (bytes: byte[]) =
                    try
                        let tryDecode (candidate: byte[]) =
                            try
                                let decoded = shiftJisEncoding.Value.GetString candidate
                                Some(trimNuls decoded)
                            with :? DecoderFallbackException ->
                                None

                        if isNull bytes || bytes.Length = 0 then
                            None
                        else
                            match tryDecode bytes with
                            | Some decoded -> Some decoded
                            | None ->
                                // Some JV-Link APIs appear to leave garbage bytes at the end of the buffer.
                                // Retry by trimming a small number of trailing bytes to recover a valid Shift-JIS sequence.
                                let maxTrim = min 8 (bytes.Length - 1)

                                [ 1..maxTrim ]
                                |> List.tryPick (fun trim ->
                                    let len = bytes.Length - trim
                                    if len <= 0 then None else tryDecode (Array.take len bytes))
                    with :? DecoderFallbackException ->
                        None

                let tryDecodeShiftJisLenient (bytes: byte[]) =
                    try
                        if isNull bytes || bytes.Length = 0 then
                            None
                        else
                            let decoded = shiftJisEncodingLenient.Value.GetString bytes
                            Some(decoded |> trimNuls |> removeEmbeddedNuls)
                    with _ ->
                        None

                let bytesFromLowBytePerChar =
                    text.ToCharArray() |> Array.map (fun ch -> byte (int ch &&& 0xFF))

                let bytesFromHighBytePerChar =
                    text.ToCharArray() |> Array.map (fun ch -> byte ((int ch >>> 8) &&& 0xFF))

                let bytesFromUtf16LittleEndian =
                    let bytes = Encoding.Unicode.GetBytes text
                    bytes |> Array.rev |> Array.skipWhile ((=) 0uy) |> Array.rev

                let bytesFromUtf16BigEndian =
                    let bytes = Encoding.BigEndianUnicode.GetBytes text
                    bytes |> Array.rev |> Array.skipWhile ((=) 0uy) |> Array.rev

                let isJapaneseChar (ch: char) =
                    let code = int ch

                    (code >= 0x3000 && code <= 0x303F) // CJK Symbols and Punctuation
                    || (code >= 0x3040 && code <= 0x309F) // Hiragana
                    || (code >= 0x30A0 && code <= 0x30FF) // Katakana
                    || (code >= 0x4E00 && code <= 0x9FFF) // CJK Unified Ideographs

                let score (s: string) =
                    let hiraganaCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x3040 && code <= 0x309F then 1 else 0)

                    let katakanaCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x30A0 && code <= 0x30FF then 1 else 0)

                    let kanjiCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x4E00 && code <= 0x9FFF then 1 else 0)

                    let cjkPunctCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x3000 && code <= 0x303F then 1 else 0)

                    let asciiPrintableCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x20 && code <= 0x7E then 1 else 0)

                    let replacementCount = s |> Seq.sumBy (fun ch -> if ch = '\uFFFD' then 1 else 0)
                    let nulCount = s |> Seq.sumBy (fun ch -> if ch = '\u0000' then 1 else 0)
                    let controlCount = s |> Seq.sumBy (fun ch -> if Char.IsControl ch then 1 else 0)

                    let c1ControlCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if code >= 0x80 && code <= 0x9F then 1 else 0)

                    let surrogateCount = s |> Seq.sumBy (fun ch -> if Char.IsSurrogate ch then 1 else 0)

                    let otherNonAsciiPrintableCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            if isJapaneseChar ch then
                                0
                            else
                                let code = int ch

                                if (code >= 0x20 && code <= 0x7E) || Char.IsWhiteSpace ch then
                                    0
                                else
                                    1)

                    // A common corruption pattern is "high-byte stuffed" strings where each UTF-16 code unit is 0xXX00.
                    // Penalize those to avoid treating random CJK-looking characters as already-decoded Japanese.
                    let highByteStuffedCount =
                        s
                        |> Seq.sumBy (fun ch ->
                            let code = int ch
                            if (code &&& 0xFF) = 0 && (code >>> 8) <> 0 then 1 else 0)

                    (hiraganaCount * 20)
                    + (katakanaCount * 20)
                    + (kanjiCount * 10)
                    + (cjkPunctCount * 5)
                    + asciiPrintableCount
                    - (replacementCount * 20)
                    - (nulCount * 5)
                    - (controlCount * 5)
                    - (c1ControlCount * 10)
                    - (surrogateCount * 10)
                    - (otherNonAsciiPrintableCount * 8)
                    - (highByteStuffedCount * 8)

                let kanaCount =
                    text
                    |> Seq.sumBy (fun ch ->
                        let code = int ch

                        if (code >= 0x3040 && code <= 0x309F) || (code >= 0x30A0 && code <= 0x30FF) then
                            1
                        else
                            0)

                let kanjiCount =
                    text
                    |> Seq.sumBy (fun ch ->
                        let code = int ch
                        if code >= 0x4E00 && code <= 0x9FFF then 1 else 0)

                let c1ControlCount =
                    text
                    |> Seq.sumBy (fun ch ->
                        let code = int ch
                        if code >= 0x80 && code <= 0x9F then 1 else 0)

                let highByteStuffedCount =
                    text
                    |> Seq.sumBy (fun ch ->
                        let code = int ch
                        if (code &&& 0xFF) = 0 && (code >>> 8) <> 0 then 1 else 0)

                let otherNonAsciiPrintableCount =
                    text
                    |> Seq.sumBy (fun ch ->
                        if isJapaneseChar ch then
                            0
                        else
                            let code = int ch

                            if (code >= 0x20 && code <= 0x7E) || Char.IsWhiteSpace ch then
                                0
                            else
                                1)

                let longKanjiOnly = text.Length >= 30 && kanaCount = 0 && kanjiCount > 0

                let decodeFromBytes (bytes: byte[]) =
                    match tryDecodeShiftJisStrictWithTrimming bytes with
                    | Some decoded -> Some(removeEmbeddedNuls decoded)
                    | None -> tryDecodeShiftJisLenient bytes

                if c1ControlCount > 0 then
                    decodeFromBytes bytesFromLowBytePerChar |> Option.defaultValue text
                elif highByteStuffedCount > 0 then
                    decodeFromBytes bytesFromHighBytePerChar |> Option.defaultValue text
                elif otherNonAsciiPrintableCount > 0 || longKanjiOnly then
                    let decodedCandidates =
                        [ decodeFromBytes bytesFromUtf16LittleEndian
                          decodeFromBytes bytesFromUtf16BigEndian
                          decodeFromBytes bytesFromLowBytePerChar
                          decodeFromBytes bytesFromHighBytePerChar ]
                        |> List.choose id

                    let candidates = text :: decodedCandidates |> List.distinct
                    let scored = candidates |> List.map (fun s -> s, score s)
                    let originalScore = score text
                    let bestText, bestScore = scored |> List.maxBy snd

                    if bestText <> text && bestScore > originalScore then
                        bestText
                    else
                        text
                else
                    text

    /// <summary>
    /// Encodes a .NET string to Shift-JIS byte array.
    /// </summary>
    /// <param name="text">The string to encode. May be null or empty.</param>
    /// <returns>
    /// The Shift-JIS encoded byte array.
    /// Returns an empty array for null or empty input.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Results are cached for performance unless:
    /// <list type="bullet">
    /// <item>XANTHOS_DISABLE_TEXT_CACHE is set</item>
    /// <item>The string exceeds 512 characters</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="System.Text.EncoderFallbackException">
    /// Thrown if the string contains characters that cannot be encoded in Shift-JIS.
    /// </exception>
    let encodeShiftJis (text: string) =
        if String.IsNullOrEmpty text then
            Array.empty
        else if cacheDisabled || text.Length > maxEncodeCacheLength then
            shiftJisEncoding.Value.GetBytes text
        else
            match encodeCache.TryGetValue text with
            | true, cached ->
                // Return a defensive copy to prevent callers from corrupting the cache
                Array.copy cached
            | _ ->
                let bytes = shiftJisEncoding.Value.GetBytes text

                if encodeCache.TryAdd(text, bytes) then
                    encodeKeyQueue.Enqueue(text)
                    trimCache encodeKeyQueue encodeCache maxEncodeEntries

                // Return a copy since the original is now in the cache
                Array.copy bytes

    /// <summary>
    /// Normalizes JV-Link text by converting fullwidth characters to ASCII equivalents.
    /// </summary>
    /// <param name="text">The text to normalize. May be null or empty.</param>
    /// <returns>
    /// The normalized string with fullwidth digits and letters converted to ASCII.
    /// Returns the input unchanged if null or empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// JV-Link data often contains fullwidth (全角) characters that should be normalized
    /// for consistent processing:
    /// <list type="bullet">
    /// <item>Fullwidth digits (０-９) → ASCII digits (0-9)</item>
    /// <item>Fullwidth uppercase (Ａ-Ｚ) → ASCII uppercase (A-Z)</item>
    /// <item>Fullwidth lowercase (ａ-ｚ) → ASCII lowercase (a-z)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Also applies Unicode NormalizationForm.FormKC for compatibility decomposition.
    /// </para>
    /// </remarks>
    let normalizeJvText (text: string) =
        if String.IsNullOrEmpty text then
            text
        else
            let normalized = text.Normalize(NormalizationForm.FormKC)
            let builder = StringBuilder(normalized.Length)

            for ch in normalized do
                if ch >= '\uFF10' && ch <= '\uFF19' then
                    let asciiDigit = char (int '0' + int ch - int '\uFF10')
                    builder.Append asciiDigit |> ignore
                elif ch >= '\uFF21' && ch <= '\uFF3A' then
                    let asciiUpper = char (int 'A' + int ch - int '\uFF21')
                    builder.Append asciiUpper |> ignore
                elif ch >= '\uFF41' && ch <= '\uFF5A' then
                    let asciiLower = char (int 'a' + int ch - int '\uFF41')
                    builder.Append asciiLower |> ignore
                else
                    builder.Append ch |> ignore

            builder.ToString()

    /// <summary>
    /// Clears both encode and decode caches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this function to free memory when processing is complete or when
    /// switching between different data sources. The caches will be rebuilt
    /// automatically as new data is processed.
    /// </para>
    /// <para>
    /// This function is thread-safe but may have race conditions with concurrent
    /// encode/decode operations (which will simply repopulate the cache).
    /// </para>
    /// </remarks>
    let clearCaches () =
        decodeCache.Clear()
        encodeCache.Clear()
