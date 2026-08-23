"""Class spec - Verify Face In Session [BL#50, #51].

Owning service: InterviewAPI. Entry point:
  SessionVerificationController.VerifyInSession  (POST .../verify/in-session)
  -> BiometricVerificationService.VerifyInSessionAsync
"""

SPEC = {
    'file': 'VerifyFaceInSession.drawio',
    'title': 'Class Diagram - Verify Face In Session [BL#50, #51] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'insession_res', 'quality_ex', 'strike_helper', 'capability_policy'],
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
                '+ VerifyInSession(sessionId: Guid, checkId: Guid, frame: IFormFile, cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ VerifyInSessionAsync(userId: Guid, sessionId: Guid, checkId: Guid, frame: IFormFile, ct: CancellationToken): Task<InSessionVerifyResponse>',
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
                '+ VerifyInSessionAsync(userId: Guid, sessionId: Guid, checkId: Guid, frame: IFormFile, ct: CancellationToken): Task<InSessionVerifyResponse>',
                '- EnsureEnabled(): void',
                '- GetStoredEmbeddingAsync(userId: Guid, modality: string, ct: CancellationToken): Task<float[]>',
                '- RecordEventAsync(sessionId: Guid, userId: Guid, phase: string, modality: string, score: double?, livenessPassed: bool?, matched: bool, decision: string, detail: object, ct: CancellationToken): Task',
                '- EndSessionForCheatingAsync(sessionId: Guid, ct: CancellationToken): Task',
            ],
        },
        'insession_res': {
            'name': 'InSessionVerifyResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Passed: bool',
                '+ NoFaceDetected: bool',
                '+ WarningType: string',
                '+ StrikeCount: int',
                '+ MaxStrikes: int',
                '+ NoFaceCount: int',
                '+ NoFaceStrikeThreshold: int',
                '+ NoFaceJustBecameStrike: bool',
                '+ ShouldEndSession: bool',
                '+ Message: string',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ CanGeneratePerformanceReport: bool',
                '+ CanSubmitInterviewFeedback: bool',
            ],
        },
        'quality_ex': {
            'name': 'BiometricQualityException',
            'fields': ['+ QualityCode: string?', '+ FaceCount: int?'],
            'methods': [
                '+ BiometricQualityException(message: string, qualityCode: string?, faceCount: int?)',
            ],
        },
        'strike_helper': {
            'name': 'CheatingStrikeHelper',
            'methods': [
                '+ ComputeEffectiveStrikeCount(events: IEnumerable<SessionVerificationEvent>, noFaceStrikeThreshold: int, tabStrikeThreshold: int): int',
                '+ ShouldEndSession(strikeJustHappened: bool, strictness: string?, effectiveStrikeCount: int, maxStrikes: int): bool',
            ],
        },
        'capability_policy': {
            'name': 'SessionCapabilityPolicy',
            'fields': ['+ IntegrityViolation: string'],
        },

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ VerifyFaceAsync(frame: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ VerifyFaceAsync(frame: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>',
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
                '+ AddAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task',
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryConsumeQuestionCheckAsync(incidentId: Guid, sessionId: Guid, userId: Guid, consumedAt: DateTime, ct: CancellationToken): Task<bool>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ AddAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task',
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryConsumeQuestionCheckAsync(incidentId: Guid, sessionId: Guid, userId: Guid, consumedAt: DateTime, ct: CancellationToken): Task<bool>',
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
                '+ LivenessPassed: bool?',
                '+ Matched: bool',
                '+ Decision: string',
                '+ Detail: string?',
                '+ IncidentId: Guid?',
                '+ SessionQuestionId: Guid?',
                '+ ExpiresAt: DateTime?',
                '+ ConsumedAt: DateTime?',
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
            'fields': [
                '+ RoomId: Guid',
                '+ SessionId: Guid',
                '+ Status: string',
                '+ ClosedAt: DateTime?',
            ],
        },
        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': [
                '+ UserId: Guid',
                '+ Modality: string',
                '+ Embedding: byte[]',
                '+ Dim: int',
                '+ ModelVersion: string',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'insession_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'strike_helper', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'capability_policy', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'faceverify_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'quality_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
