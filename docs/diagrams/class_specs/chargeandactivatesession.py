"""Class spec - Charge And Activate Session [BL#54].

Owning service: InterviewAPI. Entry point:
  InterviewSessionController.GenerateQuestion  (POST .../{id}/questions/generate)
  -> SessionService.GenerateQuestionAsync

This is the single credit-deduction point in InterviewAPI (SessionService.cs:718) and
the single place StartedAt is set (:745). The atomic Initialized->InProgress transition
that makes the charge exactly-once lives in SessionRepository.UpdateStatusIfAsync
(SessionRepository.cs:250-259), so ISessionRepository lists it here.

PaymentAPI is a separate service reached over HTTP, so it appears in the sequence page
only; the C# boundary drawn here is IPaymentApiClient / PaymentApiClient. Per rule 2.3
each shared class lists only the members this flow uses.
"""

SPEC = {
    'file': 'ChargeAndActivateSession.drawio',
    'title': 'Class Diagram - Charge And Activate Session [BL#54] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api', 'guard_ex', 'credit_ex'],
        ['isvc', 'svc', 'gen_res', 'credit_cfg'],
        ['isessrepo', 'sessrepo', 'ievtrepo', 'evtrepo', 'ipay', 'pay', 'credit_res', 'db'],
        ['session', 'question', 'message', 'event'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ GenerateQuestion(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
        'credit_ex': {
            'name': 'CreditServiceUnavailableException',
            'methods': ['+ CreditServiceUnavailableException()'],
        },

        'isvc': {
            'name': '<<interface>> ISessionService',
            'methods': [
                '+ GenerateQuestionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<GenerateQuestionResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _verificationEventRepository: ISessionVerificationEventRepository',
                '- _paymentApiClient: IPaymentApiClient',
                '- _db: InterviewDbContext',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ GenerateQuestionAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<GenerateQuestionResponse>',
                '- RequireIntegrityPolicyAccepted(session: InterviewSession): void',
                '- GetIdleThresholds(difficulty: string?): (int, int)',
            ],
        },
        'gen_res': {
            'name': 'GenerateQuestionResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionQuestionId: Guid',
                '+ OrderIndex: int',
                '+ Content: string',
                '+ TotalQuestions: int',
                '+ Ended: bool',
            ],
        },
        'credit_cfg': {
            'name': 'CreditConfig',
            'fields': ['+ DurationToCreditCost: Dictionary<int, int>'],
            'methods': ['+ GetCreditCost(durationMinutes: int): int'],
        },

        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetWithQuestionsAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ UpdateStatusIfAsync(userId: Guid, sessionId: Guid, currentStatus: string, newStatus: string, ct: CancellationToken): Task<bool>',
                '+ AddChatMessageAsync(message: ChatMessage, ct: CancellationToken): Task<ChatMessage>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetWithQuestionsAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ UpdateStatusIfAsync(userId: Guid, sessionId: Guid, currentStatus: string, newStatus: string, ct: CancellationToken): Task<bool>',
                '+ AddChatMessageAsync(message: ChatMessage, ct: CancellationToken): Task<ChatMessage>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
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
        'ipay': {
            'name': '<<interface>> IPaymentApiClient',
            'methods': [
                '+ DeductCreditAsync(userId: Guid, amount: int, sessionId: Guid, description: string, ct: CancellationToken): Task<CreditDeductionResponse>',
                '+ RefundCreditAsync(userId: Guid, amount: int, sessionId: Guid, description: string, ct: CancellationToken): Task<CreditDeductionResponse>',
            ],
        },
        'pay': {
            'name': 'PaymentApiClient (InterviewAPI)',
            'fields': [
                '- _httpClient: HttpClient',
                '- _logger: ILogger<PaymentApiClient>',
            ],
            'methods': [
                '+ DeductCreditAsync(userId: Guid, amount: int, sessionId: Guid, description: string, ct: CancellationToken): Task<CreditDeductionResponse>',
                '+ RefundCreditAsync(userId: Guid, amount: int, sessionId: Guid, description: string, ct: CancellationToken): Task<CreditDeductionResponse>',
            ],
        },
        'credit_res': {
            'name': 'CreditDeductionResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Success: bool',
                '+ BalanceAfter: int',
                '+ Message: string?',
                '+ ServiceAvailable: bool',
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
                '+ DurationMinutes: int',
                '+ StartedAt: DateTime?',
                '+ CreditCost: int',
                '+ IntegrityPolicyAccepted: bool',
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
            ],
        },
        'message': {
            'name': 'ChatMessage <<entity>>',
            'fields': [
                '+ MessageId: Guid',
                '+ SessionQuestionId: Guid',
                '+ SenderType: string',
                '+ MessageType: string',
                '+ SentAt: DateTime',
            ],
        },
        'event': {
            'name': 'SessionVerificationEvent <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ Phase: string',
                '+ Modality: string',
                '+ Decision: string',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ctrl', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<catch>>'},
        {'from': 'ctrl', 'to': 'credit_ex', 'kind': 'dependency', 'label': '<<catch>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'gen_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'credit_cfg', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ievtrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ipay', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'evtrepo', 'to': 'ievtrepo', 'kind': 'realization'},
        {'from': 'pay', 'to': 'ipay', 'kind': 'realization'},
        {'from': 'pay', 'to': 'credit_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'evtrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'message', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'event', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
