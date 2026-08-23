"""Class spec - Evaluate Interview Answer [BL#73, #75, #70].

ANCHOR SERVICE: EvaluationAPI.

Q1 verification: the request arrives at EvaluationAPI directly. InterviewAPI's
grading worker POSTs api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId}
through its own typed client (InterviewAPI/Clients/EvaluationApiClient.cs:27,
BaseAddress set in InterviewAPI/Program.cs:117) - it is not proxied by any
InterviewAPI endpoint. The InterviewAPI half of that handoff (IGradingQueue,
BoundedGradingQueue, GradingWorkItem) is documented on ConductInterviewQA.

Exceptions drawn are exactly those thrown on EvaluateAnswerAsync (lines 47-171):
  EvaluationRequestException      EvaluationService.cs:58
  EvaluationPersistenceException  EvaluationService.cs:77, :161
  EvaluationDependencyException   EvaluationService.cs:116, :125
EvaluationLifecycleStates is NOT drawn: it is only used on the report path.
"""

SPEC = {
    'file': 'EvaluateInterviewAnswer.drawio',
    'title': 'Class Diagram - Evaluate Interview Answer [BL#73, #75, #70] - Back-end scope (EvaluationAPI)',

    'columns': [
        ['ctrl', 'answer_req', 'api'],
        ['isvc', 'svc', 'answer_res', 'request_ex', 'dependency_ex', 'persistence_ex'],
        ['iai', 'ai', 'ai_req', 'ai_res', 'iinterview', 'interview_client', 'eligibility_res'],
        ['irepo', 'repo', 'db'],
        ['evaluation'],
    ],

    'classes': {
        'ctrl': {
            'name': 'EvaluationController (EvaluationAPI)',
            'fields': [
                '- _service: IEvaluationService',
                '- _logger: ILogger<EvaluationController>',
            ],
            'methods': [
                '+ EvaluateAnswer(id: Guid, answerId: Guid, req: EvaluateAnswerRequest, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'answer_req': {
            'name': 'EvaluateAnswerRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ Question: string',
                '+ ReferenceAnswer: string',
                '+ Category: string',
                '+ Difficulty: string',
                '+ UserAnswer: string',
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
            'name': '<<interface>> IEvaluationService',
            'methods': [
                '+ EvaluateAnswerAsync(sessionId: Guid, answerId: Guid, req: EvaluateAnswerRequest, ct: CancellationToken): Task<EvaluateAnswerResponse>',
            ],
        },
        'svc': {
            'name': 'EvaluationService (EvaluationAPI)',
            'fields': [
                '- _repo: IEvaluationRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _interviewApiClient: IInterviewApiClient',
                '- _logger: ILogger<EvaluationService>',
            ],
            'methods': [
                '+ EvaluateAnswerAsync(sessionId: Guid, answerId: Guid, req: EvaluateAnswerRequest, ct: CancellationToken): Task<EvaluateAnswerResponse>',
            ],
        },
        'answer_res': {
            'name': 'EvaluateAnswerResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ EvaluationId: Guid',
                '+ SessionId: Guid',
                '+ SessionQuestionId: Guid',
                '+ Score: decimal',
                '+ CriteriaScoresJson: string?',
                '+ Strengths: string?',
                '+ AiFeedback: string?',
                '+ AreasToImprove: string?',
                '+ ModelUsed: string?',
            ],
        },
        'request_ex': {
            'name': 'EvaluationRequestException',
            'methods': ['+ EvaluationRequestException()'],
        },
        'dependency_ex': {
            'name': 'EvaluationDependencyException',
            'methods': [
                '+ EvaluationDependencyException()',
                '+ EvaluationDependencyException(inner: Exception)',
            ],
        },
        'persistence_ex': {
            'name': 'EvaluationPersistenceException',
            'methods': ['+ EvaluationPersistenceException(inner: Exception)'],
        },

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ EvaluateAnswerAsync(request: AiEvaluateAnswerRequest, ct: CancellationToken): Task<AiEvaluateAnswerResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (EvaluationAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ EvaluateAnswerAsync(request: AiEvaluateAnswerRequest, ct: CancellationToken): Task<AiEvaluateAnswerResponse>',
            ],
        },
        'ai_req': {
            'name': 'AiEvaluateAnswerRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ Question: string',
                '+ ReferenceAnswer: string',
                '+ Category: string',
                '+ Difficulty: string',
                '+ UserAnswer: string',
                '+ Followups: List<object>',
            ],
        },
        'ai_res': {
            'name': 'AiEvaluateAnswerResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Score: double?',
                '+ Criteria: Dictionary<string, double>?',
                '+ Analysis: string',
                '+ SuggestedImprovement: string',
                '+ Praise: string',
                '+ ModelUsed: string',
            ],
        },
        'iinterview': {
            'name': '<<interface>> IInterviewApiClient',
            'methods': [
                '+ GetSessionFeedbackEligibilityAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionFeedbackEligibilityResult>',
            ],
        },
        'interview_client': {
            'name': 'InterviewApiClient (EvaluationAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<InterviewApiClient>'],
            'methods': [
                '+ GetSessionFeedbackEligibilityAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionFeedbackEligibilityResult>',
            ],
        },
        'eligibility_res': {
            'name': 'SessionFeedbackEligibilityResult <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ HasSubstantiveAnswer: bool',
                '+ CanSubmitInterviewFeedback: bool',
                '+ CanGeneratePerformanceReport: bool',
                '+ Eligible: bool',
            ],
        },

        'irepo': {
            'name': '<<interface>> IEvaluationRepository',
            'methods': [
                '+ AddEvaluationAsync(eval: AnswerEvaluation, ct: CancellationToken): Task<AnswerEvaluation>',
                '+ GetBySessionQuestionAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<AnswerEvaluation?>',
            ],
        },
        'repo': {
            'name': 'EvaluationRepository (EvaluationAPI)',
            'fields': ['- _db: EvaluationDbContext'],
            'methods': [
                '+ AddEvaluationAsync(eval: AnswerEvaluation, ct: CancellationToken): Task<AnswerEvaluation>',
                '+ GetBySessionQuestionAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<AnswerEvaluation?>',
            ],
        },
        'db': {
            'name': 'EvaluationDbContext',
            'fields': ['+ AnswerEvaluations: DbSet<AnswerEvaluation>'],
            'methods': ['+ EvaluationDbContext(options: DbContextOptions<EvaluationDbContext>)'],
        },

        'evaluation': {
            'name': 'AnswerEvaluation <<entity>>',
            'fields': [
                '+ EvaluationId: Guid',
                '+ SessionId: Guid',
                '+ SessionQuestionId: Guid',
                '+ QuestionContent: string?',
                '+ UserAnswer: string?',
                '+ Score: decimal',
                '+ CriteriaScoresJson: string?',
                '+ Strengths: string?',
                '+ Weaknesses: string?',
                '+ AiFeedback: string?',
                '+ ModelUsed: string?',
                '+ GeneratedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'answer_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'answer_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'request_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'dependency_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'persistence_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iinterview', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'evaluation', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'ai_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'ai_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'interview_client', 'to': 'iinterview', 'kind': 'realization'},
        {'from': 'interview_client', 'to': 'eligibility_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'evaluation', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
