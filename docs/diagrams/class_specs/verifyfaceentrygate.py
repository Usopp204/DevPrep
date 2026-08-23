"""Class spec - Verify Face Entry Gate [BL#48].

Owning service: InterviewAPI. Entry point:
  SessionVerificationController.VerifyFace  (POST .../verify/face?phase=Entry)
  -> BiometricVerificationService.VerifyFaceAsync
"""

SPEC = {
    'file': 'VerifyFaceEntryGate.drawio',
    'title': 'Class Diagram - Verify Face Entry Gate [BL#48] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'result_res', 'quality_ex'],
        ['iai', 'ai', 'facelive_res'],
        ['ichalrepo', 'chalrepo', 'ievtrepo', 'evtrepo', 'ibiorepo', 'biorepo', 'isessrepo', 'sessrepo', 'db'],
        ['challenge', 'event', 'session', 'biometric'],
    ],

    'classes': {
        'ctrl': {
            'name': 'SessionVerificationController (InterviewAPI)',
            'fields': [
                '- _service: IBiometricVerificationService',
                '- _logger: ILogger<SessionVerificationController>',
            ],
            'methods': [
                '+ VerifyFace(sessionId: Guid, frames: List<IFormFile>, cancellationToken: CancellationToken, phase: string): Task<IActionResult>',
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
                '+ VerifyFaceAsync(userId: Guid, sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken, phase: string): Task<VerificationResultResponse>',
            ],
        },
        'svc': {
            'name': 'BiometricVerificationService (InterviewAPI)',
            'fields': [
                '- _challengeRepository: ISessionVerificationChallengeRepository',
                '- _eventRepository: ISessionVerificationEventRepository',
                '- _biometricsRepository: IProfileBiometricsRepository',
                '- _sessionRepository: ISessionRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _configuration: IConfiguration',
            ],
            'methods': [
                '+ VerifyFaceAsync(userId: Guid, sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken, phase: string): Task<VerificationResultResponse>',
                '- EnsureEnabled(): void',
                '- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task',
                '- ConsumeChallengeAsync(sessionId: Guid, modality: string, ct: CancellationToken): Task<SessionVerificationChallenge>',
                '- GetStoredEmbeddingAsync(userId: Guid, modality: string, ct: CancellationToken): Task<float[]>',
                '- RecordEventAsync(sessionId: Guid, userId: Guid, phase: string, modality: string, score: double?, livenessPassed: bool?, matched: bool, decision: string, detail: object, ct: CancellationToken): Task',
            ],
        },
        'result_res': {
            'name': 'VerificationResultResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Modality: string',
                '+ Passed: bool',
                '+ Score: double?',
                '+ Live: bool?',
                '+ ContentOk: bool?',
                '+ Transcript: string?',
                '+ Reason: string?',
            ],
        },
        'quality_ex': {
            'name': 'BiometricQualityException',
            'methods': [
                '+ BiometricQualityException(message: string)',
            ],
        },

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ VerifyFaceLiveAsync(frames: IReadOnlyList<IFormFile>, storedEmbedding: float[], challengeJson: string, ct: CancellationToken): Task<AiFaceVerifyLiveResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ VerifyFaceLiveAsync(frames: IReadOnlyList<IFormFile>, storedEmbedding: float[], challengeJson: string, ct: CancellationToken): Task<AiFaceVerifyLiveResponse>',
            ],
        },
        'facelive_res': {
            'name': 'AiFaceVerifyLiveResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Passed: bool',
                '+ Live: bool',
                '+ IdentityScore: double?',
                '+ Threshold: double',
                '+ YawRange: double',
                '+ MovedEnough: bool?',
                '+ DirectionOk: bool?',
                '+ Reason: string?',
            ],
        },

        'ichalrepo': {
            'name': '<<interface>> ISessionVerificationChallengeRepository',
            'methods': [
                '+ GetActiveAsync(sessionId: Guid, modality: string, now: DateTime, ct: CancellationToken): Task<SessionVerificationChallenge?>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'chalrepo': {
            'name': 'SessionVerificationChallengeRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetActiveAsync(sessionId: Guid, modality: string, now: DateTime, ct: CancellationToken): Task<SessionVerificationChallenge?>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'ievtrepo': {
            'name': '<<interface>> ISessionVerificationEventRepository',
            'methods': [
                '+ AddAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task',
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ AddAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task',
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
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ SessionVerificationChallenges: DbSet<SessionVerificationChallenge>',
                '+ SessionVerificationEvents: DbSet<SessionVerificationEvent>',
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'challenge': {
            'name': 'SessionVerificationChallenge <<entity>>',
            'fields': [
                '+ Id: long',
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Modality: string',
                '+ ChallengeJson: string',
                '+ ExpiresAt: DateTime',
                '+ ConsumedAt: DateTime?',
                '+ CreatedAt: DateTime',
            ],
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
                '+ CreatedAt: DateTime',
            ],
        },
        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': ['+ SessionId: Guid', '+ UserId: Guid', '+ Status: string'],
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
        {'from': 'svc', 'to': 'result_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ichalrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'facelive_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'quality_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'chalrepo', 'to': 'ichalrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'chalrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'challenge', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
