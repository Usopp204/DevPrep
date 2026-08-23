"""Class spec - Accept Integrity Policy [BL#63, #64].

Owning service: InterviewAPI. Entry points:
  InterviewSessionController.GetIntegrityPolicyStatus / AcceptIntegrityPolicy
  -> SessionService.GetIntegrityPolicyStatusAsync / AcceptIntegrityPolicyAsync
"""

SPEC = {
    'file': 'AcceptIntegrityPolicy.drawio',
    'title': 'Class Diagram - Accept Integrity Policy [BL#63, #64] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'policy_res'],
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
                '+ GetIntegrityPolicyStatus(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ AcceptIntegrityPolicy(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ GetIntegrityPolicyStatusAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<IntegrityPolicyStatusResponse>',
                '+ AcceptIntegrityPolicyAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<IntegrityPolicyStatusResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ GetIntegrityPolicyStatusAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<IntegrityPolicyStatusResponse>',
                '+ AcceptIntegrityPolicyAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<IntegrityPolicyStatusResponse>',
            ],
        },
        'policy_res': {
            'name': 'IntegrityPolicyStatusResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Accepted: bool', '+ AcceptedAt: DateTime?'],
        },

        'irepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ AcceptIntegrityPolicyIfAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
            ],
        },
        'repo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ AcceptIntegrityPolicyIfAsync(sessionId: Guid, ct: CancellationToken): Task<bool>',
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
                '+ IntegrityPolicyAccepted: bool',
                '+ IntegrityPolicyAcceptedAt: DateTime?',
                '+ UpdatedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'policy_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
