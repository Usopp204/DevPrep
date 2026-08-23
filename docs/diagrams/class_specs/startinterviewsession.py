"""Class spec - Start Interview Session [BL#55, #45, #46].

Owning service: InterviewAPI. Entry point:
  InterviewSessionController.Init  (POST .../init)
  -> SessionService.InitAsync

Plans the question set via ai-service, persists SessionQuestions and the room.
"""

SPEC = {
    'file': 'StartInterviewSession.drawio',
    'title': 'Class Diagram - Start Interview Session [BL#55, #45, #46] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'init_req', 'api'],
        ['isvc', 'svc', 'init_res', 'guard_ex'],
        ['iai', 'ai', 'plan_req', 'plan_res', 'planned_q'],
        ['isessrepo', 'sessrepo', 'ibiorepo', 'biorepo', 'db'],
        ['session', 'question', 'room', 'biometric'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _service: ISessionService',
                '- _logger: ILogger<InterviewSessionController>',
            ],
            'methods': [
                '+ Init(req: InitRequest, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'init_req': {
            'name': 'InitRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ Position: string?',
                '+ Jd: string?',
                '+ JdId: Guid?',
                '+ FocusAreas: List<string>?',
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
                '+ InitAsync(userId: Guid, req: InitRequest, ct: CancellationToken): Task<InitResponse>',
            ],
        },
        'svc': {
            'name': 'SessionService (InterviewAPI)',
            'fields': [
                '- _repo: ISessionRepository',
                '- _biometricsRepository: IProfileBiometricsRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _configuration: IConfiguration',
                '- _logger: ILogger<SessionService>',
            ],
            'methods': [
                '+ InitAsync(userId: Guid, req: InitRequest, ct: CancellationToken): Task<InitResponse>',
            ],
        },
        'init_res': {
            'name': 'InitResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ TotalQuestions: int',
                '+ RoomId: Guid',
                '+ FirstQuestion: string',
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

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ PlanQuestionsAsync(request: AiQuestionPlanRequest, ct: CancellationToken): Task<AiQuestionPlanResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ PlanQuestionsAsync(request: AiQuestionPlanRequest, ct: CancellationToken): Task<AiQuestionPlanResponse>',
            ],
        },
        'plan_req': {
            'name': 'AiQuestionPlanRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ Position: string',
                '+ Level: string',
                '+ FocusAreas: List<string>',
                '+ DurationMin: int',
                '+ CvText: string?',
                '+ JdText: string?',
            ],
        },
        'plan_res': {
            'name': 'AiQuestionPlanResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Questions: List<AiPlannedQuestion>'],
        },
        'planned_q': {
            'name': 'AiPlannedQuestion <<DTO>>',
            'dto': True,
            'fields': [
                '+ OrderIndex: int',
                '+ Question: string',
                '+ ReferenceAnswer: string',
                '+ Category: string',
                '+ Difficulty: string',
            ],
        },

        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ AddSessionQuestionsAsync(questions: List<SessionQuestion>, ct: CancellationToken): Task',
                '+ CreateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task<InterviewRoom>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
                '+ AddSessionQuestionsAsync(questions: List<SessionQuestion>, ct: CancellationToken): Task',
                '+ CreateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task<InterviewRoom>',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
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
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ SessionQuestions: DbSet<SessionQuestion>',
                '+ InterviewRooms: DbSet<InterviewRoom>',
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': [
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Status: string',
                '+ Position: string?',
                '+ Level: string',
                '+ DurationMinutes: int',
                '+ TotalQuestions: int',
                '+ JdId: Guid?',
                '+ UpdatedAt: DateTime',
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
                '+ IsAiGenerated: bool',
                '+ ContextSource: string?',
            ],
        },
        'room': {
            'name': 'InterviewRoom <<entity>>',
            'fields': [
                '+ RoomId: Guid',
                '+ SessionId: Guid',
                '+ ChannelName: string',
                '+ Status: string',
                '+ InitializedAt: DateTime',
            ],
        },
        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': ['+ UserId: Guid', '+ Modality: string', '+ ModelVersion: string'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'init_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'init_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'guard_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'plan_req', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'plan_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'plan_res', 'to': 'planned_q', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'question', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'session', 'to': 'room', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..1'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
