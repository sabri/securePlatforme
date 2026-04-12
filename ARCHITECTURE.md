# SecurePlatform — Full Project Architecture

## 1. Project Purpose

**SecurePlatform** is a full-stack, enterprise-grade authentication and AI platform that demonstrates production-level security best practices for modern web applications. It combines:

- **JWT-based authentication** with HTTP-only cookies (BFF pattern)
- **OAuth 2.0** social login (Google, GitHub)
- **Password reset workflows** with email verification
- **CSRF/XSS/Clickjacking protections** via security headers and anti-forgery tokens
- **Rate limiting** to prevent brute-force and DoS attacks
- **Token revocation** via Redis blacklist
- **Local AI integration** via Ollama for LLM completions and RAG-ready Q&A

---

## 2. Tech Stack

### Backend

| Technology | Version | Purpose |
|---|---|---|
| .NET | 8.0 | Runtime & framework |
| ASP.NET Core | 8.0 | Web API |
| Entity Framework Core | 8.0.2 | ORM / data access |
| SQLite | — | Database (file-based, zero-config) |
| ASP.NET Core Identity | 8.0.2 | User management, password hashing |
| JWT Bearer Authentication | 8.0.2 | Token validation middleware |
| StackExchange.Redis | 2.12.4 | Token revocation cache |
| OllamaSharp | 5.4.25 | Local LLM client (Ollama) |
| Swashbuckle (Swagger) | 6.5.0 | API documentation |

### Frontend

| Technology | Version | Purpose |
|---|---|---|
| React | 18.3.1 | UI library |
| TypeScript | 5.6.2 | Type safety |
| Vite | 5.4.10 | Build tool & dev server |
| Redux Toolkit | 2.11.2 | State management |
| React Router | 7.13.1 | Client-side routing |
| Axios | 1.13.6 | HTTP client with interceptors |
| ESLint | 9.13.0 | Code linting |

### AI / LLM

| Technology | Version | Purpose |
|---|---|---|
| Ollama | 0.20.2 | Local LLM inference server |
| llama3.2:1b | — | Default language model (~1.3 GB) |

### Infrastructure

| Service | Purpose |
|---|---|
| Redis (`localhost:6379`) | JWT revocation blacklist |
| SQLite (`SecurePlatform.db`) | Application database |
| Ollama (`localhost:11434`) | LLM API server |
| SMTP (Gmail/configurable) | Password reset emails |

---

## 3. Architecture Overview

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                     SecurePlatform.API                       │
│        Controllers, Middleware, Program.cs, Config           │
├──────────────────────┬──────────────────────────────────────┤
│  SecurePlatform.AI   │     SecurePlatform.Infrastructure    │
│  Ollama integration  │     EF Core, Identity, Redis, SMTP  │
├──────────────────────┴──────────────────────────────────────┤
│                  SecurePlatform.Application                  │
│           Interfaces, DTOs, Settings (contracts)            │
├─────────────────────────────────────────────────────────────┤
│                    SecurePlatform.Domain                     │
│               Entities, Enums (core models)                 │
└─────────────────────────────────────────────────────────────┘
```

**Dependency rule:** outer layers depend inward. Domain has zero dependencies. Application defines contracts. Infrastructure and AI implement them. API wires everything via DI.

### Full System Diagram

```
┌──────────────────┐                    ┌──────────────────────────────────────────────┐
│                  │   /api/auth/*      │              SecurePlatform.API               │
│   React SPA      │   /api/oauth/*     │                                              │
│   (Vite + TS)    │──────────────────► │  AuthController   OAuthController             │
│                  │   /api/ai/*        │  AiController                                │
│   Redux Store    │ ◄──────────────────│                                              │
│   Axios + CSRF   │   HTTP-only        │  ┌─── Middleware ──────────────────────────┐ │
│                  │   cookies          │  │ CORS ► Security Headers ► Rate Limit    │ │
└──────────────────┘                    │  │ ► CSRF Validation ► JWT Auth            │ │
       ▲                                │  └─────────────────────────────────────────┘ │
       │ Proxy                          └────────┬──────────────────┬──────────────────┘
       │ localhost:5173 → 5237                   │                  │
                                                 │ DI               │ DI
                                    ┌────────────▼──┐    ┌─────────▼────────────┐
                                    │ SecurePlatform │    │ SecurePlatform       │
                                    │ .AI            │    │ .Infrastructure      │
                                    │                │    │                      │
                                    │ AiService      │    │ AuthService          │
                                    │              │    │ TokenService         │
                                    └───────┬───────┘    │ RedisRevocation      │
                                            │            │ SmtpEmailService     │
                                            ▼            └───┬──────┬──────┬────┘
                                    ┌───────────────┐        │      │      │
                                    │  Ollama       │        ▼      ▼      ▼
                                    │  localhost:    │   ┌───────┐┌─────┐┌──────┐
                                    │  11434         │   │SQLite ││Redis││ SMTP │
                                    │  llama3.2:1b   │   └───────┘└─────┘└──────┘
                                    └───────────────┘
```

---

## 4. Security Architecture

### 4.1 Authentication Flow (BFF Pattern)

Tokens are **never exposed to JavaScript**. The backend stores JWT access tokens and refresh tokens in **HTTP-only, Secure, SameSite=Strict cookies**.

```
User submits credentials
       │
       ▼
POST /api/auth/login
       │
       ▼
Server validates → generates JWT + RefreshToken
       │
       ▼
Set-Cookie: AccessToken  (HttpOnly, Secure, SameSite=Strict, 15 min)
Set-Cookie: RefreshToken (HttpOnly, Secure, SameSite=Strict, 7 days)
       │
       ▼
Browser auto-sends cookies on every same-origin request
       │
       ▼
JS cannot read tokens → XSS cannot steal them
```

### 4.2 JWT Token Strategy

| Token | Storage | Lifetime | Content |
|---|---|---|---|
| Access Token | HTTP-only cookie | 15 minutes | `sub`, `email`, `name`, `roles`, `jti` |
| Refresh Token | HTTP-only cookie + DB | 7 days | Cryptographic random (64 bytes, Base64) |

- Access tokens signed with **HMAC-SHA256**
- Refresh tokens support **rotation** — old token is revoked, new one issued
- Rotation chain tracked via `ReplacedByToken` field

### 4.3 Token Revocation (Redis)

On logout or security event, the JWT's `jti` claim is blacklisted in Redis:

```
Key:   revoked-token:{jti}
Value: "revoked"
TTL:   remaining token lifetime (auto-cleanup)
```

Every request passes through middleware that checks Redis before granting access. If Redis is down, tokens still validated structurally (graceful degradation).

### 4.4 CSRF / XSRF Protection

```
GET /api/auth/csrf-token → Server sets XSRF-TOKEN cookie (readable by JS)
                                │
Client reads cookie value ──────┘
                                │
POST /api/auth/login            │
  Header: X-XSRF-TOKEN: {value}◄┘
                                │
Server validates header matches generated token
```

### 4.5 HTTP Security Headers

| Header | Value | Protects Against |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | MIME-sniffing XSS |
| `X-Frame-Options` | `DENY` | Clickjacking |
| `X-XSS-Protection` | `1; mode=block` | Legacy browser XSS |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Referrer info leak |
| `Content-Security-Policy` | `default-src 'self'; frame-ancestors 'none'` | Resource injection |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Browser feature abuse |

### 4.6 Rate Limiting

| Policy | Limit | Window | Applied To | Purpose |
|---|---|---|---|---|
| `auth` | 5 req | 60s per IP | Login, register, password reset | Brute-force protection |
| `ai` | 10 req | 60s per IP | `/api/ai/*` | Expensive compute protection |
| `global` | 100 req | 60s per IP | All other endpoints | General DoS prevention |

Rate limiter runs **before authentication** — abusive requests fail fast.

### 4.7 Input Validation & XSS Prevention

- **HTML/script tag detection** in auth endpoints (`ContainsHtmlOrScript()`)
- **Prompt length cap** at 4000 chars on AI endpoints
- **URL parameter escaping** for OAuth redirects (`Uri.EscapeDataString`)
- **EF Core parameterized queries** — zero raw SQL, prevents SQL injection
- **React auto-escaping** — JSX escapes rendered output by default

### 4.8 CORS

```csharp
policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

Only the trusted React dev servers are allowed. `AllowCredentials` enables cookie transmission.

---

## 5. OAuth 2.0 Integration

**Providers:** Google, GitHub (Authorization Code Flow)

```
User clicks "Login with Google"
       │
       ▼
GET /api/oauth/google → 302 Redirect to Google consent screen
       │
       ▼
User grants consent → Google redirects to /api/oauth/google/callback?code=xxx
       │
       ▼
Backend exchanges code for access token → fetches user info (email, name)
       │
       ▼
Create or update ApplicationUser in DB
       │
       ▼
Issue JWT + RefreshToken cookies → Redirect to /oauth/callback (frontend)
       │
       ▼
Frontend calls GET /api/auth/me → navigates to /dashboard
```

---

## 6. Password Reset Workflow

```
POST /api/auth/forgot-password { email }
       │
       ▼
Generate reset token via Identity → Send code via SMTP (or console in dev)
       │
       ▼
User receives email with code
       │
       ▼
POST /api/auth/reset-password { email, code, newPassword, confirmPassword }
       │
       ▼
Validate code → Update password → Revoke ALL refresh tokens → Force fresh login
```

**Email providers (pluggable):**
- `SmtpEmailService` — Gmail, Outlook, Brevo (production)
- `DevEmailService` — Console output (development)

---

## 7. AI Layer — Ollama Integration

### How It Works

The AI layer uses **Ollama** running locally to provide LLM inference. The `AiService` communicates with it via the OllamaSharp client library.

```
POST /api/ai/complete  ──►  AiService.GetCompletionAsync()
POST /api/ai/ask       ──►  AiService.QueryKnowledgeBaseAsync()
                                     │
                                     ▼ OllamaSharp (HTTP)
                              Ollama server (localhost:11434)
                              Model: llama3.2:1b
```

### Configuration (`appsettings.json`)

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "Model": "llama3.2:1b"
}
```

Swap models by changing `Model` and running `ollama pull <model-name>`.

### Key Classes

- **`OllamaSettings`** — Strongly-typed config bound via Options pattern
- **`AiService`** — Implements `IAiService`, calls Ollama for completions
- **`QueryKnowledgeBaseAsync`** — RAG-ready: wraps question in a system prompt, ready for vector DB context injection

---

## 8. API Endpoints

### Authentication

| Method | Route | Rate Limit | Auth | Purpose |
|---|---|---|---|---|
| GET | `/api/auth/csrf-token` | global | No | Get CSRF token cookie |
| POST | `/api/auth/register` | auth | No | Create account |
| POST | `/api/auth/login` | auth | No | Sign in |
| POST | `/api/auth/refresh` | auth | No | Refresh expired token |
| POST | `/api/auth/logout` | global | Yes | Revoke all tokens |
| GET | `/api/auth/me` | global | Yes | Get current user profile |
| POST | `/api/auth/forgot-password` | auth | No | Request password reset |
| POST | `/api/auth/reset-password` | auth | No | Submit reset code + new password |

### OAuth

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/oauth/google` | Redirect to Google consent |
| GET | `/api/oauth/github` | Redirect to GitHub consent |
| GET | `/api/oauth/google/callback` | Handle Google callback |
| GET | `/api/oauth/github/callback` | Handle GitHub callback |

### AI

| Method | Route | Rate Limit | Auth | Purpose |
|---|---|---|---|---|
| POST | `/api/ai/complete` | ai (10/60s) | Yes | Free-form LLM completion |
| POST | `/api/ai/ask` | ai (10/60s) | Yes | Knowledge-base Q&A |

**Request:** `{ "prompt": "Your question" }` — **Response:** `{ "response": "..." }`

---

## 9. Domain Model

### Entities

**`ApplicationUser`** (extends IdentityUser)

| Property | Type | Description |
|---|---|---|
| `FirstName` | string | User first name |
| `LastName` | string | User last name |
| `CreatedAt` | DateTime | Account creation timestamp |
| `LastLoginAt` | DateTime? | Last successful login |
| `IsActive` | bool | Soft-delete flag |
| `RefreshTokens` | Collection | Associated refresh tokens |

**`RefreshToken`** (token rotation)

| Property | Type | Description |
|---|---|---|
| `Token` | string | Cryptographic random string |
| `ExpiresAt` | DateTime | Token expiration |
| `RevokedAt` | DateTime? | Revocation timestamp |
| `ReplacedByToken` | string? | Points to new token in rotation chain |
| `ReasonRevoked` | string? | Why the token was revoked |
| `IsActive` | computed | `!IsRevoked && !IsExpired` |

### Enums

**`AuthResultType`**: `Success`, `InvalidCredentials`, `UserNotFound`, `UserLocked`, `EmailNotConfirmed`, `TokenExpired`, `TokenInvalid`, `RegistrationFailed`, `DuplicateEmail`, `PasswordResetFailed`, `PasswordResetCodeSent`, `PasswordResetSuccess`, `ServerError`

---

## 10. Frontend Architecture

### Pages & Routing

| Route | Component | Protected | Purpose |
|---|---|---|---|
| `/login` | `LoginPage` | No | Email/password sign-in |
| `/register` | `RegisterPage` | No | Account creation |
| `/forgot-password` | `ForgotPasswordPage` | No | Request password reset |
| `/reset-password` | `ResetPasswordPage` | No | Submit reset code |
| `/oauth/callback` | `OAuthCallbackPage` | No | OAuth redirect handler |
| `/dashboard` | `DashboardPage` | **Yes** | User dashboard |
| `*` | Redirect → `/login` | — | Catch-all fallback |

### State Management (Redux)

```
store
 └── auth (authSlice)
      ├── user: UserDto | null
      ├── isAuthenticated: boolean
      ├── isLoading: boolean
      └── error: string | null
```

**Async thunks:** `initAuth` (restore session), `loginUser`, `registerUser`, `logoutUser`

### Axios Interceptors (`api.ts`)

- **Request:** Reads `XSRF-TOKEN` cookie, attaches as `X-XSRF-TOKEN` header
- **Response (401):** Auto-calls `POST /api/auth/refresh`, retries the original request. Skips refresh for `/auth/*` to avoid infinite loops.

### BFF Proxy (Vite)

The dev server proxies `/api` to the .NET backend, enabling same-origin cookie flow:

```
localhost:5173/api/*  ──proxy──►  localhost:5237/api/*
```

---

## 11. Project Structure

```
SecurePlatform/
├── client/                          # React SPA (Vite + TypeScript)
│   ├── src/
│   │   ├── components/ProtectedRoute.tsx
│   │   ├── hooks/useRedux.ts
│   │   ├── pages/                   # Login, Register, Dashboard, OAuth, etc.
│   │   ├── services/api.ts          # Axios client + interceptors
│   │   ├── store/slices/authSlice.ts
│   │   └── types/auth.ts
│   ├── package.json
│   └── vite.config.ts               # API proxy config
│
├── src/
│   ├── API/                         # ASP.NET Core Web API (entry point)
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── OAuthController.cs
│   │   │   └── AiController.cs
│   │   ├── Program.cs               # Middleware pipeline & DI
│   │   └── appsettings.json         # All configuration
│   │
│   ├── Application/                 # Contracts & DTOs (no dependencies)
│   │   ├── Interfaces/
│   │   │   ├── IAuthService.cs
│   │   │   ├── ITokenService.cs
│   │   │   ├── ITokenRevocationService.cs
│   │   │   ├── IEmailService.cs
│   │   │   └── IAiService.cs
│   │   ├── DTOs/Auth/               # Request/Response records
│   │   └── Common/                  # JwtSettings, SmtpSettings
│   │
│   ├── Domain/                      # Core entities (zero dependencies)
│   │   ├── Entities/
│   │   │   ├── ApplicationUser.cs
│   │   │   └── RefreshToken.cs
│   │   └── Enums/AuthResultType.cs
│   │
│   ├── Infrastructure/              # Implementations
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── DbInitializer.cs     # Seeds Admin role + user
│   │   ├── Services/
│   │   │   ├── AuthService.cs
│   │   │   ├── TokenService.cs
│   │   │   ├── RedisTokenRevocationService.cs
│   │   │   ├── SmtpEmailService.cs
│   │   │   └── DevEmailService.cs
│   │   └── DependencyInjection.cs
│   │
│   └── AI/                          # LLM integration
│       ├── Services/AiService.cs    # Ollama client
│       └── DependencyInjection.cs
│
└── SecurePlatform.sln
```

---

## 12. Configuration Reference (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=SecurePlatform.db",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "REPLACE_WITH_A_SECURE_KEY_AT_LEAST_32_CHARS_LONG",
    "Issuer": "SecurePlatform",
    "Audience": "SecurePlatformUsers",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "SmtpSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": true
  },
  "OAuth": {
    "Google": { "ClientId": "...", "ClientSecret": "..." },
    "GitHub": { "ClientId": "...", "ClientSecret": "..." }
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2:1b"
  }
}
```

---

## 13. How to Run

```bash
# 1. Start Redis
redis-server

# 2. Start Ollama (if not running as a service)
ollama serve
ollama pull llama3.2:1b

# 3. Run the .NET API
dotnet run --project src/API

# 4. Run the React client
cd client
npm install
npm run dev
```

- **API:** `http://localhost:5237`
- **Client:** `http://localhost:5173` (proxies `/api` to backend)
- **Swagger:** `http://localhost:5237/swagger`
- **Seeded admin:** `admin@secureplatform.com` / `Admin123!`

---

## 14. Security Summary

| Attack Vector | Mitigation |
|---|---|
| **XSS (token theft)** | HTTP-only cookies — JS cannot access tokens |
| **CSRF** | Anti-forgery tokens (`X-XSRF-TOKEN` header validation) |
| **Brute-force** | Rate limiting (5 req/60s on auth endpoints) |
| **Token replay after logout** | Redis JTI blacklist with auto-expiring TTL |
| **Clickjacking** | `X-Frame-Options: DENY` + CSP `frame-ancestors 'none'` |
| **MIME sniffing** | `X-Content-Type-Options: nosniff` |
| **SQL injection** | EF Core parameterized queries only |
| **Credential exposure in JS** | BFF pattern — tokens live server-side in cookies |
| **OAuth redirect hijack** | `Uri.EscapeDataString` on redirect URIs |
| **Stale refresh tokens** | Token rotation with chain tracking + revocation on logout |
| **AI abuse / DoS** | Separate rate limit policy (10 req/60s) on AI endpoints |

---

## 15. Next Steps

- **RAG Pipeline:** Integrate a vector database (Qdrant, Chroma) to retrieve document chunks for context-aware AI answers
- **Streaming AI:** Expose OllamaSharp streaming via Server-Sent Events or SignalR
- **Model Switching:** Runtime model selection endpoint
- **Embeddings:** Use `nomic-embed-text` for vector search
- **Email Confirmation:** Require email verification on registration
- **2FA / MFA:** Add TOTP-based two-factor authentication
