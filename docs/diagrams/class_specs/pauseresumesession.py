"""Class spec - Pause Resume Session [BL#39, #40, #62, #65].

Owning service: InterviewAPI. Entry points:
  InterviewSessionController.Pause / Unpause  (POST .../{id}/pause | /unpause)
  -> SessionService.PauseSessionAsync / UnpauseSessionAsync
"""

SPEC = {
    'file': 'PauseResumeSession.drawio',
    'title': 'Class Diagram - Pause Resume Session [BL#39, #40, #62, #65] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'pause_res', 'unpause_res'],
        ['irepo', 'repo', 'db'],
        ['session'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ Pause(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ Unpause(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ PauseSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<PauseResponse>',
                '+ UnpauseSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<UnpauseResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- MaxPauseCount: int',
                '- MaxTotalPausedSeconds: int',
                '- _repo: ISessionRepository',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ PauseSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<PauseResponse>',
                '+ UnpauseSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<UnpauseResponse>',
                '- GetEffectivePausedSeconds(session: InterviewSession): int',
            ],
        },
        'pause_res': {
            'name': 'PauseResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Status: string',
                '+ PauseCount: int',
                '+ PausesRemaining: int',
                '+ PausedSecondsRemaining: int',
            ],
        },
        'unpause_res': {
            'name': 'UnpauseResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Status: string',
                '+ RemainingSeconds: int',
                '+ PauseCount: int',
                '+ PausesRemaining: int',
                '+ PausedSecondsRemaining: int',
            ],
        },

        'irepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'repo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': ['+ InterviewSessions: DbSet<InterviewSession>'],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ StartedAt: DateTime?',
                '+ PausedAt: DateTime?',
                '+ PauseCount: int',
                '+ TotalPausedSeconds: int',
                '+ DurationMinutes: int',
                '+ UpdatedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'pause_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'unpause_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
