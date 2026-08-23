"""Class spec - Resume Active Session.

Owning service: InterviewAPI. Two entry points reach the SAME service method:
  InterviewSessionController.GetActiveSession  (GET .../sessions/active)
    -> SessionService.GetActiveSessionAsync            (dashboard resume banner)
  InterviewSessionController.ConfigDuration    (PUT .../config/duration)
    -> SessionService.ConfigDurationAsync
    -> SessionService.ResolveSessionAsync -> GetActiveSessionAsync (409 guard)

The lookup is not read-only: it lazily auto-resumes an expired pause
(SessionRepository.TryAutoResumeExpiredPauseAsync, an atomic conditional UPDATE) and
lazily finalizes a session whose time ran out, closing its room. Both writes are listed
on ISessionRepository because both are reachable from this flow.

No backlog row covers GET .../sessions/active - the endpoint exists in code and has a
real UI entry point, but the backlog never captured it. See FLOWS/ResumeActiveSession.md.
"""

SPEC = {
    'file': 'ResumeActiveSession.drawio',
    'title': 'Class Diagram - Resume Active Session - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api', 'guard_ex'],
        ['isvc', 'svc', 'active_res'],
        ['isessrepo', 'sessrepo', 'db'],
        ['session', 'room', 'message'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ GetActiveSession(cancellationToken: CancellationToken): Task<IActionResult>',
                '+ ConfigDuration(req: ConfigDurationRequest, cancellationToken: CancellationToken): Task<IActionResult>',
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
        'guard_ex': {
            'name': 'SessionGuardException',
            'fields': [
                '+ StatusCode: int',
                '+ ErrorCode: string',
                '+ ActiveSessionId: Guid?',
            ],
        },

        'isvc': {
            'name': '<<interface>> ISessionService',
            'methods': [
                '+ GetActiveSessionAsync(userId: Guid, ct: CancellationToken): Task<ActiveSessionResponse?>',
                '+ ConfigDurationAsync(userId: Guid, req: ConfigDurationRequest, ct: CancellationToken): Task<ConfigResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _logger: ILogger<SessionService>',
                '- MaxTotalPausedSeconds: int',
            ],
            'methods': [
                '+ GetActiveSessionAsync(userId: Guid, ct: CancellationToken): Task<ActiveSessionResponse?>',
                '+ ConfigDurationAsync(userId: Guid, req: ConfigDurationRequest, ct: CancellationToken): Task<ConfigResponse>',
                '- ResolveSessionAsync(sessionId: Guid?, userId: Guid, ct: CancellationToken, factory: Func<InterviewSession>): Task<InterviewSession>',
                '- AutoResumeIfPauseExpiredAsync(session: InterviewSession, ct: CancellationToken): Task',
                '- FinalizeIfTimeExpiredAsync(session: InterviewSession, ct: CancellationToken): Task<bool>',
                '- GetEffectivePausedSeconds(session: InterviewSession): int',
            ],
        },
        'active_res': {
            'name': 'ActiveSessionResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ Status: string',
                '+ StartedAt: DateTime?',
                '+ DurationMinutes: int',
                '+ RemainingSeconds: int',
            ],
        },

        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetActiveSessionAsync(userId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ TryAutoResumeExpiredPauseAsync(sessionId: Guid, expectedPausedAt: DateTime, newTotalPausedSeconds: int, ct: CancellationToken): Task<bool>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ CreateAsync(session: InterviewSession, ct: CancellationToken): Task<InterviewSession>',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': [
                '- _db: InterviewDbContext',
                '- _logger: ILogger<SessionRepository>',
            ],
            'methods': [
                '+ GetActiveSessionAsync(userId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ TryAutoResumeExpiredPauseAsync(sessionId: Guid, expectedPausedAt: DateTime, newTotalPausedSeconds: int, ct: CancellationToken): Task<bool>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ CreateAsync(session: InterviewSession, ct: CancellationToken): Task<InterviewSession>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ InterviewRooms: DbSet<InterviewRoom>',
                '+ ChatMessages: DbSet<ChatMessage>',
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
                '+ PausedAt: DateTime?',
                '+ TotalPausedSeconds: int',
                '+ DurationMinutes: int',
                '+ EndedAt: DateTime?',
                '+ TerminationReason: string?',
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
        'message': {
            'name': 'ChatMessage <<entity>>',
            'fields': [
                '+ MessageId: Guid',
                '+ SessionQuestionId: Guid',
                '+ SenderType: string',
                '+ MessageType: string',
                '+ AnswerReason: string?',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ctrl', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<catch>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'active_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'message', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
