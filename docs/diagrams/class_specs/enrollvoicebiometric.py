"""Class spec - Enroll Voice Biometric [BL#13, #12, #53].

Owning service: InterviewAPI.

Near-clone of class_specs/enrollfacebiometric.py. The enrollment pipeline is one
shared implementation (ProfileBiometricsService.EnrollAsync switches on modality),
so the two diagrams differ in exactly three places and nowhere else:
  1. controller action   EnrollVoice   instead of EnrollFace
  2. ai-service call     EmbedVoiceAsync instead of EmbedFaceAsync
  3. BiometricEmbeddingResult.Quality is null for voice (face-only), noted here
     because the field stays on the diagram - the type is shared.
Everything else - service, repository, DbContext, entity, both exceptions and the
three helpers - is literally the same code on both paths.

Every signature below was read character-for-character out of source/InterviewAPI/:
  Controllers/ProfileBiometricsController.cs
  Services/Interfaces/IProfileBiometricsService.cs
  Services/ProfileBiometricsService.cs
  Repositories/Interfaces/IProfileBiometricsRepository.cs
  Repositories/ProfileBiometricsRepository.cs
  Clients/IAiServiceClient.cs, Clients/AiServiceClient.cs
  Clients/IPaymentApiClient.cs, Clients/PaymentApiClient.cs
  Clients/BiometricQualityException.cs, Clients/BiometricIdentityMismatchException.cs
  Helpers/EmbeddingCodec.cs, Helpers/BiometricSimilarity.cs, Helpers/CreditConfig.cs
  Models/ProfileBiometric.cs, Models/InterviewDbContext.Biometrics.cs
  DTOs/Biometrics/*.cs, DTOs/AiService/BiometricEmbeddingResult.cs, DTOs/ApiResponse.cs
"""

SPEC = {
    'file': 'EnrollVoiceBiometric.drawio',
    'title': 'Class Diagram - Enroll Voice Biometric [BL#13, #12, #53] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'enroll_res', 'status_res', 'quality_ex', 'mismatch_ex',
         'codec', 'similarity', 'credit_cfg'],
        ['iai', 'ai', 'embed_res', 'ipay', 'pay', 'award_res'],
        ['irepo', 'repo', 'db'],
        ['entity'],
    ],

    'classes': {
        # -- column 1 ---------------------------------------------------------
        'ctrl': {
            'name': 'ProfileBiometricsController (InterviewAPI)',
            'fields': [
                '- _service: IProfileBiometricsService',
            ],
            'methods': [
                '+ GetStatus(cancellationToken: CancellationToken): Task<IActionResult>',
                '+ EnrollVoice(file: IFormFile, cancellationToken: CancellationToken): Task<IActionResult>',
                '- EnrollAsync(modality: string, file: IFormFile, ct: CancellationToken): Task<IActionResult>',
            ],
        },
        'api': {
            'name': 'ApiResponse<T> <<DTO>>',
            'dto': True,
            'fields': [
                '+ Success: bool',
                '+ Message: string?',
                '+ Data: T?',
            ],
            'methods': [
                '+ Ok(data: T, message: string?): ApiResponse<T>',
                '+ Fail(message: string): ApiResponse<T>',
            ],
        },

        # -- column 2 ---------------------------------------------------------
        'isvc': {
            'name': '<<interface>> IProfileBiometricsService',
            'methods': [
                '+ EnrollAsync(userId: Guid, modality: string, file: IFormFile, ct: CancellationToken): Task<BiometricEnrollResponse>',
                '+ GetStatusAsync(userId: Guid, ct: CancellationToken): Task<BiometricStatusResponse>',
            ],
        },
        'svc': {
            'name': 'ProfileBiometricsService (InterviewAPI)',
            'fields': [
                '+ ModalityFace: string',
                '+ ModalityVoice: string',
                '+ BonusStatusAwarded: string',
                '+ BonusStatusAlreadyAwarded: string',
                '+ BonusStatusIdentityAlreadyClaimed: string',
                '+ BonusStatusNotAwarded: string',
                '+ BonusStatusNotYetEligible: string',
                '- _repository: IProfileBiometricsRepository',
                '- _aiServiceClient: IAiServiceClient',
                '- _paymentApiClient: IPaymentApiClient',
                '- _logger: ILogger<ProfileBiometricsService>',
                '- _configuration: IConfiguration',
            ],
            'methods': [
                '+ EnrollAsync(userId: Guid, modality: string, file: IFormFile, ct: CancellationToken): Task<BiometricEnrollResponse>',
                '+ GetStatusAsync(userId: Guid, ct: CancellationToken): Task<BiometricStatusResponse>',
                '- EnsureReEnrollmentMatches(existing: ProfileBiometric, candidate: BiometricEmbeddingResult, modality: string): void',
                '- TryAwardEnrollmentBonusAsync(userId: Guid, justEnrolledModality: string, ct: CancellationToken): Task<string>',
                '- IsCanonicalBonusAccountAsync(userId: Guid, ct: CancellationToken): Task<bool>',
            ],
        },
        'enroll_res': {
            'name': 'BiometricEnrollResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Modality: string',
                '+ Dim: int',
                '+ Quality: double?',
                '+ BonusStatus: string',
            ],
        },
        'status_res': {
            'name': 'BiometricStatusResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ FaceEnrolled: bool',
                '+ FaceUpdatedAt: DateTime?',
                '+ VoiceEnrolled: bool',
                '+ VoiceUpdatedAt: DateTime?',
            ],
        },
        'quality_ex': {
            'name': 'BiometricQualityException',
            'methods': [
                '+ BiometricQualityException(message: string)',
            ],
        },
        'mismatch_ex': {
            'name': 'BiometricIdentityMismatchException',
            'fields': [
                '+ Modality: string',
                '+ Score: double',
                '+ Threshold: double',
            ],
            'methods': [
                '+ BiometricIdentityMismatchException(modality: string, score: double, threshold: double)',
            ],
        },
        'codec': {
            'name': 'EmbeddingCodec',
            'methods': [
                '+ ToBytes(embedding: float[]): byte[]',
                '+ ToFloats(bytes: byte[]): float[]',
            ],
        },
        'similarity': {
            'name': 'BiometricSimilarity',
            'methods': [
                '+ Cosine(left: float[], right: float[]): double',
            ],
        },
        'credit_cfg': {
            'name': 'CreditConfig',
            'fields': [
                '+ BiometricEnrollmentBonusCredits: int',
            ],
        },

        # -- column 3 ---------------------------------------------------------
        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ EmbedVoiceAsync(file: IFormFile, ct: CancellationToken): Task<BiometricEmbeddingResult>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': [
                '- _httpClient: HttpClient',
                '- _logger: ILogger<AiServiceClient>',
            ],
            'methods': [
                '+ EmbedVoiceAsync(file: IFormFile, ct: CancellationToken): Task<BiometricEmbeddingResult>',
                '- EmbedAsync(path: string, file: IFormFile, ct: CancellationToken): Task<BiometricEmbeddingResult>',
            ],
        },
        'embed_res': {
            'name': 'BiometricEmbeddingResult <<DTO>>',
            'dto': True,
            'fields': [
                '+ Embedding: float[]',
                '+ Dim: int',
                '+ Quality: double?',
                '+ ModelVersion: string',
            ],
        },
        'ipay': {
            'name': '<<interface>> IPaymentApiClient',
            'methods': [
                '+ AwardCreditAsync(userId: Guid, amount: int, referenceId: Guid, description: string, ct: CancellationToken): Task<CreditAwardResponse>',
            ],
        },
        'pay': {
            'name': 'PaymentApiClient (InterviewAPI)',
            'fields': [
                '- _httpClient: HttpClient',
                '- _logger: ILogger<PaymentApiClient>',
            ],
            'methods': [
                '+ AwardCreditAsync(userId: Guid, amount: int, referenceId: Guid, description: string, ct: CancellationToken): Task<CreditAwardResponse>',
            ],
        },
        'award_res': {
            'name': 'CreditAwardResponse <<DTO>>',
            'dto': True,
            'fields': [
                '+ Success: bool',
                '+ Awarded: bool',
                '+ BalanceAfter: int',
                '+ Message: string?',
            ],
        },

        # -- column 4 ---------------------------------------------------------
        'irepo': {
            'name': '<<interface>> IProfileBiometricsRepository',
            'methods': [
                '+ GetAsync(userId: Guid, modality: string, ct: CancellationToken): Task<ProfileBiometric?>',
                '+ ListByUserAsync(userId: Guid, ct: CancellationToken): Task<List<ProfileBiometric>>',
                '+ ListByModalityAsync(modality: string, ct: CancellationToken): Task<List<ProfileBiometric>>',
                '+ AddAsync(entity: ProfileBiometric, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'repo': {
            'name': 'ProfileBiometricsRepository (InterviewAPI)',
            'fields': [
                '- _db: InterviewDbContext',
            ],
            'methods': [
                '+ GetAsync(userId: Guid, modality: string, ct: CancellationToken): Task<ProfileBiometric?>',
                '+ ListByUserAsync(userId: Guid, ct: CancellationToken): Task<List<ProfileBiometric>>',
                '+ ListByModalityAsync(modality: string, ct: CancellationToken): Task<List<ProfileBiometric>>',
                '+ AddAsync(entity: ProfileBiometric, ct: CancellationToken): Task',
                '+ SaveChangesAsync(ct: CancellationToken): Task',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': [
                '+ ProfileBiometrics: DbSet<ProfileBiometric>',
            ],
            'methods': [
                '+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)',
            ],
        },

        # -- column 5 ---------------------------------------------------------
        'entity': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': [
                '+ Id: long',
                '+ UserId: Guid',
                '+ Modality: string',
                '+ Embedding: byte[]',
                '+ Dim: int',
                '+ ModelVersion: string',
                '+ Quality: double?',
                '+ CreatedAt: DateTime',
                '+ UpdatedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'enroll_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'status_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'mismatch_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'svc', 'to': 'codec', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'similarity', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'credit_cfg', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'ipay', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'svc', 'to': 'entity', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
        {'from': 'ai', 'to': 'embed_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'quality_ex', 'kind': 'dependency', 'label': '<<throw>>'},
        {'from': 'pay', 'to': 'ipay', 'kind': 'realization'},
        {'from': 'pay', 'to': 'award_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'entity', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
