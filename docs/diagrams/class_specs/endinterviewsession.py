"""Class spec - End Interview Session [BL#37, #61].

Owning service: InterviewAPI. Entry points:
  InterviewSessionController.CompleteSession / EndSession  (POST .../{id}/complete | /end)
  -> SessionService.CompleteSessionAsync / EndSessionAsync

Complete is the deadline-reached path, End is the candidate-cancel path. Both
close the room and derive the post-session capability flags from
SessionCapabilityPolicy.
"""

SPEC = {
    'file': 'EndInterviewSession.drawio',
    'title': 'Class Diagram - End Interview Session [BL#37, #61] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'end_res', 'capability_policy', 'capabilities'],
        ['irepo', 'repo', 'db'],
        ['session', 'room'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ CompleteSession(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ EndSession(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
            'name': '<<interface>> ISessionService',
            'methods': [
                '+ CompleteSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EndResponse>',
                '+ EndSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EndResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ CompleteSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EndResponse>',
                '+ EndSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EndResponse>',
                '- GetEffectivePausedSeconds(session: InterviewSession): int',
            ],
        },
        'end_res': {
            'name': 'EndResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Status: string',
                '+ EndedAt: DateTime?',
                '+ TerminationReason: string?',
                '+ CanGeneratePerformanceReport: bool',
                '+ CanSubmitInterviewFeedback: bool',
                '+ ShouldEndSession: bool',
                '+ Message: string',
            ],
        },
        'capability_policy': {
            'name': 'SessionCapabilityPolicy',
            'fields': [
                '+ CandidateCancelled: string',
                '+ IntegrityViolation: string',
                '+ TimeExpired: string',
            ],
            'methods': [
                '+ Evaluate(status: string, terminationReason: string?, hasSubstantiveAnswer: bool): SessionCapabilities',
            ],
        },
        'capabilities': {
            'name': 'SessionCapabilities <<DTO>>',
            'dto': True,
            'fields': [
                '+ CanGeneratePerformanceReport: bool',
                '+ CanSubmitInterviewFeedback: bool',
            ],
        },

        'irepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'repo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ InterviewRooms: DbSet<InterviewRoom>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ StartedAt: DateTime?',
                '+ EndedAt: DateTime?',
                '+ TerminationReason: string?',
                '+ DurationMinutes: int',
                '+ TotalPausedSeconds: int',
                '+ PausedAt: DateTime?',
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
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'end_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'capability_policy', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'capability_policy', 'to': 'capabilities', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..1'},
    ],
}
