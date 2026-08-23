"""Class spec - Check Biometric Enrollment [BL#12].

Owning service: InterviewAPI. Entry point:
  ProfileBiometricsController.GetStatus  (GET api/profiles/me/biometrics)
  -> ProfileBiometricsService.GetStatusAsync
  -> ProfileBiometricsRepository.ListByUserAsync

Read-only per-modality enrollment state. Two WebClient callers reach this one endpoint
(the Profile > Identity screen, and the face-enrollment gate in InterviewController.Start),
but both are WebClient-side and belong to the sequence page - this page is back-end scope,
so ProfileBiometricsService lists only GetStatusAsync per rule 2.3. EnrollAsync belongs to
#1 EnrollFaceBiometric / #2 EnrollVoiceBiometric and is not drawn here.

The controller answers 503 on any exception (ProfileBiometricsController.cs:38-44) rather
than throwing a typed guard, so no exception class appears in this diagram.
"""

SPEC = {
    'file': 'CheckBiometricEnrollment.drawio',
    'title': 'Class Diagram - Check Biometric Enrollment [BL#12] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'api'],
        ['isvc', 'svc', 'status_res'],
        ['irepo', 'repo', 'db'],
        ['biometric'],
    ],

    'classes': {
        'ctrl': {
            'name': 'ProfileBiometricsController (InterviewAPI)',
            'fields': [
                '- _service: IProfileBiometricsService',
                '- _logger: ILogger<ProfileBiometricsController>',
            ],
            'methods': [
                '+ GetStatus(cancellationToken: CancellationToken): Task<IActionResult>',
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
            'name': '<<interface>> IProfileBiometricsService',
            'methods': [
                '+ GetStatusAsync(userId: Guid, ct: CancellationToken): Task<BiometricStatusResponse>',
            ],
        },
        'svc': {
            'name': 'ProfileBiometricsService (InterviewAPI)',
            'fields': [
                '- _repository: IProfileBiometricsRepository',
                '+ ModalityFace: string',
                '+ ModalityVoice: string',
            ],
            'methods': [
                '+ GetStatusAsync(userId: Guid, ct: CancellationToken): Task<BiometricStatusResponse>',
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

        'irepo': {
            'name': '<<interface>> IProfileBiometricsRepository',
            'methods': [
                '+ ListByUserAsync(userId: Guid, ct: CancellationToken): Task<List<ProfileBiometric>>',
            ],
        },
        'repo': {
            'name': 'ProfileBiometricsRepository (InterviewAPI)',
            'fields': ['- _db: InterviewDbContext'],
            'methods': [
                '+ ListByUserAsync(userId: Guid, ct: CancellationToken): Task<List<ProfileBiometric>>',
            ],
        },
        'db': {
            'name': 'InterviewDbContext',
            'fields': ['+ ProfileBiometrics: DbSet<ProfileBiometric>'],
            'methods': ['+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)'],
        },

        'biometric': {
            'name': 'ProfileBiometric <<entity>>',
            'fields': [
                '+ Id: long',
                '+ UserId: Guid',
                '+ Modality: string',
                '+ ModelVersion: string',
                '+ CreatedAt: DateTime',
                '+ UpdatedAt: DateTime',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'isvc', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'isvc', 'kind': 'realization'},
        {'from': 'svc', 'to': 'status_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'svc', 'to': 'irepo', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'repo', 'to': 'irepo', 'kind': 'realization'},
        {'from': 'repo', 'to': 'db', 'kind': 'association'},
        {'from': 'db', 'to': 'biometric', 'kind': 'association', 'mult_src': '1', 'mult_dst': '0..*'},
    ],
}
