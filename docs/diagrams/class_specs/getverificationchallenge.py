"""Class spec - Get Verification Challenge [BL#66].

Owning service: InterviewAPI. Entry point:
  SessionVerificationController.GetChallenge  (GET .../verify/challenge)
  -> BiometricVerificationService.IssueChallengeAsync

Issue-only: it stores the expected value and returns just the instruction. No
ai-service call happens here, so there is no IAiServiceClient column.
"""

SPEC = {
    'file': 'GetVerificationChallenge.drawio',
    'title': 'Class Diagram - Get Verification Challenge [BL#66] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'challenge_res'],
        ['ichalrepo', 'chalrepo', 'ibiorepo', 'biorepo', 'isessrepo', 'sessrepo', 'db'],
        ['challenge', 'biometric', 'session'],
    ],

    'classes': {
        'ctrl': {
            'name': 'SessionVerificationController (InterviewAPI)',
            'fields': [
                '- _service: IBiometricVerificationService',
                '- _logger: ILogger<SessionVerificationController>',
            ],
            'methods': [
                '+ GetChallenge(sessionId: Guid, modality: string, cancellationToken: CancellationToken): Task<IActionResult>',
                '- NormalizeModality(modality: string?): string?',
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
            'name': '<<interface>> IBiometricVerificationService',
            'methods': [
                '+ IssueChallengeAsync(userId: Guid, sessionId: Guid, modality: string, ct: CancellationToken): Task<VerificationChallengeResponse>',
            ],
        },
        'svc': {
            'name': 'BiometricVerificationService (InterviewAPI)',
            'fields': [
                '- ChallengePhrases: string[]',
                '- _challengeRepository: ISessionVerificationChallengeRepository',
                '- _biometricsRepository: IProfileBiometricsRepository',
                '- _sessionRepository: ISessionRepository',
                '- _configuration: IConfiguration',
            ],
            'methods': [
                '+ IssueChallengeAsync(userId: Guid, sessionId: Guid, modality: string, ct: CancellationToken): Task<VerificationChallengeResponse>',
                '- EnsureEnabled(): void',
                '- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task',
                '- EnsureEnrolledAsync(userId: Guid, modality: string, ct: CancellationToken): Task',
            ],
        },
        'challenge_res': {
            'name': 'VerificationChallengeResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Modality: string',
                '+ Direction: string?',
                '+ Phrase: string?',
                '+ ExpiresInSeconds: int',
            ],
        },

        'ichalrepo': {
            'name': '<<interface>> ISessionVerificationChallengeRepository',
            'methods': [
                '+ AddAsync(challenge: SessionVerificationChallenge, ct: CancellationToken): Task',
            ],
        },
        'chalrepo': {
            'name': 'SessionVerificationChallengeRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ AddAsync(challenge: SessionVerificationChallenge, ct: CancellationToken): Task',
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
        'isessrepo': {
            'name': '<<interface>> ISessionRepository',
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
            ],
        },
        'sessrepo': {
            'name': 'SessionRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ InterviewSessions: DbSet<InterviewSession>',
                '+ SessionVerificationChallenges: DbSet<SessionVerificationChallenge>',
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'challenge': {
            'name': 'SessionVerificationChallenge <<entity>>',
            'fields': [
                '+ Id: long',
                '+ SessionId: Guid',
                '+ UserId: Guid',
                '+ Modality: string',
                '+ ChallengeJson: string',
                '+ ExpiresAt: DateTime',
                '+ ConsumedAt: DateTime?',
                '+ CreatedAt: DateTime',
            ],
        },
        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': ['+ UserId: Guid', '+ Modality: string', '+ ModelVersion: string'],
        },
        'session': {
            'name': 'InterviewSession <<entity>>',
            'fields': ['+ SessionId: Guid', '+ UserId: Guid', '+ Status: string'],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'challenge_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'ichalrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ibiorepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'isessrepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'challenge', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'chalrepo', 'to': 'ichalrepo', 'kind': 'realization'},
        {'from': 'biorepo', 'to': 'ibiorepo', 'kind': 'realization'},
        {'from': 'sessrepo', 'to': 'isessrepo', 'kind': 'realization'},
        {'from': 'chalrepo', 'to': 'db', 'kind': 'association'},
        {'from': 'biorepo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'challenge', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
        {'from': 'db', 'to': 'session', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
