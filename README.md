# SecurePlatform — Feature Deep-Dive

This document explains **why** each feature was built, **how** it works under the hood, and the **trade-offs** involved.

---

## Table of Contents

1. [Streaming SSE (Server-Sent Events)](#1-streaming-sse-server-sent-events)
2. [Embeddings](#2-embeddings)
3. [In-Memory Vector Store & RAG Pipeline](#3-in-memory-vector-store--rag-pipeline)
4. [Model Switching](#4-model-switching)
5. [Email Confirmation](#5-email-confirmation)

---

## 1. Streaming SSE (Server-Sent Events)

### Why is it in the feature list?

When you send a prompt to an LLM like Llama 3, the model generates tokens **one at a time**. Without streaming, the API waits until the entire response is generated (which can take 5–30 seconds) and then sends it all at once. The user stares at a blank screen the whole time.

**Streaming SSE** pushes each token to the browser **the instant it's generated**, so the user sees text appearing word-by-word — exactly like ChatGPT.

### How it works (step by step)

```
Browser                    ASP.NET API                    Ollama (llama3.2:1b)
  │                            │                                │
  │  POST /api/ai/stream       │                                │
  │  { "prompt": "..." }       │                                │
  │ ──────────────────────────>│                                │
  │                            │  GenerateAsync(stream: true)   │
  │                            │ ──────────────────────────────>│
  │                            │       token "The"              │
  │                            │ <──────────────────────────────│
  │   data: The\n\n            │                                │
  │ <──────────────────────────│                                │
  │                            │       token " cat"             │
  │                            │ <──────────────────────────────│
  │   data:  cat\n\n           │                                │
  │ <──────────────────────────│                                │
  │          ...               │           ...                  │
  │   data: [DONE]\n\n        │                                │
  │ <──────────────────────────│                                │
```

1. The client sends a POST to `/api/ai/stream` with the prompt.
2. The controller sets `Content-Type: text/event-stream` and disables caching.
3. Ollama streams tokens back through OllamaSharp's `IAsyncEnumerable<GenerateResponseStream>`.
4. Our `StreamCompletionAsync` method `yield return`s each token.
5. The controller writes each token as an SSE frame (`data: <token>\n\n`) and flushes the response body.
6. When the model finishes, we send `data: [DONE]\n\n` to signal completion.

### Key code

```csharp
// AiService.cs — yields tokens one-by-one
public async IAsyncEnumerable<string> StreamCompletionAsync(string prompt, CancellationToken ct)
{
    var stream = _client.GenerateAsync(new GenerateRequest
    {
        Model = _currentModel,
        Prompt = prompt,
        Stream = true       // ← tells Ollama to stream
    }, ct);

    await foreach (var chunk in stream.WithCancellation(ct))
    {
        if (chunk?.Response is not null)
            yield return chunk.Response;   // ← one token at a time
    }
}
```

```csharp
// AiController.cs — writes SSE frames
Response.ContentType = "text/event-stream";
await foreach (var token in _aiService.StreamCompletionAsync(request.Prompt, ct))
{
    await Response.WriteAsync($"data: {token}\n\n", ct);
    await Response.Body.FlushAsync(ct);  // ← push immediately, don't buffer
}
await Response.WriteAsync("data: [DONE]\n\n", ct);
```

### Trade-offs

| Aspect | Benefit | Cost |
|--------|---------|------|
| **User experience** | Text appears instantly, feels responsive | — |
| **Connection lifetime** | — | HTTP connection stays open for the entire generation (seconds to minutes). If the server is behind a reverse proxy with a short timeout (e.g., 30 s), the stream can be cut |
| **Error handling** | — | If the model crashes mid-stream, the client gets a partial response with no proper error JSON — just a broken SSE stream |
| **SSE vs WebSocket** | SSE is simpler (just HTTP), no handshake, works through most proxies, browser `EventSource` API is trivial | SSE is **unidirectional** (server → client only). If you need the user to cancel mid-stream or send follow-up messages on the same connection, you need WebSocket or SignalR |
| **Memory** | Data is not buffered on the server — each token is flushed and discarded | Each open stream is a held HTTP connection, limiting concurrency |
| **Caching** | — | Streaming responses can't be cached by CDNs or browser cache |

### Why SSE instead of WebSocket/SignalR?

- SSE uses plain HTTP — no protocol upgrade, no special server config.
- It's the same approach used by OpenAI's API (`stream: true`).
- Simpler to implement and debug (you can test with `curl`).
- If we later need bidirectional communication, we can add SignalR alongside SSE (it's in our roadmap).

---

## 2. Embeddings

### What is an embedding?

An embedding is a **dense numerical vector** (array of floats) that represents the *meaning* of a piece of text. Texts with similar meanings have vectors that point in similar directions.

```
"The cat sat on the mat"  →  [0.12, -0.45, 0.78, ..., 0.33]   (768 floats)
"A kitten is on the rug"  →  [0.11, -0.44, 0.79, ..., 0.31]   (very similar!)
"Stock market crashed"    →  [-0.88, 0.22, -0.15, ..., 0.67]  (very different)
```

### Why do we need them?

Embeddings are the **foundation of RAG** (Retrieval-Augmented Generation). They let us:

1. **Store** document chunks as vectors in a vector store.
2. **Search** by meaning (not keywords). When the user asks a question, we embed the question and find the most similar stored chunks.
3. **Give context to the LLM** so it can answer based on your actual data instead of hallucinating.

### Which model?

We use **`nomic-embed-text`** (~274 MB), an open embedding model that runs locally on Ollama.

| Property | Value |
|----------|-------|
| Model | `nomic-embed-text` |
| Dimensions | 768 floats per vector |
| Max tokens | ~8192 |
| Runs on | Ollama (local, no API key needed) |

### How it works in code

```csharp
// AiService.cs
public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
{
    var response = await _client.EmbedAsync(new EmbedRequest
    {
        Model = _settings.EmbeddingModel,   // "nomic-embed-text"
        Input = [text]
    }, ct);

    return response?.Embeddings?.FirstOrDefault()?.ToArray() ?? [];
}
```

The API call is: `POST http://localhost:11434/api/embed` with the model name and input text. Ollama returns a 768-dimension float array.

### Trade-offs

| Aspect | Benefit | Cost |
|--------|---------|------|
| **Privacy** | Runs 100% locally — no data leaves your machine | — |
| **Cost** | Free, no API keys | — |
| **Quality** | Good for general English text | Not as strong as OpenAI `text-embedding-3-large` or Cohere on benchmarks |
| **Speed** | ~50–200 ms per embedding on CPU | Slow on large document batches (1000+ chunks). GPU helps significantly |
| **Dimensions** | 768-D gives decent retrieval accuracy | Higher-dimension models (1536-D, 3072-D) can capture more nuance |

---

## 3. In-Memory Vector Store & RAG Pipeline

### Which vector store?

We use a **custom in-memory vector store** — a `ConcurrentBag<(string Text, float[] Embedding)>` with manual cosine similarity search.

### Why in-memory instead of a database?

This is a deliberate **MVP / learning choice**. Here's the trade-off matrix:

| Aspect | In-Memory (our choice) | Qdrant / ChromaDB / Pinecone |
|--------|----------------------|------------------------------|
| **Setup** | Zero config, no extra service | Need to install and run a separate server |
| **Code complexity** | ~40 lines of C# | SDK integration, connection management |
| **Speed (small data)** | Extremely fast (<1 ms) | Slightly slower due to network hop |
| **Speed (large data)** | O(n) brute-force scan — slows with 10K+ docs | Uses HNSW/IVF indexes — stays fast at millions of docs |
| **Persistence** | **Lost on app restart** | Persisted to disk |
| **Scalability** | Single-server, RAM-limited | Distributed, horizontal scaling |
| **Filtering** | Not supported | Metadata filters, namespaces |

**Bottom line:** Our in-memory store works great for development and demos (up to ~1000 chunks). For production, you'd swap in Qdrant or ChromaDB.

### How the RAG pipeline works (full flow)

```
┌──────────────────────────────────────────────────────────────┐
│                    INGEST PHASE (one-time)                    │
│                                                              │
│  Document Text                                               │
│       │                                                      │
│       ▼                                                      │
│  ┌──────────┐     ┌───────────┐     ┌──────────────────┐    │
│  │  Chunk   │────>│  Embed    │────>│  Store in Vector │    │
│  │  (500ch) │     │  each one │     │  Store (RAM)     │    │
│  └──────────┘     └───────────┘     └──────────────────┘    │
│                                                              │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                    QUERY PHASE (per question)                 │
│                                                              │
│  User Question: "What is the password policy?"               │
│       │                                                      │
│       ▼                                                      │
│  ┌───────────┐                                               │
│  │  Embed    │  → [0.12, -0.45, ...]                         │
│  │  Question │                                               │
│  └───────────┘                                               │
│       │                                                      │
│       ▼                                                      │
│  ┌───────────────────┐                                       │
│  │  Cosine Similarity│  Compare question vector              │
│  │  Search (top 3)   │  against ALL stored vectors           │
│  └───────────────────┘                                       │
│       │                                                      │
│       ▼                                                      │
│  ┌───────────────────────────────────────────────┐           │
│  │  Build RAG Prompt:                            │           │
│  │  "Context: <top 3 chunks>                     │           │
│  │   Question: What is the password policy?"     │           │
│  └───────────────────────────────────────────────┘           │
│       │                                                      │
│       ▼                                                      │
│  ┌──────────┐     ┌──────────────────┐                       │
│  │  LLM     │────>│  Answer based on │                       │
│  │  (Llama) │     │  actual context   │                       │
│  └──────────┘     └──────────────────┘                       │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

#### Step 1: Ingest (`POST /api/ai/ingest`)

```csharp
// Split into ~500-character chunks by paragraph boundaries
var chunks = ChunkText(request.Text, 500);

foreach (var chunk in chunks)
{
    var embedding = await _aiService.GetEmbeddingAsync(chunk, ct);  // 768-D vector
    _vectorStore.Add(chunk, embedding);  // store in ConcurrentBag
}
```

#### Step 2: Query (`POST /api/ai/ask`)

```csharp
// 1. Embed the user's question
var queryEmbedding = await GetEmbeddingAsync(question, ct);

// 2. Find the 3 most similar chunks
var relevantChunks = _vectorStore.Search(queryEmbedding, topK: 3);

// 3. Build an augmented prompt
var ragPrompt = $"""
    Context:
    {string.Join("\n\n", relevantChunks)}

    Question: {question}
    """;

// 4. Send augmented prompt to LLM
return await GetCompletionAsync(ragPrompt, ct);
```

#### Step 3: Cosine Similarity (the math)

Two vectors are similar if the angle between them is small. Cosine similarity is:

$$\text{cosine\_similarity}(\vec{a}, \vec{b}) = \frac{\vec{a} \cdot \vec{b}}{|\vec{a}| \times |\vec{b}|}$$

- Result of **1.0** = identical meaning
- Result of **0.0** = unrelated
- Result of **-1.0** = opposite meaning

Our implementation:

```csharp
private static float CosineSimilarity(float[] a, float[] b)
{
    float dot = 0f, normA = 0f, normB = 0f;
    for (int i = 0; i < a.Length; i++)
    {
        dot   += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
}
```

---

## 4. Model Switching

### Why?

Different models excel at different tasks:

| Model | Size | Best for |
|-------|------|----------|
| `llama3.2:1b` | 1.3 GB | Fast responses, simple Q&A |
| `llama3.2:3b` | 2 GB | Better reasoning, still fast |
| `codellama:7b` | 3.8 GB | Code generation |
| `mistral:7b` | 4.1 GB | General-purpose |

The `/api/ai/models/switch` endpoint lets the user change the active model at runtime **without restarting the server**.

### How it works

```csharp
// AiService.cs — simple field swap
private string _currentModel;  // starts as "llama3.2:1b" from config

public Task SetModelAsync(string modelName, CancellationToken ct)
{
    _currentModel = modelName;  // all future completions use this model
    return Task.CompletedTask;
}
```

The controller validates that the model actually exists locally before switching:

```csharp
var available = await _aiService.ListModelsAsync(ct);
if (!available.Contains(request.Model))
    return BadRequest("Model not available locally. Pull it first.");
```

### Trade-off

- **Benefit:** Quickly switch between models for experimentation.
- **Cost:** The `_currentModel` field is per-service-instance. In a multi-server deployment, switching on one server doesn't affect others. A proper solution would store the active model in Redis or a database.

---

## 5. Email Confirmation

### Why?

Without email confirmation, anyone can register with a fake email (e.g., `president@whitehouse.gov`). This means:
- You can't reliably send password reset emails.
- You can't trust the identity behind an account.
- Spam bots can create unlimited accounts.

Email confirmation proves the user **actually owns the email address** they registered with.

### How it works behind the scenes (full lifecycle)

```
┌──────────────────────────────────────────────────────────────┐
│                     REGISTRATION FLOW                        │
│                                                              │
│  User                 API                  Identity    Email  │
│   │                    │                      │          │   │
│   │  POST /register    │                      │          │   │
│   │  {email,password}  │                      │          │   │
│   │ ──────────────────>│                      │          │   │
│   │                    │  CreateAsync(user)    │          │   │
│   │                    │ ────────────────────>│          │   │
│   │                    │                      │          │   │
│   │                    │  GenerateEmailConfirmationToken  │   │
│   │                    │ ────────────────────>│          │   │
│   │                    │  <token>              │          │   │
│   │                    │ <────────────────────│          │   │
│   │                    │                      │          │   │
│   │                    │  SendConfirmationEmail(token)    │   │
│   │                    │ ───────────────────────────────>│   │
│   │                    │                      │          │   │
│   │  "Check your email"│                      │          │   │
│   │ <──────────────────│                      │          │   │
│   │                    │                      │          │   │
│   │  ⚠️ Login BLOCKED  │                      │          │   │
│   │  until confirmed   │                      │          │   │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                    CONFIRMATION FLOW                          │
│                                                              │
│  User                 API                  Identity           │
│   │                    │                      │              │
│   │  POST /confirm-email                      │              │
│   │  {email, token}    │                      │              │
│   │ ──────────────────>│                      │              │
│   │                    │  ConfirmEmailAsync   │              │
│   │                    │  (user, token)       │              │
│   │                    │ ────────────────────>│              │
│   │                    │                      │              │
│   │                    │  Sets EmailConfirmed │              │
│   │                    │  = true in DB        │              │
│   │                    │ <────────────────────│              │
│   │                    │                      │              │
│   │                    │  Generate JWT + Refresh Token       │
│   │  {tokens, user}   │  (auto-login)        │              │
│   │ <──────────────────│                      │              │
│   │                    │                      │              │
│   │  ✅ Can now login   │                      │              │
│   │  normally           │                      │              │
└──────────────────────────────────────────────────────────────┘
```

### Step-by-step behind the scenes

#### 1. Registration — Token Generation

```csharp
// AuthService.cs — RegisterAsync
var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
```

Behind the scenes, ASP.NET Core Identity:
- Uses the `DataProtectorTokenProvider` (configured by `AddDefaultTokenProviders()`)
- Creates a **data-protection token** containing: the user ID, the purpose ("EmailConfirmation"), and a security stamp
- The security stamp is a random GUID stored in the `AspNetUsers.SecurityStamp` column — if it changes (e.g., password change), all outstanding tokens are automatically invalidated
- The token is Base64-encoded and looks like: `CfDJ8Nz3g...long string...==`

#### 2. Email Sending

```csharp
await _emailService.SendEmailConfirmationAsync(user.Email!, confirmToken);
```

In **development mode** (`DevEmailService`), the token is logged to the console:
```
[EMAIL-CONFIRMATION] To: user@example.com | Token: CfDJ8Nz3g...
```

In **production** (`SmtpEmailService`), an HTML email is sent via SMTP with the token.

#### 3. Identity Config — Requires Confirmed Email

```csharp
// DependencyInjection.cs
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;  // ← the gate
});
```

This setting means ASP.NET Identity's `SignInManager` will refuse to sign in users where `EmailConfirmed == false`.

We also enforce it manually in `LoginAsync`:

```csharp
if (!user.EmailConfirmed)
    return AuthResponse.Failure(AuthResultType.EmailNotConfirmed,
        "Please confirm your email before logging in.");
```

#### 4. Token Verification

```csharp
// AuthService.cs — ConfirmEmailAsync
var result = await _userManager.ConfirmEmailAsync(user, token);
```

Behind the scenes, Identity:
1. **Decodes** the Base64 token using the Data Protection API
2. **Validates** the purpose matches "EmailConfirmation"
3. **Compares** the security stamp in the token with the one in the database
4. If valid, sets `user.EmailConfirmed = true` and saves to DB
5. Returns `IdentityResult.Success`

#### 5. Auto-Login After Confirmation

After successful confirmation, we automatically generate JWT + refresh token and return them — so the user doesn't have to login again manually.

### Security considerations

| Concern | How we handle it |
|---------|-----------------|
| **Token expiration** | Default Identity token lifetime is 1 day (configurable via `DataProtectionTokenProviderOptions.TokenLifespan`) |
| **Token replay** | After confirmation, the security stamp is unchanged so the same token could theoretically be reused — but `ConfirmEmailAsync` checks if already confirmed and returns immediately |
| **Email enumeration** | `ResendConfirmationEmailAsync` returns the same generic message whether the email exists or not |
| **Brute force** | Tokens are cryptographically generated (Data Protection API) — not guessable 6-digit codes |

---

## Summary: Why These 5 Features?

| Feature | Problem it solves | One-line summary |
|---------|------------------|------------------|
| **Streaming SSE** | User waits 10+ seconds staring at nothing | Push tokens word-by-word as they're generated |
| **Embeddings** | Need to compare text by *meaning*, not keywords | Convert text → 768-dimension float vector |
| **Vector Store (RAG)** | LLM hallucinates without context | Store your docs, retrieve relevant chunks, feed them to the LLM |
| **Model Switching** | One model doesn't fit all tasks | Swap models at runtime without restart |
| **Email Confirmation** | Fake accounts, unreachable emails | Prove email ownership before allowing login |
