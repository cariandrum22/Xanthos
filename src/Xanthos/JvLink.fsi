namespace Xanthos

/// Synchronous SDK operations. COM executes on the session's STA; UI calls wait for the user's choice.
module JvLink =
    val connect: ConnectionOptions -> Result<Session, JvError>
    val disconnect: Session -> Result<unit, JvError>
    val withSession: ConnectionOptions -> (Session -> Result<'a, JvError>) -> Result<'a, JvError>
    val init: string -> Session -> Result<unit, JvError>
    val configureUi: Session -> Result<unit, JvError>
    val setServiceKey: string -> Session -> Result<unit, JvError>
    val setSaveFlag: bool -> Session -> Result<unit, JvError>
    val setSavePath: string -> Session -> Result<unit, JvError>
    val openData: OpenRequest -> Session -> Result<OpenOutcome, JvError>
    val openRealtime: string -> string -> Session -> Result<RealtimeOpenOutcome, JvError>
    val status: Session -> Result<int, JvError>
    val read: Session -> Result<ReadResult, JvError>
    val gets: Session -> Result<ReadResult, JvError>
    val readWithCapacity: int -> Session -> Result<ReadResult, JvError>
    val getsWithCapacity: int -> Session -> Result<ReadResult, JvError>
    val decodeShiftJis: byte[] -> string
    val skip: Session -> Result<unit, JvError>
    val cancel: Session -> Result<unit, JvError>
    val closeData: Session -> Result<unit, JvError>
    val deleteFile: string -> Session -> Result<unit, JvError>
    val silksFile: string -> string -> Session -> Result<ImageResult<string>, JvError>
    val silksBinary: string -> Session -> Result<ImageResult<byte[]>, JvError>
    val courseFile: string -> Session -> Result<ImageResult<CourseImage>, JvError>
    val courseFile2: string -> string -> Session -> Result<ImageResult<string>, JvError>
    val movieCheck: string -> Session -> Result<VideoAvailability, JvError>
    val movieCheckWithType: string -> string -> Session -> Result<VideoAvailability, JvError>
    val moviePlay: string -> Session -> Result<unit, JvError>
    val moviePlayWithType: string -> string -> Session -> Result<unit, JvError>
    val movieOpen: string -> string -> Session -> Result<VideoOpenOutcome, JvError>
    val movieRead: Session -> Result<VideoReadOutcome, JvError>
    val movieReadWithCapacity: int -> Session -> Result<VideoReadOutcome, JvError>
    val getSaveFlag: Session -> Result<int, JvError>
    val getSavePath: Session -> Result<string, JvError>
    val getServiceKey: Session -> Result<string, JvError>
    val getVersion: Session -> Result<string, JvError>
    val getTotalReadFileSize: Session -> Result<FileSizeKilobytes, JvError>
    val getCurrentReadFileSize: Session -> Result<int, JvError>
    /// Returns an empty string when the SDK has no current file timestamp (null BSTR).
    val getCurrentFileTimestamp: Session -> Result<string, JvError>
    val setParentWindowHandle: nativeint -> Session -> Result<unit, JvError>
    val getPayFlag: Session -> Result<int, JvError>
    val watchEvent: (JvEvent -> unit) -> Session -> Result<unit, JvError>
    val watchEventClose: Session -> Result<unit, JvError>
    /// Consumers run on an owned worker. Overflow or consumer exceptions stop delivery and are observable.
    val subscribe: (JvEvent -> unit) -> Session -> Result<Subscription, JvError>
    val subscribeWithOptions: SubscriptionOptions -> (JvEvent -> unit) -> Session -> Result<Subscription, JvError>
    val unsubscribe: Subscription -> Result<unit, JvError>
    val subscriptionError: Subscription -> JvError option
    val watchError: Session -> Result<JvError option, JvError>
    val parseEvent: JvEvent -> Result<ParsedEvent, JvError>
    val toRealtimeRequest: JvEvent -> Result<RealtimeRequest, JvError>
