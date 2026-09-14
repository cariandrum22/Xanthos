namespace Xanthos.Interop

open System
open Xanthos.Core

[<AutoOpen>]
module ComInterop =

    /// The owned COM STA uses Japanese LCID 1041, so JVRead returns Japanese BSTR text.
    /// Restore the documented CP932 record bytes without silent replacement.
    /// JVGets is the preferred raw-byte path.
    let internal readBstrBytes (text: string) =
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance)

        let encoding =
            System.Text.Encoding.GetEncoding(
                932,
                System.Text.EncoderFallback.ExceptionFallback,
                System.Text.DecoderFallback.ExceptionFallback
            )

        encoding.GetBytes text

    /// JV-Link 5.0 declares ParentHWnd as VT_I4 even in its x64 type library.
    let internal windowHandleToInt32 (handle: IntPtr) : Result<int, ComError> =
        let value = handle.ToInt64()

        if value < int64 Int32.MinValue || value > int64 Int32.MaxValue then
            Error(InvalidInput "ParentHWnd must fit the signed 32-bit COM Long contract.")
        else
            Ok(int value)

    let inline marshalToManaged (value: obj) = value

    let inline toNullableDate (value: obj) =
        match value with
        | :? string as text when String.IsNullOrWhiteSpace text -> None
        | :? string as text ->
            match DateTime.TryParse text with
            | true, dt -> Some dt
            | _ -> None
        | _ -> None
