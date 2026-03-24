namespace IntelliLog.Domain.Enums;

public enum WebhookEventType
{
    AnomalyDetected = 0,
    CriticalLogReceived = 1,
    ModelTrained = 2,
    DocumentIngested = 3
}
