"""Class spec - Enter Interview Room.

Owning service: InterviewAPI. Two entry points remain in this file's back-end scope:
  InterviewSessionController.ResumeSession    (GET .../sessions/{id}/resume)
    -> SessionService.ResumeSessionAsync         - backs WebClient GetRoomAsync (message 3)
  InterviewSessionController.MarkRoomTourSeen (POST api/interview/room-tour/seen)
    -> SessionService.MarkRoomTourSeenAsync      - Phase 3, drawn in full (V.1-V.12)

RESCOPED after Phase 2 was extracted to #20 ChargeAndActivateSession. This page previously
described GenerateQuestionAsync end to end - the controller action, the service method, the
credit client and the payment DTOs - none of which this file's sequence page draws any more.
Everything specific to the charge now lives in #20 and was removed here.

Three flows the sequence page reaches are cross-referenced out and are therefore NOT drawn
on this page: the entry gate (#14), the integrity policy (#11) and generate/charge (#20).

ResumeSessionAsync is kept even though the sequence page compresses it to one WebClient-level
call+return (message 3 -> 4): GET .../resume has no diagram of its own, so this is the only
class page in the set that represents it. Its private helpers pull in the pause auto-resume,
the lazy finalize and the expired-check settlement, so the repositories they use are listed.
"""

SPEC = {
    'file': 'EnterInterviewRoom.drawio',
    'title': 'Class Diagram - Enter Interview Room - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api', 'guard_ex'],
        ['isvc', 'svc', 'resume_res'],
        ['isessrepo', 'sessrepo', 'ievtrepo', 'evtrepo', 'db'],
        ['session', 'question', 'chat', 'room', 'tour'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ ResumeSession(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ MarkRoomTourSeen(cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ RetryAfterSeconds: int?',
            ],
        },

        'isvc': {
            'name': '<<interface>> ISessionService',
            'methods': [
                '+ ResumeSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<ResumeResponse>',
                '+ MarkRoomTourSeenAsync(userId: Guid, ct: CancellationToken): Task',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _verificationEventRepository: ISessionVerificationEventRepository',
                '- _db: InterviewDbContext',
                '- _configuration: IConfiguration',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ ResumeSessionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<ResumeResponse>',
                '+ MarkRoomTourSeenAsync(userId: Guid, ct: CancellationToken): Task',
                '- AutoResumeIfPauseExpiredAsync(session: InterviewSession, ct: CancellationToken): Task',
                '- FinalizeIfTimeExpiredAsync(session: InterviewSession, ct: CancellationToken): Task<bool>',
                '- SettleExpiredVerificationChecksAsync(session: InterviewSession, ct: CancellationToken): Task',
                '- GetEffectivePausedSeconds(session: InterviewSession): int',
            ],
        },
        'resume_res': {
            'name': 'ResumeResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Status: string',
                '+ Position: string?',
                '+ Level: string',
                '+ RemainingSeconds: int',
                '+ CurrentQuestion: NextQuestionDto?',
                '+ TotalQuestions: int',
                '+ AnsweredQuestions: int',
                '+ PausesRemaining: int',
                '+ HasSeenRoomTour: bool',
            ],
        },

        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetWithFullHistoryAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ TryAutoResumeExpiredPauseAsync(sessionId: Guid, expectedPausedAt: DateTime, newTotalPausedSeconds: int, ct: CancellationToken): Task<bool>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetWithFullHistoryAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ TryAutoResumeExpiredPauseAsync(sessionId: Guid, expectedPausedAt: DateTime, newTotalPausedSeconds: int, ct: CancellationToken): Task<bool>',
                '+ HasSubstantiveAnswerAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>',
                '+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'ievtrepo': {
            'name': '<<interface>> ISessionVerificationEventRepository',
            'methods': [
                '+ ListExpiredPendingChecksAsync(sessionId: Guid, now: DateTime, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryMarkCheckMissedAsync(eventId: long, ct: CancellationToken): Task<bool>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ ListExpiredPendingChecksAsync(sessionId: Guid, now: DateTime, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryMarkCheckMissedAsync(eventId: long, ct: CancellationToken): Task<bool>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ InterviewRooms: DbSet<InterviewRoom>',
                '+ ChatMessages: DbSet<ChatMessage>',
                '+ UserRoomTourStatuses: DbSet<UserRoomTourStatus>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ DurationMinutes: int',
                '+ StartedAt: DateTime?',
                '+ PausedAt: DateTime?',
                '+ TotalPausedSeconds: int',
                '+ TotalQuestions: int',
                '+ AnsweredQuestions: int',
                '+ SessionQuestions: ICollection<SessionQuestion>',
            ],
        },
        'question': {
            'name': 'SessionQuestion <<entity>>',
            'fields': [
                '+ SessionQuestionId: Guid',
                '+ SessionId: Guid',
                '+ OrderIndex: int',
                '+ Content: string',
                '+ AskedAt: DateTime?',
                '+ ChatMessages: ICollection<ChatMessage>',
            ],
        },
        'chat': {
            'name': 'ChatMessage <<entity>>',
            'fields': [
                '+ MessageId: Guid',
                '+ SessionQuestionId: Guid',
                '+ SenderType: string',
                '+ MessageType: string',
                '+ Content: string',
                '+ SentAt: DateTime',
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
        'tour': {
            'name': 'UserRoomTourStatus <<entity>>',
            'fields': [
                '+ UserId: Guid',
                '+ HasSeenRoomTour: bool',
                '+ UpdatedAt: DateTime?',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ctrl', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<catch>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'resume_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'chat', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'tour', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'question', 'to': 'chat', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
