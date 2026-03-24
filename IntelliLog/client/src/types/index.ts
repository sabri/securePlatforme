// ─── Log types ──────────────────────────────────────────────
export interface LogInput {
  source: string;
  message: string;
  timestamp?: string;
}

export interface LogDto {
  id: string;
  timestamp: string;
  severity: string;
  source: string;
  message: string;
  isAnomaly: boolean;
  anomalyScore: number;
}

export interface IngestLogsResult {
  ingested: number;
  anomaliesDetected: number;
  criticalCount: number;
}

export interface GetLogsResult {
  logs: LogDto[];
  totalCount: number;
}

// ─── Document types ─────────────────────────────────────────
export interface DocumentDto {
  id: string;
  title: string;
  category: string;
  snippet: string;
}

export interface IngestDocumentResult {
  documentId: string;
  category: string;
  title: string;
}

export interface GetDocumentsResult {
  documents: DocumentDto[];
  totalCount: number;
}

// ─── Search / RAG types ─────────────────────────────────────
export interface SearchHit {
  documentId: string;
  title: string;
  category: string;
  snippet: string;
  score: number;
}

export interface SearchResult {
  hits: SearchHit[];
}

export interface ClassifyTextResult {
  text: string;
  predictedLabel: string;
  modelAvailable: boolean;
}

// ─── Anomaly types ──────────────────────────────────────────
export interface AnomalyDto {
  logId: string;
  timestamp: string;
  message: string;
  anomalyScore: number;
}

export interface DetectAnomaliesResult {
  totalChecked: number;
  anomaliesFound: number;
  anomalies: AnomalyDto[];
}

// ─── Webhook types ──────────────────────────────────────────
export interface WebhookDto {
  id: string;
  name: string;
  url: string;
  eventType: string;
  isActive: boolean;
  deliveryCount: number;
}

export interface RegisterWebhookResult {
  subscriptionId: string;
  secret: string;
  eventType: string;
}

export interface GetWebhooksResult {
  subscriptions: WebhookDto[];
}

// ─── Data generation types ──────────────────────────────────
export interface GenerateDataResult {
  logsGenerated: number;
  documentsGenerated: number;
}

export interface TrainModelResult {
  modelType: string;
  success: boolean;
  message: string;
}
