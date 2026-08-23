"""Class spec - View Evaluation Report [BL#74, #76, #68, #69].

ANCHOR SERVICE: EvaluationAPI.

Q1 verification: the browser calls EvaluationAPI directly through the gateway -
route /api/evaluation/{**catch-all} -> evaluation-cluster -> http://localhost:7006/
(ApiGateway/appsettings.json:88-94, :400-405), and EvaluationController is mounted
at api/evaluation/sessions (EvaluationController.cs:14). Nothing is proxied through
InterviewAPI. EvaluationAPI reaches back to InterviewAPI through its own outbound
client, IInterviewApiClient, which section 1 permits.

Exceptions drawn are exactly those thrown on the report path:
  SessionNotEligibleForPerformanceReportException  EvaluationService.cs:242, :249, :334, :461
  InterviewApiDependencyException                  EvaluationService.cs:256
  AggregateReportNotReadyException                 EvaluationService.cs:350, :381
  EvaluationDependencyException                    EvaluationService.cs:424

RequestRegradeIfNeededAsync IS on this path (called at EvaluationService.cs:289
and :296 inside GetPerformanceReportAsync) and is drawn, together with
IInterviewApiClient.RequestRegradeAsync - the lost-grading recovery is a real part
of this design and was invisible on the previous diagram.
"""

SPEC = {
    'file': 'ViewEvaluationReport.drawio',
    'title': 'Class Diagram - View Evaluation Report [BL#74, #76, #68, #69] - Back-end scope (EvaluationAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'report_res', 'lifecycle', 'noteligible_ex', 'notready_ex', 'dependency_ex'],
        ['iinterview', 'interview_client', 'eligibility_res', 'context_res', 'interview_dep_ex'],
        ['iai', 'ai', 'ai_req', 'ai_res'],
        ['irepo', 'repo', 'db'],
        ['report', 'metric', 'evaluation'],
    ],

    'classes': {
        'ctrl': {
            'name': 'EvaluationController (EvaluationAPI)',
            'fields': [
                '- _service: IEvaluationService',
                '- _logger: ILogger<EvaluationController>',
            ],
            'methods': [
                '+ GetPerformance(sessionId: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ EnsureReport(sessionId: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ GetAnswerEvaluations(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>',
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
                '+ GetPerformanceReportAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<PerformanceReportResponse>',
                '+ EnsurePerformanceReportAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<PerformanceReportResponse>',
                '+ GetAnswerEvaluationsAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<List<EvaluateAnswerResponse>>',
            ],
        },
        'svc': {
            'name': 'EvaluationService (EvaluationAPI)',
            'fields': [
                '- _regradeAttemptedThisRequest: HashSet<Guid>',
                '- _repo: IEvaluationRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _interviewApiClient: IInterviewApiClient',
                '- _logger: ILogger<EvaluationService>',
            ],
            'methods': [
                '+ GetPerformanceReportAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<PerformanceReportResponse>',
                '+ EnsurePerformanceReportAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<PerformanceReportResponse>',
                '+ GetAnswerEvaluationsAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<List<EvaluateAnswerResponse>>',
                '- RequestRegradeIfNeededAsync(sessionId: Guid, substantiveQuestionIds: HashSet<Guid>, coveredEvaluations: IReadOnlyCollection<AnswerEvaluation>, bearerToken: string?, ct: CancellationToken): Task',
                '- StateOnly(sessionId: Guid, state: string): PerformanceReportResponse',
            ],
        },
        'report_res': {
            'name': 'PerformanceReportResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ State: string',
                '+ SessionId: Guid',
                '+ HasPersistedReport: bool',
                '+ OverallScore: decimal?',
                '+ TechnicalScore: decimal?',
                '+ CommunicationScore: decimal?',
                '+ ProblemSolvingScore: decimal?',
                '+ Summary: string?',
                '+ Strengths: List<string>',
                '+ Improvements: List<string>',
                '+ Recommendations: string?',
                '+ Grade: string?',
            ],
        },
        'lifecycle': {
            'name': 'EvaluationLifecycleStates',
            'fields': [
                '+ Scored: string',
                '+ EvaluationUnavailable: string',
                '+ EvaluationIncomplete: string',
                '+ NoEvaluatedAnswers: string',
                '+ ExpiredNoResult: string',
            ],
        },
        'noteligible_ex': {
            'name': 'SessionNotEligibleForPerformanceReportException',
            'methods': ['+ SessionNotEligibleForPerformanceReportException(status: string)'],
        },
        'notready_ex': {
            'name': 'AggregateReportNotReadyException',
            'methods': ['+ AggregateReportNotReadyException(state: string)'],
        },
        'dependency_ex': {
            'name': 'EvaluationDependencyException',
            'methods': ['+ EvaluationDependencyException(inner: Exception)'],
        },

        'iinterview': {
            'name': '<<interface>> IInterviewApiClient',
            'methods': [
                '+ GetSessionFeedbackEligibilityAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionFeedbackEligibilityResult>',
                '+ GetSessionEvaluationContextAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionEvaluationContextResult>',
                '+ RequestRegradeAsync(sessionId: Guid, missingSessionQuestionIds: IReadOnlyCollection<Guid>, bearerToken: string?, ct: CancellationToken): Task',
            ],
        },
        'interview_client': {
            'name': 'InterviewApiClient (EvaluationAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<InterviewApiClient>'],
            'methods': [
                '+ GetSessionFeedbackEligibilityAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionFeedbackEligibilityResult>',
                '+ GetSessionEvaluationContextAsync(sessionId: Guid, bearerToken: string?, ct: CancellationToken): Task<SessionEvaluationContextResult>',
                '+ RequestRegradeAsync(sessionId: Guid, missingSessionQuestionIds: IReadOnlyCollection<Guid>, bearerToken: string?, ct: CancellationToken): Task',
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
        'context_res': {
            'name': 'SessionEvaluationContextResult <<DTO>>',
            'dto': True,
            'fields': [
                '+ SessionId: Guid',
                '+ Status: string',
                '+ TerminationReason: string?',
                '+ StartedAt: DateTime?',
                '+ EndedAt: DateTime?',
                '+ TotalPausedSeconds: int',
                '+ Questions: List<SessionEvaluationQuestionResult>',
            ],
        },
        'interview_dep_ex': {
            'name': 'InterviewApiDependencyException',
            'methods': ['+ InterviewApiDependencyException()'],
        },

        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ BuildReportAsync(request: AiBuildReportRequest, ct: CancellationToken): Task<AiBuildReportResponse>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (EvaluationAPI)',
            'fields': ['- _httpClient: HttpClient', '- _logger: ILogger<AiServiceClient>'],
            'methods': [
                '+ BuildReportAsync(request: AiBuildReportRequest, ct: CancellationToken): Task<AiBuildReportResponse>',
            ],
        },
        'ai_req': {
            'name': 'AiBuildReportRequest <<DTO>>',
            'dto': True,
            'fields': [
                '+ DurationActualSec: int',
                '+ Evaluations: List<AiAggregateEvaluationRow>',
            ],
        },
        'ai_res': {
            'name': 'AiBuildReportResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ OverallScore: double',
                '+ NumQuestions: int',
                '+ Summary: AiBuildReportSummary?',
            ],
        },

        'irepo': {
            'name': '<<interface>> IEvaluationRepository',
            'methods': [
                '+ GetEvaluationsBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<List<AnswerEvaluation>>',
                '+ GetReportBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<FeedbackReport?>',
                '+ CreateReportWithMetricsAsync(report: FeedbackReport, metrics: IReadOnlyCollection<PerformanceMetric>, ct: CancellationToken): Task<FeedbackReport>',
                '+ UpdateReportAggregateAsync(reportId: Guid, overallScore: decimal, technicalScore: decimal?, communicationScore: decimal?, problemSolvingScore: decimal?, grade: string, ct: CancellationToken): Task',
            ],
        },
        'repo': {
            'name': 'EvaluationRepository (EvaluationAPI)',
            'fields': ['- _db: EvaluationDbContext'],
            'methods': [
                '+ GetEvaluationsBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<List<AnswerEvaluation>>',
                '+ GetReportBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<FeedbackReport?>',
                '+ CreateReportWithMetricsAsync(report: FeedbackReport, metrics: IReadOnlyCollection<PerformanceMetric>, ct: CancellationToken): Task<FeedbackReport>',
                '+ UpdateReportAggregateAsync(reportId: Guid, overallScore: decimal, technicalScore: decimal?, communicationScore: decimal?, problemSolvingScore: decimal?, grade: string, ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'EvaluationDbContext',
            'fields': [
                '+ AnswerEvaluations: DbSet<AnswerEvaluation>',
                '+ FeedbackReports: DbSet<FeedbackReport>',
                '+ PerformanceMetrics: DbSet<PerformanceMetric>',
            ],
            'methods': ['+ EvaluationDbContext(options: DbContextOptions<EvaluationDbContext>)'],
        },

        'report': {
            'name': 'FeedbackReport <<entity>>',
            'fields': [
                '+ ReportId: Guid',
                '+ SessionId: Guid',
                '+ OverallScore: decimal',
                '+ TechnicalScore: decimal?',
                '+ TheoryScore: decimal?',
                '+ CommunicationScore: decimal?',
                '+ ProblemSolvingScore: decimal?',
                '+ Summary: string?',
                '+ Strengths: string?',
                '+ Improvements: string?',
                '+ Recommendations: string?',
                '+ Grade: string?',
                '+ GeneratedAt: DateTime',
            ],
        },
        'metric': {
            'name': 'PerformanceMetric <<entity>>',
            'fields': [
                '+ MetricId: Guid',
                '+ SessionId: Guid',
                '+ MetricName: string',
                '+ MetricValue: decimal',
                '+ RecordedAt: DateTime',
            ],
        },
        'evaluation': {
            'name': 'AnswerEvaluation <<entity>>',
            'fields': [
                '+ EvaluationId: Guid',
                '+ SessionId: Guid',
                '+ SessionQuestionId: Guid',
                '+ Score: decimal',
                '+ CriteriaScoresJson: string?',
                '+ AiFeedback: string?',
                '+ GeneratedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'report_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'lifecycle', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'noteligible_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'notready_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'dependency_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'interview_dep_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'iinterview', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'interview_client', 'to': 'iinterview', 'kind': 'realization'},
        {'from': 'interview_client', 'to': 'eligibility_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'interview_client', 'to': 'context_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'ai_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'ai_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        # aggregate-root pattern, as settled on EnterInterviewRoom: the DbContext owns
        # the root; the rest of the graph is linked entity-to-entity by SessionId.
        {'from': 'db', 'to': 'report', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'report', 'to': 'metric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'report', 'to': 'evaluation', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
