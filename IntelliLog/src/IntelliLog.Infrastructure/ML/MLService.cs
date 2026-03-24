using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace IntelliLog.Infrastructure.ML;

// ML.NET input/output schemas
public class TextInput
{
    [LoadColumn(0)] public string Text { get; set; } = "";
    [LoadColumn(1)] public string Label { get; set; } = "";
}

public class TextPrediction
{
    [ColumnName("PredictedLabel")] public string PredictedLabel { get; set; } = "";
}

public class MLService : IMLService
{
    private readonly MLContext _mlContext;
    private PredictionEngine<TextInput, TextPrediction>? _logEngine;
    private PredictionEngine<TextInput, TextPrediction>? _docEngine;

    public bool IsLogModelTrained => _logEngine is not null;
    public bool IsDocumentModelTrained => _docEngine is not null;

    public MLService()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public LogSeverity ClassifyLogSeverity(string message)
    {
        if (_logEngine is null) return FallbackLogSeverity(message);

        var prediction = _logEngine.Predict(new TextInput { Text = message });
        return Enum.TryParse<LogSeverity>(prediction.PredictedLabel, true, out var sev)
            ? sev
            : LogSeverity.Info;
    }

    public DocumentCategory ClassifyDocument(string text)
    {
        if (_docEngine is null) return DocumentCategory.General;

        var prediction = _docEngine.Predict(new TextInput { Text = text });
        return Enum.TryParse<DocumentCategory>(prediction.PredictedLabel, true, out var cat)
            ? cat
            : DocumentCategory.General;
    }

    public List<LogEntry> DetectAnomalies(List<LogEntry> logs)
    {
        // Simple statistical anomaly detection based on message length variance
        // (In production you'd use IidSpikeDetector or SsaSpikeDetector on numeric metrics)
        if (logs.Count < 5) return new List<LogEntry>();

        var lengths = logs.Select(l => (double)l.Message.Length).ToList();
        var mean = lengths.Average();
        var stdDev = Math.Sqrt(lengths.Select(x => Math.Pow(x - mean, 2)).Average());

        if (stdDev < 1) stdDev = 1; // Avoid division by zero

        var anomalies = new List<LogEntry>();
        for (int i = 0; i < logs.Count; i++)
        {
            var zScore = Math.Abs((lengths[i] - mean) / stdDev);
            if (zScore > 2.0) // 2 standard deviations
            {
                logs[i].IsAnomaly = true;
                logs[i].AnomalyScore = zScore;
                anomalies.Add(logs[i]);
            }
        }

        return anomalies;
    }

    public async Task TrainLogClassifierAsync(List<(string Text, string Label)> data, CancellationToken ct = default)
    {
        var model = await TrainClassifierAsync(data, ct);
        _logEngine = _mlContext.Model.CreatePredictionEngine<TextInput, TextPrediction>(model);
    }

    public async Task TrainDocumentClassifierAsync(List<(string Text, string Label)> data, CancellationToken ct = default)
    {
        var model = await TrainClassifierAsync(data, ct);
        _docEngine = _mlContext.Model.CreatePredictionEngine<TextInput, TextPrediction>(model);
    }

    private Task<ITransformer> TrainClassifierAsync(List<(string Text, string Label)> data, CancellationToken ct)
    {
        return Task.Run<ITransformer>(() =>
        {
            var trainData = _mlContext.Data.LoadFromEnumerable(
                data.Select(d => new TextInput { Text = d.Text, Label = d.Label }));

            var pipeline = _mlContext.Transforms.Conversion
                .MapValueToKey("Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(TextInput.Text)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            return pipeline.Fit(trainData);
        }, ct);
    }

    private static LogSeverity FallbackLogSeverity(string message)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("critical") || lower.Contains("unreachable") || lower.Contains("out of memory"))
            return LogSeverity.Critical;
        if (lower.Contains("error") || lower.Contains("exception") || lower.Contains("failed"))
            return LogSeverity.Error;
        if (lower.Contains("warning") || lower.Contains("retry") || lower.Contains("timeout"))
            return LogSeverity.Warning;
        return LogSeverity.Info;
    }
}
