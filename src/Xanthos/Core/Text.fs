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
