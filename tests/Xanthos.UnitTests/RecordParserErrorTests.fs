module Xanthos.UnitTests.RecordParserErrorTests

open System
open Xunit
open Xanthos.Core
open Xanthos.Core.Text
open Xanthos.Core.Records

// ============================================================================
// TK Parser - Error Cases
// ============================================================================

[<Fact>]
let ``TK parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short (need 346 bytes)
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)

    match TK.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``TK parse should fail with empty data`` () =
    match TK.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``TK parse should handle corrupted Shift-JIS data`` () =
    let data = Array.create 346 0xFFuy // Invalid Shift-JIS bytes
    Array.Copy(encodeShiftJis "TK", 0, data, 0, 2)

    match TK.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// RA Parser - Error Cases
// ============================================================================

[<Fact>]
let ``RA parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short (need 366 bytes)
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)

    match RA.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``RA parse should fail with empty data`` () =
    match RA.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``RA parse should handle corrupted Shift-JIS data`` () =
    let data = Array.create 366 0xFFuy // Invalid Shift-JIS bytes
    Array.Copy(encodeShiftJis "RA", 0, data, 0, 2)

    match RA.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// SE Parser - Error Cases
// ============================================================================

[<Fact>]
let ``SE parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short (need 1446 bytes)
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)

    match SE.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``SE parse should fail with empty data`` () =
    match SE.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``SE parse should handle corrupted Shift-JIS data`` () =
    let data = Array.create 1446 0xFFuy // Invalid Shift-JIS bytes
    Array.Copy(encodeShiftJis "SE", 0, data, 0, 2)

    match SE.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// HR Parser - Error Cases
// ============================================================================

[<Fact>]
let ``HR parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short
    Array.Copy(encodeShiftJis "HR", 0, data, 0, 2)

    match HR.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``HR parse should fail with empty data`` () =
    match HR.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

// ============================================================================
// O1 Parser - Error Cases
// ============================================================================

[<Fact>]
let ``O1 parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)

    match O1.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``O1 parse should fail with empty data`` () =
    match O1.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``O1 parse should handle corrupted data gracefully`` () =
    let data = Array.create 100 0xFFuy
    Array.Copy(encodeShiftJis "O1", 0, data, 0, 2)

    match O1.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// H1 Parser - Error Cases
// ============================================================================

[<Fact>]
let ``H1 parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)

    match H1.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``H1 parse should fail with empty data`` () =
    match H1.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``H1 parse should handle corrupted data gracefully`` () =
    let data = Array.create 100 0xFFuy
    Array.Copy(encodeShiftJis "H1", 0, data, 0, 2)

    match H1.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// WF Parser - Error Cases
// ============================================================================

[<Fact>]
let ``WF parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)

    match WF.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``WF parse should fail with empty data`` () =
    match WF.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``WF parse should handle corrupted data gracefully`` () =
    let data = Array.create 100 0xFFuy
    Array.Copy(encodeShiftJis "WF", 0, data, 0, 2)

    match WF.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// UM Parser - Error Cases
// ============================================================================

[<Fact>]
let ``UM parse should fail with data too short`` () =
    let data = Array.create 10 32uy // Far too short
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)

    match UM.parse data with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with insufficient data length"

[<Fact>]
let ``UM parse should fail with empty data`` () =
    match UM.parse [||] with
    | Error _ -> () // Expected
    | Ok _ -> failwith "Should fail with empty data"

[<Fact>]
let ``UM parse should handle corrupted data gracefully`` () =
    let data = Array.create 500 0xFFuy
    Array.Copy(encodeShiftJis "UM", 0, data, 0, 2)

    match UM.parse data with
    | Ok _ -> () // May succeed with default values
    | Error _ -> () // Or may fail - both are acceptable

// ============================================================================
// Additional Parsers - Error Cases (O2-O6, H5, H6, JC, TC, etc.)
// ============================================================================

[<Fact>]
let ``All parsers should handle single byte data`` () =
    let singleByte = [| 0uy |]

    // These should all return errors gracefully
    match TK.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "TK should fail"

    match RA.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "RA should fail"

    match SE.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "SE should fail"

    match HR.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "HR should fail"

    match O1.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "O1 should fail"

    match H1.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "H1 should fail"

    match WF.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "WF should fail"

    match UM.parse singleByte with
    | Error _ -> ()
    | Ok _ -> failwith "UM should fail"
