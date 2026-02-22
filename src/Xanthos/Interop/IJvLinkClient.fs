namespace Xanthos.Interop

open System
open Xanthos.Core

/// <summary>
/// Represents a canonical JV-Link data payload emitted by the COM layer.
/// </summary>
type JvPayload =
    { Timestamp: DateTime option
      Data: byte[] }

/// <summary>
/// Represents the state of the read loop when interacting with JV-Link.
/// </summary>
type JvReadOutcome =
    | Payload of JvPayload
    | FileBoundary
    | DownloadPending
    | EndOfStream

type JvOpenRequest =
    { Spec: string
      FromTime: DateTime
      Option: int }

/// <summary>
/// Result of a JVOpen or JVRTOpen call, containing metadata about the data session.
/// </summary>
type JvOpenResult =
    {
        /// <summary>True if data is available to read, false if no matching data exists (-1).</summary>
        HasData: bool
        /// <summary>Total number of files matching the request criteria.</summary>
        ReadCount: int
        /// <summary>Number of files that need to be downloaded.</summary>
        DownloadCount: int
        /// <summary>Timestamp of the most recent file (YYYYMMDDhhmmss format), if available.</summary>
        LastFileTimestamp: string option
    }

/// <summary>
/// Minimal surface that any JV-Link client implementation must expose.
/// </summary>
/// <remarks>
/// Interface stability rules:
/// - JVRead: returns Payload/FileBoundary/DownloadPending/EndOfStream; never throws for EOF/boundary.
/// - JVGets: returns raw byte count (>=0), -1(file boundary), -3(download pending), or &lt; -3 mapped to ComError via ErrorCodes.
///   Buffer is populated from Shift-JIS decoded bytes extracted directly from SAFEARRAY&lt;byte&gt; (no string round-trip).
/// - Status/Open/Close/Skip/Cancel: follow ErrorCodes for negative codes; success is Ok().
/// - Movie APIs: return domain types; errors surface as ComError.
/// These rules are now fixed and documented for contributors.
/// </remarks>
type IJvLinkClient =
    inherit IDisposable

    /// <summary>Initialises the JV-Link session.</summary>
    abstract member Init: sid: string -> Result<unit, ComError>
    /// <summary>
    /// Requests the download/opening of JV data.
    /// Returns JvOpenResult containing session metadata (file counts, timestamp).
    /// </summary>
    abstract member Open: request: JvOpenRequest -> Result<JvOpenResult, ComError>
    /// <summary>
    /// Requests the opening of real-time JV data (JVRTOpen).
    /// Returns JvOpenResult containing session metadata (file counts, timestamp).
    /// </summary>
    /// <param name="spec">Data spec ID (4 characters, e.g., "0B12", "0B11", "0B16")</param>
    /// <param name="key">Request key in one of the following formats:
    /// - Race-by-race: "YYYYMMDDJJKKHHRR" or "YYYYMMDDJJRR"
    /// - Daily: "YYYYMMDD"
    /// - Event-based: Parameter returned by WatchEvent callback
    /// </param>
    abstract member OpenRealtime: spec: string * key: string -> Result<JvOpenResult, ComError>
    /// <summary>Reads the next payload from JV-Link (DU cases); never throws for EOF/boundary.</summary>
    abstract member Read: unit -> Result<JvReadOutcome, ComError>
    /// <summary>
    /// Reads a single line from the current JV-Link payload using JVGets.
    /// Returns: &gt;=0 byte count, -1(file boundary), -3(download pending), or ComError for other negatives.
    /// </summary>
    abstract member Gets: buffer: byref<string> * bufferSize: int * filename: byref<string> -> Result<int, ComError>
    /// <summary>Closes the active JV-Link session.</summary>
    abstract member Close: unit -> unit
    /// <summary>Retrieves the number of files whose download has completed.</summary>
    abstract member Status: unit -> Result<int, ComError>
    /// <summary>Skips the current download file.</summary>
    abstract member Skip: unit -> Result<unit, ComError>
    /// <summary>Cancels the active download thread.</summary>
    abstract member Cancel: unit -> Result<unit, ComError>
    /// <summary>Deletes a previously downloaded file from the save path.</summary>
    abstract member DeleteFile: filename: string -> Result<unit, ComError>
    /// <summary>Starts JV-Link watch event notifications.</summary>
    abstract member WatchEvent: (string -> unit) -> Result<unit, ComError>
    /// <summary>Stops JV-Link watch event notifications.</summary>
    abstract member WatchEventClose: unit -> Result<unit, ComError>
    /// <summary>Shows JV-Link UI configuration dialog (`JVSetUIProperties`).</summary>
    abstract member SetUiProperties: unit -> Result<unit, ComError>
    /// <summary>Sets the save flag via `JVSetSaveFlag`.</summary>
    abstract member SetSaveFlag: enabled: bool -> Result<unit, ComError>
    /// <summary>Sets the service key via `JVSetServiceKey`.</summary>
    abstract member SetServiceKeyDirect: key: string -> Result<unit, ComError>
    /// <summary>Sets the save path via `JVSetSavePath`.</summary>
    abstract member SetSavePathDirect: path: string -> Result<unit, ComError>
    /// <summary>Sets the parent window handle for JV-Link dialogs (<c>ParentHWnd</c>).</summary>
    /// <remarks>
    /// In COM mode, <c>ParentHWnd</c> is write-only: reading it back is not supported.
    /// </remarks>
    abstract member SetParentWindowHandleDirect: handle: IntPtr -> Result<unit, ComError>
    /// <summary>Attempts to set whether payoff dialogs are suppressed (<c>m_payflag</c>).</summary>
    /// <remarks>
    /// In COM mode, <c>m_payflag</c> is effectively read-only (write fails). Users can still change this
    /// setting interactively via <see cref="SetUiProperties"/> (JVSetUIProperties dialog).
    /// </remarks>
    abstract member SetPayoffDialogSuppressedDirect: suppressed: bool -> Result<unit, ComError>
    /// <summary>Retrieves a course diagram with explanation.</summary>
    abstract member CourseFile: key: string -> Result<string * string, ComError>
    /// <summary>Saves a course diagram to the specified filepath via `JVCourseFile2`.</summary>
    abstract member CourseFile2: key: string * filepath: string -> Result<unit, ComError>
    /// <summary>Generates a silks bitmap on disk via <c>JVFukuFile</c>.</summary>
    /// <returns>Some path if image created, None if No Image (pattern not found).</returns>
    abstract member SilksFile: pattern: string * outputPath: string -> Result<string option, ComError>
    /// <summary>Retrieves a silks bitmap as binary data via <c>JVFuku</c>.</summary>
    /// <returns>Some bytes if image data returned, None if No Image (pattern not found).</returns>
    abstract member SilksBinary: pattern: string -> Result<byte[] option, ComError>
    /// <summary>Checks movie availability via <c>JVMVCheck</c> (race videos only).</summary>
    abstract member MovieCheck: key: string -> Result<MovieAvailability, ComError>
    /// <summary>Checks movie availability for a given movietype via <c>JVMVCheckWithType</c>.</summary>
    abstract member MovieCheckWithType: movieType: string * key: string -> Result<MovieAvailability, ComError>
    /// <summary>Requests movie playback via <c>JVMVPlay</c>.</summary>
    abstract member MoviePlay: key: string -> Result<unit, ComError>
    /// <summary>Requests movie playback for a given movietype via <c>JVMVPlayWithType</c>.</summary>
    abstract member MoviePlayWithType: movieType: string * key: string -> Result<unit, ComError>
    /// <summary>Opens a workout movie list via <c>JVMVOpen</c>.</summary>
    abstract member MovieOpen: movieType: string * searchKey: string -> Result<unit, ComError>
    /// <summary>Reads the next workout movie entry via <c>JVMVRead</c>.</summary>
    abstract member MovieRead: unit -> Result<MovieReadOutcome, ComError>
    /// <summary>Gets or sets whether downloads are persisted to disk (<c>m_saveflag</c>).</summary>
    /// <remarks>The getter returns a default value (false) if COM property access fails. Use <see cref="TryGetSaveFlag"/> for explicit error handling.</remarks>
    abstract member SaveFlag: bool with get, set
    /// <summary>Gets the save path configured within JV-Link.</summary>
    /// <remarks>
    /// This property is read-only. Per JV-Link specification, m_savepath can only be modified
    /// via JVSetSavePath or JVSetUIProperties methods.
    /// The getter returns an empty string if COM property access fails. Use <see cref="TryGetSavePath"/> for explicit error handling.
    /// </remarks>
    abstract member SavePath: string
    /// <summary>Gets the service key configured within JV-Link.</summary>
    /// <remarks>
    /// This property is read-only. Per JV-Link specification, m_servicekey can only be modified
    /// via JVSetServiceKey or JVSetUIProperties methods.
    /// The getter returns an empty string if COM property access fails. Use <see cref="TryGetServiceKey"/> for explicit error handling.
    /// </remarks>
    abstract member ServiceKey: string
    /// <summary>Attempts to retrieve whether downloads are persisted to disk (<c>m_saveflag</c>).</summary>
    /// <returns>Ok(bool) on success, or Error if COM property access fails.</returns>
    abstract member TryGetSaveFlag: unit -> Result<bool, ComError>
    /// <summary>Attempts to retrieve the save path configured within JV-Link.</summary>
    /// <returns>Ok(string) on success, or Error if COM property access fails.</returns>
    abstract member TryGetSavePath: unit -> Result<string, ComError>
    /// <summary>Attempts to retrieve the service key configured within JV-Link.</summary>
    /// <returns>Ok(string) on success, or Error if COM property access fails.</returns>
    abstract member TryGetServiceKey: unit -> Result<string, ComError>
    /// <summary>Attempts to retrieve the JV-Link version string (<c>m_JVLinkVersion</c>).</summary>
    /// <returns>Ok(string) on success, or Error if COM property access fails.</returns>
    abstract member TryGetJVLinkVersion: unit -> Result<string, ComError>
    /// <summary>Attempts to retrieve the total size (in KB, kilobytes) remaining in the current download session.</summary>
    /// <remarks>JV-Link returns this value in KB (value / 1024). Use <c>GetTotalReadFileSizeBytes</c> in JvLinkService for bytes.</remarks>
    /// <returns>Ok(int64) on success, or Error if COM property access fails.</returns>
    abstract member TryGetTotalReadFileSize: unit -> Result<int64, ComError>
    /// <summary>Attempts to retrieve the size (in bytes) of the currently read file.</summary>
    /// <returns>Ok(int64) on success, or Error if COM property access fails.</returns>
    abstract member TryGetCurrentReadFileSize: unit -> Result<int64, ComError>
    /// <summary>Attempts to retrieve the timestamp of the currently open file within JV-Link.</summary>
    /// <returns>Ok(DateTime option) on success, or Error if COM property access fails.</returns>
    abstract member TryGetCurrentFileTimestamp: unit -> Result<DateTime option, ComError>
    /// <summary>Attempts to retrieve the parent window handle for JV dialogs.</summary>
    /// <remarks>In COM mode, <c>ParentHWnd</c> is write-only and cannot be read.</remarks>
    /// <returns>Ok(IntPtr) on success, or Error if the value cannot be read.</returns>
    abstract member TryGetParentWindowHandle: unit -> Result<IntPtr, ComError>
    /// <summary>Attempts to retrieve whether payoff dialogs are suppressed (<c>m_payflag</c>).</summary>
    /// <returns>Ok(bool) on success, or Error if COM property access fails.</returns>
    abstract member TryGetPayoffDialogSuppressed: unit -> Result<bool, ComError>
    /// <summary>Gets the JV-Link version string (<c>m_JVLinkVersion</c>).</summary>
    /// <remarks>The getter returns an empty string if COM property access fails. Use <see cref="TryGetJVLinkVersion"/> for explicit error handling.</remarks>
    abstract member JVLinkVersion: string
    /// <summary>Total size (in KB, kilobytes) remaining in the current download session (<c>m_TotalReadFilesize</c>).</summary>
    /// <remarks>JV-Link returns this value in KB (value / 1024). The getter returns 0 if COM property access fails. Use <see cref="TryGetTotalReadFileSize"/> for explicit error handling.</remarks>
    abstract member TotalReadFileSize: int64
    /// <summary>Size (in bytes) of the currently read file (<c>m_CurrentReadFilesize</c>).</summary>
    /// <remarks>The getter returns 0 if COM property access fails. Use <see cref="TryGetCurrentReadFileSize"/> for explicit error handling.</remarks>
    abstract member CurrentReadFileSize: int64
    /// <summary>Timestamp of the currently open file within JV-Link.</summary>
    /// <remarks>The getter returns None if COM property access fails. Use <see cref="TryGetCurrentFileTimestamp"/> for explicit error handling.</remarks>
    abstract member CurrentFileTimestamp: DateTime option
    /// <summary>Gets or sets the parent window handle for JV dialogs.</summary>
    /// <remarks>
    /// In COM mode, <c>ParentHWnd</c> is write-only. The getter returns IntPtr.Zero.
    /// Use <see cref="TryGetParentWindowHandle"/> for explicit error handling.
    /// </remarks>
    abstract member ParentWindowHandle: IntPtr with get, set
    /// <summary>Gets or sets whether payoff dialogs are suppressed (<c>m_payflag</c>).</summary>
    /// <remarks>
    /// In COM mode, <c>m_payflag</c> is effectively read-only: setting it programmatically is not supported.
    /// The setter is best-effort and may be ignored. Use <see cref="SetUiProperties"/> to change the value interactively.
    /// The getter returns false if COM property access fails. Use <see cref="TryGetPayoffDialogSuppressed"/> for explicit error handling.
    /// </remarks>
    abstract member PayoffDialogSuppressed: bool with get, set
