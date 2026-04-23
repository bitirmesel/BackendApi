using DktApi.Dtos;
using DktApi.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DktApi.Endpoints;

public static class SyllablePronunciationEndpoints
{
    public static void MapSyllablePronunciationEndpoints(this WebApplication app)
    {
        /// <summary>
        /// POST /api/pronunciation/check-syllable
        /// Hece egzersizleri (S1, S2, S3) için Whisper tabanlı telaffuz kontrolü.
        /// FluentMe yerine doğrudan OpenAI Whisper kullanır — daha hızlı ve basit.
        /// 
        /// Form-data:
        ///   - text: hedef hece metni (ör: "ka", "aka", "kaka")
        ///   - audio_file: WAV ses dosyası
        /// </summary>
        app.MapPost("/api/pronunciation/check-syllable", async (
            HttpRequest request,
            WhisperService whisper,
            PronunciationScoringService scoring) =>
        {
            var requestStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 1. Form verilerini oku
            if (!request.HasFormContentType)
                return Results.BadRequest(new { message = "Content-Type multipart/form-data olmalı." });

            var form = await request.ReadFormAsync();

            var text = form["text"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(text))
                return Results.BadRequest(new { message = "text alanı boş." });

            var audioFile = form.Files.GetFile("audio_file")
                         ?? form.Files.GetFile("audioFile")
                         ?? form.Files.GetFile("audio")
                         ?? form.Files.FirstOrDefault();

            if (audioFile == null || audioFile.Length == 0)
                return Results.BadRequest(new { message = "Ses dosyası yok. form-data'da audio_file gönder." });

            // 2. Ses verisini oku
            byte[] inputBytes;
            await using (var ms = new MemoryStream())
            {
                await audioFile.CopyToAsync(ms);
                inputBytes = ms.ToArray();
            }

            Console.WriteLine($"[SYLLABLE] Hedef: '{text}', Ses boyutu: {inputBytes.Length} bytes");

            try
            {
                // 3. WAV normalize (16kHz mono PCM16 — Whisper için de ideal format)
                var decodeStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var normalizedWav = ConvertTo16kMonoPcm16(inputBytes);
                var decodeEnd = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // 4. Whisper'a gönder — transkript al
                var whisperResult = await whisper.TranscribeAsync(normalizedWav);

                Console.WriteLine($"[SYLLABLE] Whisper transkript: '{whisperResult.Transcript}' ({whisperResult.DurationMs}ms)");

                // 5. PronunciationScoringService ile karşılaştır
                var evaluation = scoring.Evaluate(text, whisperResult.Transcript, 0);

                var responseSend = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                Console.WriteLine($"[SYLLABLE] Skor: {evaluation.Score}, Feedback: {evaluation.FeedbackKey}");

                // 6. Aynı DTO formatında dön (Unity/Flutter tarafı değişmez)
                var resultDto = new PronunciationResultDto
                {
                    TargetText = text,
                    Transcript = whisperResult.Transcript,
                    ApiScore = 0, // Whisper skor vermez, sadece transkript
                    Score = evaluation.Score,
                    Passed = evaluation.Score >= 70,
                    Confidence = evaluation.Confidence,
                    FeedbackKey = evaluation.FeedbackKey,
                    AiReadingUrl = null, // Whisper'da AI reading yok
                    RecordingDurationSec = 0,
                    Timing = new PronunciationTimingDto
                    {
                        TokenMs = 0, // Whisper'da token adımı yok
                        PostMs = 0,  // Whisper'da post adımı yok
                        DecodeMs = decodeEnd - decodeStart,
                        UploadMs = 0, // Cloudinary'ye yükleme yok
                        InferenceMs = whisperResult.DurationMs,
                        TotalMs = responseSend - requestStart
                    }
                };

                return Results.Ok(resultDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SYLLABLE] HATA: {ex.Message}");
                return Results.Json(
                    new { message = "Whisper hatası", detail = ex.Message },
                    statusCode: 500);
            }
        })
        .WithTags("Pronunciation")
        .WithName("CheckSyllablePronunciation")
        .DisableAntiforgery(); // form-data için gerekli
    }

    /// <summary>
    /// WAV dosyasını 16kHz mono PCM16 formatına dönüştürür.
    /// Whisper her formatı kabul eder ama normalize etmek doğruluğu artırır.
    /// </summary>
    private static byte[] ConvertTo16kMonoPcm16(byte[] wavBytes)
    {
        using var inputMs = new MemoryStream(wavBytes);
        using var reader = new WaveFileReader(inputMs);
        ISampleProvider sampleProvider = reader.ToSampleProvider();

        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider);

        if (sampleProvider.WaveFormat.SampleRate != 16000)
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);

        using var outMs = new MemoryStream();
        using (var writer = new WaveFileWriter(outMs, new WaveFormat(16000, 16, 1)))
        {
            var pcm16 = new SampleToWaveProvider16(sampleProvider);
            byte[] buffer = new byte[pcm16.WaveFormat.AverageBytesPerSecond];
            int read;
            while ((read = pcm16.Read(buffer, 0, buffer.Length)) > 0)
                writer.Write(buffer, 0, read);
        }
        return outMs.ToArray();
    }
}
