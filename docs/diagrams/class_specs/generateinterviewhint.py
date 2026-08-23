"""Class spec - Generate Interview Hint [BL#44].

Owning service: InterviewAPI. Entry point:
  InterviewSessionController.Hint  (POST .../{id}/hint)
  -> SessionService.HintAsync

D2: the four ai-service boxes (HintIn, internal, GeminiJudge, config) are Python,
not C# classes, so section 1 excludes them - cross-service communication belongs to
the sequence diagram. InvalidOperationException is a BCL type, also excluded.
JwtDelegatingHandler and DelegatingHandler are WebClient infrastructure and a
framework base class; InterviewAPI has no DelegatingHandler subclass at all
(verified: no match under source/InterviewAPI), so nothing replaces them here.
"""

SPEC = {
    'file': 'GenerateInterviewHint.drawio',
    'title': 'Class Diagram - Generate Interview Hint [BL#44] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'hint_req', 'api'],
        ['isvc', 'svc', 'hint_res', 'guard_ex'],
        ['iai', 'ai', 'ai_hint_res'],
        ['irepo', 'repo', 'db'],
        ['question', 'session'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ Hint(id: Guid, req: HintRequest, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'hint_req': {
            'name': 'HintRequest <<DTO>>',
            'dto': True,
            'fields': ['+ SessionQuestionId: Guid'],
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
                '+ HintAsync(userId: Guid, sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<HintResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ HintAsync(userId: Guid, sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<HintResponse>',
            ],
        },
        'hint_res': {
            'name': 'HintResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Hint: string'],
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

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ GetHintAsync(question: string, ct: CancellationToken): Task<string>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ GetHintAsync(question: string, ct: CancellationToken): Task<string>',
            ],
        },
        'ai_hint_res': {
            'name': 'AiHintResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Hint: string'],
        },

        'irepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetQuestionByIdAsync(sessionQuestionId: Guid, ct: CancellationToken): Task<SessionQuestion?>',
                '+ TryConsumeHintAsync(sessionQuestionId: Guid, sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ ReleaseHintAsync(sessionQuestionId: Guid, ct: CancellationToken): Task',
            ],
        },
        'repo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetQuestionByIdAsync(sessionQuestionId: Guid, ct: CancellationToken): Task<SessionQuestion?>',
                '+ TryConsumeHintAsync(sessionQuestionId: Guid, sessionId: Guid, ct: CancellationToken): Task<bool>',
                '+ ReleaseHintAsync(sessionQuestionId: Guid, ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ SessionQuestions: DbSet<SessionQuestion>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'question': {
            'name': 'SessionQuestion <<entity>>',
            'fields': [
                '+ SessionQuestionId: Guid',
                '+ SessionId: Guid',
                '+ Content: string',
                '+ HintUsedAt: DateTime?',
            ],
        },
        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': ['+ SessionId: Guid', '+ UserId: Guid'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'hint_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'hint_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'ai_hint_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
