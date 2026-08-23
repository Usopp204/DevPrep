"""Class spec - Report Camera Violation [BL#41].

Owning service: InterviewAPI. Entry point:
  InterviewSessionController.ReportCameraViolation  (POST .../{id}/camera-violation)
  -> SessionService.ReportCameraViolationAsync
"""

SPEC = {
    'file': 'ReportCameraViolation.drawio',
    'title': 'Class Diagram - Report Camera Violation [BL#41] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'violation_req', 'api'],
        ['isvc', 'svc', 'violation_res', 'strike_helper', 'capability_policy'],
        ['ievtrepo', 'evtrepo', 'isessrepo', 'sessrepo', 'db'],
        ['event', 'session', 'room'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ ReportCameraViolation(id: Guid, req: ReportCameraViolationRequest, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'violation_req': {
            'name': 'ReportCameraViolationRequest <<DTO>>',
            'dto': True,
            'fields': ['+ IncidentId: Guid', '+ Reason: string'],
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
            'name': '<<interface>> ISessionService',
            'methods': [
                '+ ReportCameraViolationAsync(userId: Guid, sessionId: Guid, req: ReportCameraViolationRequest, ct: CancellationToken): Task<ReportCameraViolationResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _verificationEventRepository: ISessionVerificationEventRepository',
                '- _configuration: IConfiguration',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ ReportCameraViolationAsync(userId: Guid, sessionId: Guid, req: ReportCameraViolationRequest, ct: CancellationToken): Task<ReportCameraViolationResponse>',
            ],
        },
        'violation_res': {
            'name': 'ReportCameraViolationResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ CameraViolationCount: int',
                '+ StrikeCount: int',
                '+ MaxStrikes: int',
                '+ Duplicate: bool',
                '+ ShouldEndSession: bool',
                '+ Message: string',
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
        'capability_policy': {
            'name': 'SessionCapabilityPolicy',
            'fields': ['+ IntegrityViolation: string'],
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
        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ IncrementCameraViolationAsync(sessionId: Guid, ct: CancellationToken): Task<int>',
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
                '+ IncrementCameraViolationAsync(sessionId: Guid, ct: CancellationToken): Task<int>',
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
                '+ CameraViolationCount: int',
                '+ TerminationReason: string?',
                '+ EndedAt: DateTime?',
                '+ UpdatedAt: DateTime',
            ],
        },
        'room': {
            'name': 'InterviewRoom <<entity>>',
            'fields': ['+ RoomId: Guid', '+ SessionId: Guid', '+ Status: string', '+ ClosedAt: DateTime?'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'violation_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'violation_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'strike_helper', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'capability_policy', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..1'},
    ],
}
