using IntelliLog.Application.Common.Interfaces;
using IntelliLog.Domain.Entities;
using IntelliLog.Domain.Enums;

namespace IntelliLog.Infrastructure.DataGeneration;

public class SyntheticDataGenerator : IDataGenerator
{
    private static readonly Random Rng = new(42);

    private static readonly string[] Sources = {
        "AuthService", "PaymentGateway", "UserAPI", "DatabaseEngine",
        "CacheLayer", "FileProcessor", "NotificationHub", "SchedulerWorker",
        "APIGateway", "MetricsCollector"
    };

    private static readonly Dictionary<LogSeverity, string[]> LogTemplates = new()
    {
        [LogSeverity.Info] = new[]
        {
            "User {0} logged in successfully from {1}.",
            "Request processed in {2}ms for endpoint {3}.",
            "Cache hit ratio: {4}% for service {5}.",
            "Health check passed for {6}. Uptime: {7} hours.",
            "Configuration reloaded for module {8}.",
            "Scheduled task {9} completed successfully."
        },
        [LogSeverity.Warning] = new[]
        {
            "Retry attempt {0} for connection to {1}.",
            "Response time exceeded threshold: {2}ms for {3}.",
            "Memory usage at {4}% on instance {5}.",
            "Timeout waiting for lock on resource {6}.",
            "Rate limit approaching for API key {7}.",
            "Certificate for {8} expires in {9} days."
        },
        [LogSeverity.Error] = new[]
        {
            "Failed to connect to database: Connection refused on {0}:{1}.",
            "NullReferenceException in {2}.{3}() at line {4}.",
            "Authentication failed for user {5}: Invalid credentials.",
            "File not found: {6}. Operation aborted.",
            "Exception processing payment #{7}: Insufficient funds.",
            "HTTP 500 returned from upstream service {8}."
        },
        [LogSeverity.Critical] = new[]
        {
            "CRITICAL: Database server {0} is unreachable. Failover initiated.",
            "Out of memory on instance {1}. Emergency GC triggered.",
            "Data corruption detected in table {2}. Writes suspended.",
            "CRITICAL: Disk space below 1% on volume {3}.",
            "Security breach detected: unauthorized access from {4}.",
            "CRITICAL: SSL certificate expired for {5}. Service unavailable."
        }
    };

    private static readonly Dictionary<DocumentCategory, (string TitleTemplate, string ContentTemplate)[]> DocTemplates = new()
    {
        [DocumentCategory.Technical] = new[]
        {
            ("API Reference: {0} Service", "This document describes the REST API for the {0} service. Endpoints include GET /api/{1}, POST /api/{1}, PUT /api/{1}/{{id}}, DELETE /api/{1}/{{id}}. Authentication uses Bearer tokens. Rate limiting is set to 100 requests per minute per client. The service runs on .NET 8 with Entity Framework Core for data persistence."),
            ("Architecture Decision: {0}", "We decided to use {0} because it provides better scalability and maintainability. The trade-offs include increased complexity in deployment but reduced latency for end users. The microservice communicates via gRPC for internal calls and REST for external consumers."),
            ("Deployment Guide: {0}", "To deploy {0}, ensure Docker is installed and configured. Run the pipeline with: dotnet publish -c Release followed by docker build. The container requires ports 8080 and 443. Health checks are available at /health. Environment variables: DATABASE_URL, REDIS_URL, JWT_SECRET.")
        },
        [DocumentCategory.Security] = new[]
        {
            ("Security Policy: {0}", "All API endpoints must validate JWT tokens with RS256 signatures. Passwords are hashed using bcrypt with a cost factor of 12. Sessions expire after 30 minutes of inactivity. Two-factor authentication is required for admin accounts. CORS is restricted to approved origins only."),
            ("Incident Report: {0}", "On {1}, a security incident was detected involving {0}. The attack vector was identified as a brute-force attempt on the authentication endpoint. Mitigation steps included IP blocking, rate limiting enforcement, and mandatory password resets for affected accounts. No data exfiltration occurred."),
            ("Vulnerability Assessment: {0}", "The security scan of {0} identified 3 low-severity and 1 medium-severity findings. The medium finding relates to outdated TLS configuration. Remediation involves updating the cipher suite and enforcing TLS 1.3 minimum. All findings have remediation deadlines within 30 days.")
        },
        [DocumentCategory.Business] = new[]
        {
            ("Quarterly Report: {0}", "Revenue grew by 15% compared to the previous quarter. Active users reached {1} with a retention rate of 78%. Key metrics: Average session duration 12 minutes, conversion rate 3.2%, customer satisfaction score 4.5/5. The product roadmap includes real-time analytics and AI-powered recommendations."),
            ("SLA Document: {0} Service", "The {0} service guarantees 99.9% uptime measured monthly. Planned maintenance windows are Saturday 02:00-06:00 UTC. Response time SLA: P95 < 200ms. Support response times: Critical - 15 minutes, High - 1 hour, Medium - 4 hours, Low - 24 hours."),
            ("Project Proposal: {0}", "This proposal outlines the implementation of {0}. Estimated timeline: 8 weeks. Team size: 4 engineers, 1 designer. Expected ROI: 25% efficiency improvement. Budget: allocated within Q3 operational expenses. Key milestones: prototype (week 2), MVP (week 5), production release (week 8).")
        },
        [DocumentCategory.Support] = new[]
        {
            ("Troubleshooting: {0}", "If you encounter {0}, try the following steps: 1) Clear browser cache and cookies. 2) Verify network connectivity. 3) Check service status at status.example.com. 4) If the issue persists, collect logs using the diagnostic tool and contact support with ticket number."),
            ("FAQ: {0}", "Q: How do I reset my password for {0}? A: Navigate to the login page and click 'Forgot Password'. Enter your email address. Q: What browsers are supported? A: Chrome 90+, Firefox 88+, Safari 14+, Edge 90+. Q: How do I export my data? A: Go to Settings > Data > Export."),
            ("Runbook: {0} Restart", "To restart the {0} service: 1) SSH into the host. 2) Run: systemctl stop {1}. 3) Verify no lingering processes. 4) Run: systemctl start {1}. 5) Verify health at /health. Expected recovery time: 30 seconds. Escalation: if service doesn't recover in 5 minutes, page on-call engineer.")
        },
        [DocumentCategory.General] = new[]
        {
            ("Meeting Notes: {0}", "Attendees discussed the progress on {0}. Key decisions: approved the new feature flag system, agreed to deprecate the legacy endpoint by end of month. Action items: update documentation (due Friday), schedule load test (next week), review pull requests for the authentication module."),
            ("Onboarding Guide: {0}", "Welcome to the team! This guide covers {0} development workflow. Set up your environment: install .NET 8 SDK, Node.js 20 LTS, Docker Desktop. Clone the repository and run dotnet restore followed by npm install. Development server: dotnet run -- the API will be available at https://localhost:7001."),
            ("Release Notes: v{0}", "Version {0} includes: new dashboard analytics, improved search performance (40% faster), bug fix for session timeout handling, updated dependency packages. Breaking changes: the /api/v1/legacy endpoint has been removed. Migration guide available in the docs folder.")
        }
    };

    private static readonly string[] Names = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry" };
    private static readonly string[] IPs = { "192.168.1.100", "10.0.0.55", "172.16.0.12", "203.0.113.42" };
    private static readonly string[] Services = { "UserService", "OrderService", "PaymentService", "NotificationService", "AnalyticsService" };

    public List<LogEntry> GenerateLogs(int count)
    {
        var logs = new List<LogEntry>(count);
        var severities = Enum.GetValues<LogSeverity>();
        var baseTime = DateTime.UtcNow.AddHours(-count);

        for (int i = 0; i < count; i++)
        {
            // Weight towards Info (60%), Warning (25%), Error (12%), Critical (3%)
            var roll = Rng.NextDouble();
            var severity = roll switch
            {
                < 0.60 => LogSeverity.Info,
                < 0.85 => LogSeverity.Warning,
                < 0.97 => LogSeverity.Error,
                _ => LogSeverity.Critical
            };

            var templates = LogTemplates[severity];
            var template = templates[Rng.Next(templates.Length)];
            var message = FillTemplate(template);

            logs.Add(new LogEntry
            {
                Timestamp = baseTime.AddMinutes(i * (60.0 / count * count / Math.Max(count, 1))).AddSeconds(Rng.Next(60)),
                Severity = severity,
                Source = Sources[Rng.Next(Sources.Length)],
                Message = message
            });
        }

        return logs;
    }

    public List<Document> GenerateDocuments(int count)
    {
        var docs = new List<Document>(count);
        var categories = Enum.GetValues<DocumentCategory>();

        for (int i = 0; i < count; i++)
        {
            var category = categories[Rng.Next(categories.Length)];
            var templates = DocTemplates[category];
            var (titleTemplate, contentTemplate) = templates[Rng.Next(templates.Length)];

            var topic = Services[Rng.Next(Services.Length)];
            var title = string.Format(titleTemplate, topic);
            var content = string.Format(contentTemplate, topic, Rng.Next(1000, 50000));

            docs.Add(new Document
            {
                Title = title,
                Content = content,
                Category = category
            });
        }

        return docs;
    }

    private static string FillTemplate(string template)
    {
        // Replace {0}-{9} placeholders with contextual random values
        for (int i = 0; i <= 9; i++)
        {
            var placeholder = $"{{{i}}}";
            if (!template.Contains(placeholder)) continue;

            var value = i switch
            {
                0 => Names[Rng.Next(Names.Length)],
                1 => IPs[Rng.Next(IPs.Length)],
                2 => Rng.Next(50, 5000).ToString(),
                3 => $"/api/{Services[Rng.Next(Services.Length)].ToLower()}",
                4 => Rng.Next(60, 99).ToString(),
                5 => Sources[Rng.Next(Sources.Length)],
                6 => $"resource_{Rng.Next(1, 100)}",
                7 => $"key_{Rng.Next(1000, 9999)}",
                8 => Services[Rng.Next(Services.Length)],
                9 => Rng.Next(1, 30).ToString(),
                _ => "unknown"
            };

            template = template.Replace(placeholder, value);
        }

        return template;
    }
}
