"""Class spec - Validate Entry Gate Status [BL#49].

Owning service: InterviewAPI. Entry point:
  SessionVerificationController.GetEntryGateStatus  (GET .../verify/entry-gate)
  -> BiometricVerificationService.GetEntryGateStatusAsync

Read-only decision: it inspects enrollment plus Entry-phase audit rows. It calls no
ai-service endpoint, so no IAiServiceClient column appears here.
"""

SPEC = {
    'file': 'ValidateEntryGateStatus.drawio',
    'title': 'Class Diagram - Validate Entry Gate Status [BL#49] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'gate_res'],
        ['ibiorepo', 'biorepo', 'ievtrepo', 'evtrepo', 'isessrepo', 'sessrepo', 'db'],
        ['biometric', 'event', 'session'],
    ],

    'classes': {
        'ctrl': {
            'name': 'SessionVerificationController (InterviewAPI)',
            'fields': [
                '- _service: IBiometricVerificationService',
                '- _logger: ILogger<SessionVerificationController>',
            ],
            'methods': [
                '+ GetEntryGateStatus(sessionId: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ GetEntryGateStatusAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EntryGateStatusResponse>',
            ],
        },
        'svc': {
            'name': 'BiometricVerificationService (InterviewAPI)',
            'fields': [
                '- _eventRepository: ISessionVerificationEventRepository',
                '- _biometricsRepository: IProfileBiometricsRepository',
                '- _sessionRepository: ISessionRepository',
                '- _configuration: IConfiguration',
            ],
            'methods': [
                '+ GetEntryGateStatusAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EntryGateStatusResponse>',
                '- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task',
            ],
        },
        'gate_res': {
            'name': 'EntryGateStatusResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ FaceEnrolled: bool',
                '+ Allowed: bool',
                '+ AttemptsUsed: int',
                '+ AttemptsRemaining: int',
                '+ Reason: string',
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
        'ievtrepo': {
            'name': '<<interface>> ISessionVerificationEventRepository',
            'methods': [
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
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
                '+ SessionVerificationEvents: DbSet<SessionVerificationEvent>',
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': ['+ UserId: Guid', '+ Modality: string', '+ ModelVersion: string'],
        },
        'event': {
            'name': 'SessionVerificationEvent <<entity>>',
            'fields': [
                '+ Id: long',
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Phase: string',
                '+ Modality: string',
                '+ Decision: string',
                '+ CreatedAt: DateTime',
            ],
        },
        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': ['+ SessionId: Guid', '+ UserId: Guid', '+ Status: string'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'gate_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'db', 'kind': 'association'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
