"""Class spec - Voice Interaction [BL#42, #43].

Owning service: InterviewAPI.

Scope note (section 1): TTS/STT are stateless proxies. InterviewSessionController
calls IAiServiceClient directly - there is no service-layer or repository hop in
this use case, and none is invented here (see source/InterviewAPI/Controllers/
InterviewSessionController.cs, actions TextToSpeech and SpeechToText).
"""

SPEC = {
    'file': 'VoiceInteraction.drawio',
    'title': 'Class Diagram - Voice Interaction [BL#42, #43] - Back-end scope (InterviewAPI)',

    'columns': [
        ['ctrl', 'tts_req', 'stt_res', 'api'],
        ['iai', 'ai'],
    ],

    'classes': {
        'ctrl': {
            'name': 'InterviewSessionController (InterviewAPI)',
            'fields': [
                '- _aiServiceClient: IAiServiceClient',
            ],
            'methods': [
                '+ TextToSpeech(req: TtsRequest, cancellationToken: CancellationToken): Task<IActionResult>',
                '+ SpeechToText(audio: IFormFile, cancellationToken: CancellationToken): Task<IActionResult>',
            ],
        },
        'tts_req': {
            'name': 'TtsRequest <<DTO>>',
            'dto': True,
            'fields': ['+ Text: string'],
        },
        'stt_res': {
            'name': 'SttResponse <<DTO>>',
            'dto': True,
            'fields': ['+ Text: string'],
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
        'iai': {
            'name': '<<interface>> IAiServiceClient',
            'methods': [
                '+ SynthesizeSpeechAsync(text: string, ct: CancellationToken): Task<byte[]>',
                '+ TranscribeAsync(audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<string>',
            ],
        },
        'ai': {
            'name': 'AiServiceClient (InterviewAPI)',
            'fields': [
                '- _httpClient: HttpClient',
                '- _logger: ILogger<AiServiceClient>',
            ],
            'methods': [
                '+ SynthesizeSpeechAsync(text: string, ct: CancellationToken): Task<byte[]>',
                '+ TranscribeAsync(audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<string>',
            ],
        },
    },

    'edges': [
        {'from': 'ctrl', 'to': 'iai', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'tts_req', 'kind': 'dependency', 'label': '<<use>>'},
        {'from': 'ctrl', 'to': 'stt_res', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ctrl', 'to': 'api', 'kind': 'dependency', 'label': '<<create>>'},
        {'from': 'ai', 'to': 'iai', 'kind': 'realization'},
    ],
}
