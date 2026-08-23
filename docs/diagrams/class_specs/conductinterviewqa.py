"""Class spec - Conduct Interview QA [BL#56, #57, #47, #59, #60].

Owning service: InterviewAPI. Entry point:
  InterviewSessionController.SendMessage  (POST .../{id}/messages)
  -> SessionService.SendMessageAsync

Q1 - grading dispatch. The previous diagram showed a private
SessionService.ScheduleGrading(...) that does not exist anywhere in the source.
The real dispatch is a bounded in-process queue:
  SessionService._gradingQueue        SessionService.cs:197
  _gradingQueue.EnqueueAsync(item)    SessionService.cs:952
  new GradingWorkItem(...)            SessionService.cs:936-946
  IGradingQueue                       Services/Grading/IGradingQueue.cs
  BoundedGradingQueue : IGradingQueue Services/Grading/BoundedGradingQueue.cs
IEvaluationApiClient is NOT here: it is held by GradingBackgroundService, which
consumes the queue. That hop belongs to the EvaluateInterviewAnswer diagram.
"""

SPEC = {
    'file': 'ConductInterviewQA.drawio',
    'title': 'Class Diagram - Conduct Interview QA [BL#56, #57, #47, #59, #60] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'msg_req', 'api'],
        ['isvc', 'svc', 'msg_res', 'next_q', 'guard_ex', 'moderation', 'substantive'],
        ['iqueue', 'queue', 'work_item', 'payload'],
        ['isessrepo', 'sessrepo', 'ievtrepo', 'evtrepo', 'db'],
        ['session', 'question', 'chat', 'event'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ SendMessage(id: Guid, req: SendMessageRequest, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'msg_req': {
            'name': 'SendMessageRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionQuestionId: Guid',
                '+ Content: string',
                '+ ResponseTimeMs: int?',
                '+ Reason: string?',
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
                '+ SendMessageAsync(userId: Guid, sessionId: Guid, req: SendMessageRequest, ct: CancellationToken): Task<MessageResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _verificationEventRepository: ISessionVerificationEventRepository',
                '- _gradingQueue: IGradingQueue',
                '- _httpContextAccessor: IHttpContextAccessor',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ SendMessageAsync(userId: Guid, sessionId: Guid, req: SendMessageRequest, ct: CancellationToken): Task<MessageResponse>',
                '- BuildCurrentTurnResponseAsync(sessionId: Guid, afterOrderIndex: int, ct: CancellationToken): Task<MessageResponse>',
                '- AttachVerificationCheckAsync(question: NextQuestionDto, sessionId: Guid, userId: Guid, ct: CancellationToken): Task',
            ],
        },
        'msg_res': {
            'name': 'MessageResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Ended: bool',
                '+ /ShouldEndSession: bool',
                '+ Message: string',
                '+ NextQuestion: NextQuestionDto?',
                '+ AnsweredQuestions: int',
                '+ TotalQuestions: int',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ CanGeneratePerformanceReport: bool',
                '+ CanSubmitInterviewFeedback: bool',
            ],
        },
        'next_q': {
            'name': 'NextQuestionDto <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionQuestionId: Guid',
                '+ OrderIndex: int',
                '+ Content: string',
                '+ IdleReminderSeconds: int',
                '+ IdleHardSeconds: int',
                '+ IdleElapsedSeconds: int',
                '+ VerificationCheckId: Guid?',
                '+ VerificationCheckExpiresAt: DateTime?',
            ],
        },
        'guard_ex': {
            'name': 'SessionGuardException',
            'fields': [
                '+ StatusCode: int',
                '+ ErrorCode: string',
                '+ RetryAfterSeconds: int?',
                '+ ActiveSessionId: Guid?',
            ],
            'methods': [
                '+ SessionGuardException(statusCode: int, errorCode: string, message: string, retryAfterSeconds: int?, activeSessionId: Guid?)',
            ],
        },
        'moderation': {
            'name': 'ContentModerationHelper',
            'methods': [
                '+ IsAcceptable(content: string): bool',
                '+ ContainsProfanity(content: string): bool',
                '+ LooksLikeGibberish(content: string): bool',
            ],
        },
        'substantive': {
            'name': 'SubstantiveAnswerRules',
            'fields': [
                '+ UserSenderType: string',
                '+ AnswerMessageType: string',
                '+ SkippedReason: string',
                '+ TimedOutReason: string',
            ],
            'methods': [
                '+ IsSubstantive(message: ChatMessage): bool',
                '+ HasSubstantiveContent(content: string?): bool',
            ],
        },

        'iqueue': {
            'name': '<<interface>> IGradingQueue',
            'fields': ['+ PendingCount: int', '+ IsAccepting: bool'],
            'methods': [
                '+ EnqueueAsync(workItem: GradingWorkItem): ValueTask',
                '+ TryEnqueue(workItem: GradingWorkItem): bool',
                '+ ReadAllAsync(cancellationToken: CancellationToken): IAsyncEnumerable<GradingWorkItem>',
                '+ Complete(): void',
            ],
        },
        'queue': {
            'name': 'BoundedGradingQueue (InterviewAPI)',
            'fields': [
                '- _channel: Channel<GradingWorkItem>',
                '- _logger: ILogger<BoundedGradingQueue>',
                '+ /PendingCount: int',
                '+ /IsAccepting: bool',
            ],
            'methods': [
                '+ EnqueueAsync(workItem: GradingWorkItem): ValueTask',
                '+ TryEnqueue(workItem: GradingWorkItem): bool',
                '+ ReadAllAsync(cancellationToken: CancellationToken): IAsyncEnumerable<GradingWorkItem>',
                '+ Complete(): void',
            ],
        },
        'work_item': {
            'name': 'GradingWorkItem <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ SessionQuestionId: Guid',
                '+ ChatMessageId: Guid',
                '+ UserId: Guid',
                '+ Payload: EvaluateAnswerPayload',
                '+ BearerToken: string?',
            ],
        },
        'payload': {
            'name': 'EvaluateAnswerPayload <<DTO>>',
            'dto': True,
            'fields': [
                '+ Question: string',
                '+ ReferenceAnswer: string',
                '+ Category: string',
                '+ Difficulty: string',
                '+ UserAnswer: string',
            ],
        },

        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetWithQuestionsAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetQuestionByIdAsync(sessionQuestionId: Guid, ct: CancellationToken): Task<SessionQuestion?>',
                '+ AddChatMessageAsync(message: ChatMessage, ct: CancellationToken): Task<ChatMessage>',
                '+ IncrementAnsweredIfAsync(sessionId: Guid, expectedCurrentCount: int, ct: CancellationToken): Task<bool>',
                '+ TryConsumeSkipCooldownAsync(sessionId: Guid, cooldownSeconds: int, ct: CancellationToken): Task<(bool Allowed, int RetryAfterSeconds)>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetWithQuestionsAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetQuestionByIdAsync(sessionQuestionId: Guid, ct: CancellationToken): Task<SessionQuestion?>',
                '+ AddChatMessageAsync(message: ChatMessage, ct: CancellationToken): Task<ChatMessage>',
                '+ IncrementAnsweredIfAsync(sessionId: Guid, expectedCurrentCount: int, ct: CancellationToken): Task<bool>',
                '+ TryConsumeSkipCooldownAsync(sessionId: Guid, cooldownSeconds: int, ct: CancellationToken): Task<(bool Allowed, int RetryAfterSeconds)>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'ievtrepo': {
            'name': '<<interface>> ISessionVerificationEventRepository',
            'methods': [
                '+ GetQuestionCheckAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>',
                '+ TryAddIncidentAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task<bool>',
                '+ ListExpiredPendingChecksAsync(sessionId: Guid, now: DateTime, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryMarkCheckMissedAsync(eventId: long, ct: CancellationToken): Task<bool>',
            ],
        },
        'evtrepo': {
            'name': 'SessionVerificationEventRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetQuestionCheckAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>',
                '+ TryAddIncidentAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task<bool>',
                '+ ListExpiredPendingChecksAsync(sessionId: Guid, now: DateTime, ct: CancellationToken): Task<List<SessionVerificationEvent>>',
                '+ TryMarkCheckMissedAsync(eventId: long, ct: CancellationToken): Task<bool>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ SessionQuestions: DbSet<SessionQuestion>',
                '+ ChatMessages: DbSet<ChatMessage>',
                '+ SessionVerificationEvents: DbSet<SessionVerificationEvent>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ TotalQuestions: int',
                '+ AnsweredQuestions: int',
                '+ LastSkipAtUtc: DateTime?',
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
                '+ ReferenceAnswer: string?',
                '+ AskedAt: DateTime?',
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
                '+ AnswerReason: string?',
                '+ ResponseTimeMs: int?',
                '+ SentAt: DateTime',
            ],
        },
        'event': {
            'name': 'SessionVerificationEvent <<entity>>',
            'fields': [
                '+ Id: long',
                '+ SessionId: Guid',
                '+ SessionQuestionId: Guid?',
                '+ IncidentId: Guid?',
                '+ Decision: string',
                '+ ExpiresAt: DateTime?',
                '+ ConsumedAt: DateTime?',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'msg_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'msg_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'next_q', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'moderation', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'substantive', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iqueue', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'work_item', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'queue', 'to': 'iqueue', 'kind': 'realization'},
        {'from': 'work_item', 'to': 'payload', 'kind': 'association', 'mult_src': '1', 'mult_dst': '1'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'question', 'to': 'chat', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'question', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
