"""Class spec - Verify Voice Answer [BL#52, #67].

Owning service: InterviewAPI. Entry point:
  SessionVerificationController.VerifyVoiceAnswer  (POST .../verify/voice-answer)
  -> BiometricVerificationService.VerifyVoiceAnswerAsync

Speaker-only check: transcribe the answer, then match it against the enrolled
voice template. No challenge phrase and no voice liveness are applied here.
"""

SPEC = {
    'file': 'VerifyVoiceAnswer.drawio',
    'title': 'Class Diagram - Verify Voice Answer [BL#52, #67] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'voice_res', 'strike_helper', 'codec', 'capability_policy'],
        ['iai', 'ai', 'faceverify_res'],
        ['ievtrepo', 'evtrepo', 'ibiorepo', 'biorepo', 'isessrepo', 'sessrepo', 'db'],
        ['event', 'session', 'room', 'biometric'],
    ],

    'classes': {
        'ctrl': {
            'name': 'SessionVerificationController (InterviewAPI)',
            'fields': [
                '- _service: IBiometricVerificationService',
                '- _logger: ILogger<SessionVerificationController>',
            ],
            'methods': [
                '+ VerifyVoiceAnswer(sessionId: Guid, checkId: Guid, audio: IFormFile, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'api': {
            'name': 'ApiResponse<T> <<DTO>>',
            'dto': True,
            'fields': ['+ Success: bool', '+ Message: string?', '+ Data: T?'],
            'methods': [
                '+ Ok(data: T, message: string?): ApiResponse<T>',
                '+ Fail(message: string): ApiResponse<T>',
            ],
        },

        'isvc': {
            'name': '<<interface>> IBiometricVerificationService',
            'methods': [
                '+ VerifyVoiceAnswerAsync(userId: Guid, sessionId: Guid, checkId: Guid, audio: IFormFile, ct: CancellationToken): Task<VoiceAnswerVerifyResponse>',
            ],
        },
        'svc': {
            'name': 'BiometricVerificationService (InterviewAPI)',
            'fields': [
                '- _eventRepository: ISessionVerificationEventRepository',
                '- _biometricsRepository: IProfileBiometricsRepository',
                '- _sessionRepository: ISessionRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _configuration: IConfiguration',
            ],
            'methods': [
                '+ VerifyVoiceAnswerAsync(userId: Guid, sessionId: Guid, checkId: Guid, audio: IFormFile, ct: CancellationToken): Task<VoiceAnswerVerifyResponse>',
                '- RecordEventAsync(sessionId: Guid, userId: Guid, phase: string, modality: string, score: double?, livenessPassed: bool?, matched: bool, decision: string, detail: object, ct: CancellationToken): Task',
                '- EndSessionForCheatingAsync(sessionId: Guid, ct: CancellationToken): Task',
            ],
        },
        'voice_res': {
            'name': 'VoiceAnswerVerifyResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Text: string',
                '+ VoiceChecked: bool',
                '+ VoiceMatched: bool?',
                '+ Score: double?',
                '+ StrikeCount: int',
                '+ MaxStrikes: int',
                '+ ShouldEndSession: bool',
                '+ Duplicate: bool',
                '+ Message: string?',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ CanGeneratePerformanceReport: bool',
                '+ CanSubmitInterviewFeedback: bool',
            ],
        },
        'strike_helper': {
            'name': 'CheatingStrikeHelper',
            'methods': [
                '+ ComputeEffectiveStrikeCount(events: IEnumerable<SessionVerificationEvent>, noFaceStrikeThreshold: int, tabStrikeThreshold: int): int',
                '+ ShouldEndSession(strikeJustHappened: bool, strictness: string?, effectiveStrikeCount: int, maxStrikes: int): bool',
            ],
        },
        'codec': {
            'name': 'EmbeddingCodec',
            'methods': ['+ ToFloats(bytes: byte[]): float[]'],
        },
        'capability_policy': {
            'name': 'SessionCapabilityPolicy',
            'fields': ['+ IntegrityViolation: string'],
        },

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ TranscribeAsync(audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<string>',
                '+ VerifyVoiceAsync(audio: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ TranscribeAsync(audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<string>',
                '+ VerifyVoiceAsync(audio: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>',
            ],
        },
        'faceverify_res': {
            'name': 'AiFaceVerifyResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Match: bool', '+ Score: double', '+ Threshold: double'],
        },

        'ievtrepo': {
            'name': '<<interface>> ISessionVerificationEventRepository',
            'methods': [
                '+ TryAddIncidentAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task<bool>',
                '+ GetIncidentAsync(incidentId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>',
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ TryAddIncidentAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task<bool>',
                '+ GetIncidentAsync(incidentId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>',
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
            ],
        },
        'ibiorepo': {
            'name': '<<interface>> IProfileBiometricsRepository',
            'methods': [
                '+ GetAsync(userId: Guid, modality: string, ct: CancellationToken): Task<ProfileBiometric?>',
            ],
        },
        'biorepo': {
            'name': 'ProfileBiometricsRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetAsync(userId: Guid, modality: string, ct: CancellationToken): Task<ProfileBiometric?>',
            ],
        },
        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ InterviewRooms: DbSet<InterviewRoom>',
                '+ SessionVerificationEvents: DbSet<SessionVerificationEvent>',
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'event': {
            'name': 'SessionVerificationEvent <<entity>>',
            'fields': [
                '+ Id: long',
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Phase: string',
                '+ Modality: string',
                '+ Score: double?',
                '+ Matched: bool',
                '+ Decision: string',
                '+ Detail: string?',
                '+ IncidentId: Guid?',
                '+ CreatedAt: DateTime',
            ],
        },
        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ EndedAt: DateTime?',
                '+ UpdatedAt: DateTime',
            ],
        },
        'room': {
            'name': 'InterviewRoom <<entity>>',
            'fields': ['+ RoomId: Guid', '+ SessionId: Guid', '+ Status: string', '+ ClosedAt: DateTime?'],
        },
        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': ['+ UserId: Guid', '+ Modality: string', '+ Embedding: byte[]'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'voice_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'strike_helper', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'codec', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'capability_policy', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'faceverify_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..1'},
        {'from': 'session', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
