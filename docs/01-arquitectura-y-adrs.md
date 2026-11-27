# Arquitectura del Sistema y ADRs — MesaAyuda IA (Ofima S.A.S.)

## 1. Diagramas C4 de Arquitectura

### Nivel 1: Contexto del Sistema

```mermaid
flowchart LR
    Cliente["Cliente ERP\n(Contabilidad / Tesorería / Nómina)"]
    Analista["Analista de Soporte Ofima\n(Triage, IA y Aprobación)"]
    Sistema["MesaAyuda IA\n(Angular 18 + .NET 8 + FastAPI RAG)"]
    Corpus["Corpus ERP Ofima\n(/corpus: .md / .txt / .pdf)"]
    LLM["Proveedor LLM Strategy\n(Mock Local / Gemini / OpenAI)"]

    Cliente -->|"Crea casos, deflexión RAG, SLA y CSAT"| Sistema
    Analista -->|"Gestiona flujo, streaming SSE e ingesta KB"| Sistema
    Corpus -->|"Indexación vectorial + léxica BM25"| Sistema
    Sistema -->|"Generación anclada con fallback"| LLM
```

### Nivel 2: Contenedores (Local Docker Compose vs. Producción Azure)

#### Topología Local (`docker-compose.yml` — Red `mesaayuda-net`)

```mermaid
flowchart TB
    Browser["Navegador Web"] -->|"HTTP :4200 + X-Correlation-ID"| FE["Frontend Container\nAngular 18 + Nginx SPA & Proxy /api (SSE sin buffering, 10m)"]
    FE -->|"HTTP :8080 (interno) / :5080 (host)"| API["Backend Container\n.NET 8 API + CorrelationId + RateLimit 20/min IA + Polly"]
    API -->|"TCP :5432 (EF Core + Npgsql)"| PG[("PostgreSQL 16 Alpine\nVolumen pgdata")]
    API -->|"HTTP :8000 + X-Correlation-ID"| RAG["RAG Service Container\nPython 3.11 FastAPI + ChromaDB + BM25"]
    RAG -->|"Lectura / Escritura"| CorpusVol[("Corpus ERP\n./corpus")]
```

#### Arquitectura Objetivo en Microsoft Azure (Producción Zero-Trust)

```mermaid
flowchart TB
    Users["Usuarios / Entra ID OIDC+PKCE"] --> AFD["Azure Front Door + WAF"]
    AFD --> SWA["Azure Static Web Apps (Angular 18)"]
    AFD --> ACA_API["Azure Container Apps (.NET 8 API + RateLimit)"]
    ACA_API --> AZ_PG[("Azure PostgreSQL Flexible + RLS Multi-Tenant")]
    ACA_API --> SAS["Azure Blob Storage + SAS Tokens (Adjuntos)"]
    ACA_API --> SB["Azure Service Bus + Blob Ingesta Asíncrona"]
    SB --> ACA_RAG["Azure Container Apps (FastAPI RAG / Agente MCP)"]
    ACA_API -->|"X-Correlation-ID + Polly"| ACA_RAG
    ACA_RAG --> AZ_VEC[("Azure AI Search Multi-Tenant")]
    ACA_RAG --> DLP["Azure AI Content Safety (DLP / PII)"]
    DLP --> AZ_LLM["Azure OpenAI / Function Calling ERP"]
    ACA_API -.-> KV["Azure Key Vault"]
    ACA_API -.-> AI["App Insights + OpenTelemetry"]
    ACA_RAG -.-> AI
```

**Evolución a producción Azure:** ingesta documental desacoplada vía **Blob Storage + Service Bus**, adjuntos en Blob con **SAS Tokens** efímeros, aislamiento **PostgreSQL RLS + Azure AI Search**, identidad Zero-Trust con **Entra ID + Key Vault + Content Safety (DLP)**, trazabilidad `X-Correlation-ID` y evolución de RAG consultivo a **Agente ERP con Function Calling / MCP**.

---

## 2. Architecture Decision Records (ADRs)

### ADR-001: Clean Architecture en .NET 8 + Polly, Correlación y Rate Limiting FinOps

- **Contexto:** El núcleo transaccional exige RBAC JWT, aislamiento por cliente, máquina de estados, SLA/CSAT, trazabilidad distribuida hacia Python y protección ante abuso o picos de costo IA.
- **Decisión:** Estructurar la solución en 4 capas (`Domain`, `Application`, `Infrastructure`, `Api`) con `CorrelationIdMiddleware` + `CorrelationIdDelegatingHandler` (`X-Correlation-ID` + `ILogger.BeginScope`), **Rate Limiting** nativo (`"AiEndpointsPolicy"`, `20 req/min` con `429 ProblemDetails` sobre endpoints RAG), `ProblemDetails` RFC 7807 y Polly v8 (`15s`, `2` reintentos con jitter y Circuit Breaker).
- **Alternativas descartadas:** *N-capas tradicional* (modelos anémicos) y *gateways externos pesados* en MVP local.
- **Consecuencias:** (+) Trazabilidad punta a punta Angular ➔ .NET ➔ FastAPI y contención inmediata de consumo FinOps sin paquetes externos. (-) Requiere calibrar partición de ventana fija al escalar múltiples réplicas (migrar a Redis Rate Limiting en Azure).

### ADR-002: ChromaDB + Búsqueda Híbrida (`0.70` Vectorial + `0.30` BM25) y Chunking por Secciones (`650 / 120`)

- **Contexto:** Los manuales ERP combinan procedimientos secuenciales con códigos exactos (`FAC-402`, `CON-301`, `INV-108`, `SEC-423`, `999905`). La búsqueda vectorial pura pierde exactitud en códigos alfanuméricos, mientras que el chunking ciego corta pasos operativos.
- **Decisión:** Combinar similitud coseno (`all-MiniLM-L6-v2` en ChromaDB, peso `0.70`) con `BM25Okapi` (peso `0.30`) preservando códigos con guion, tras chunking por encabezados (`##`/`###`) con `chunk_size=650`, `chunk_overlap=120` y `similarity_threshold=0.35`.
- **Alternativas descartadas:** *Solo búsqueda vectorial* (baja precisión en códigos exactos) y *chunks > 1500 caracteres* (dilución semántica y mayor gasto de tokens).
- **Consecuencias:** (+) **100.00%** en Hit Rate @ 4, Faithfulness y rechazo fuera de dominio (`evaluacion-rag.md`). (-) Sensible a documentos sin estructura clara de encabezados.

### ADR-003: Microservicio RAG Desacoplado en Python / FastAPI frente a Semantic Kernel Embebido en .NET

- **Contexto:** La generación de embeddings locales (`sentence-transformers`), lectura PDF (`pypdf`) y evaluación cuantitativa son nativas del ecosistema Python, además de requerir modo `mock` gratuito intercambiable con Gemini/OpenAI.
- **Decisión:** Aislar la recuperación semántica en un servicio **FastAPI** dedicado con patrón **Strategy** (`LLMProvider`) y caché LRU de embeddings, consumido desde .NET detrás de Polly y gateway RBAC (`DocumentosController`).
- **Alternativas descartadas:** *Semantic Kernel embebido en .NET* (acoplaría ciclos de despliegue y exigiría runtimes ONNX manuales o dependencia obligatoria de APIs pagas).
- **Consecuencias:** (+) Escalado independiente en CPU/GPU e ingesta dinámica de manuales sin reiniciar el backend transaccional. (-) Requiere contrato HTTP estable (`SnakeCaseLower`) y timeouts calibrados.
