# MesaAyuda IA — Plataforma de Soporte Empresarial con RAG (Ofima S.A.S.)

Plataforma empresarial de mesa de ayuda inteligente para **Ofima ERP**, diseñada con arquitectura desacoplada de 3 piezas, búsqueda híbrida documental (Vectorial + Léxica BM25), control de acceso por roles con JWT, resiliencia distribuida con Polly y aprobación humana obligatoria antes de publicar sugerencias de IA.

## Visión Ejecutiva y Stack Tecnológico

| Componente | Tecnología | Puerto (Host -> Contenedor) | Responsabilidad Principal |
| :--- | :--- | :---: | :--- |
| **Frontend Web** | Angular 18+ (Standalone + Signals + Nginx) | `4200:80` | Portal Split-Screen, diseño Claro/Oscuro + máscaras, flujo de tickets y proxy `/api/` |
| **Backend Core** | .NET 8 Clean Architecture + EF Core + Polly | `5080:8080` | Autenticación JWT, máquina de estados de casos, `ProblemDetails` y cliente RAG resiliente |
| **Servicio RAG** | Python 3.11 FastAPI + ChromaDB + BM25 | `8000:8000` | Ingesta del corpus ERP, búsqueda híbrida, mitigación de *prompt injection* y Strategy LLM |
| **Base de Datos** | PostgreSQL 16 Alpine | `5432:5432` | Persistencia relacional de usuarios, casos, respuestas y fuentes citadas (`pgdata`) |

### Patrones de Arquitectura y Diseño Aplicados

- **Clean Architecture & DDD Táctico (.NET 8):** separación en `Domain`, `Application`, `Infrastructure` y `Api`, entidades ricas (`Caso`, `Respuesta`) que encapsulan invariantes y máquina de estados, `ICasoRepository` + `IUnitOfWork`, validación con FluentValidation y manejo global RFC 7807 (`IExceptionHandler` + `ProblemDetails`).
- **Resiliencia Distribuida:** `Microsoft.Extensions.Http.Resilience` (Polly v8) con timeout por intento de `8s`, `2` reintentos con *exponential backoff* + *jitter*, Circuit Breaker y degradación controlada si el servicio RAG no responde.
- **Patrón Strategy & Caché LRU (Python):** proveedores intercambiables (`GroundedExtractiveMockProvider`, `GeminiLLMProvider`, `OpenAILLMProvider`) con fallback automático, anclaje estricto por umbral (`0.35`) y caché LRU para embeddings de consulta.
- **Angular Signals & CSS Custom Properties:** estado reactivo sin sobrecarga de Redux, lazy loading por ruta (`loadComponent`), interceptor funcional JWT y motor de temas Claro/Oscuro con 3 máscaras predefinidas y creador personalizado.

## Inicio Rápido (Un Solo Comando)

Con Docker y Docker Compose instalados, ejecuta desde la raíz del proyecto:

```bash
cp .env.example .env && docker compose up --build
```

### URLs de Acceso Local

- **Portal Web (Angular):** `http://localhost:4200`
- **API .NET & Swagger UI:** `http://localhost:5080/swagger` (Health check: `http://localhost:5080/health`)
- **Microservicio RAG (FastAPI Docs):** `http://localhost:8000/docs` (Health check: `http://localhost:8000/health`)
- **PostgreSQL:** `localhost:5432`

### Usuarios de Demostración (Precargados Automáticamente)

Al arrancar, el backend ejecuta las migraciones (`InitialCreate`) y siembra 3 cuentas listas para probar ambos roles (también disponibles como botones de **1 clic** en la pantalla de login):

| Rol | Nombre | Correo electrónico | Contraseña |
| :--- | :--- | :--- | :--- |
| **Analista** | Santiago Graciano - Analista Soporte | `analista@ofima.com` | `Ofima2026*` |
| **Cliente** | Carlos Mendoza - Contabilidad | `cliente@empresa.com` | `Ofima2026*` |
| **Cliente** | Laura Gómez - Tesorería | `cliente2@empresa.com` | `Ofima2026*` |

## Variables de Entorno

| Variable | Valor por defecto | Descripción |
| :--- | :--- | :--- |
| `POSTGRES_DB` | `mesaayuda` | Nombre de la base de datos en PostgreSQL. |
| `POSTGRES_USER` | `mesaayuda_user` | Usuario de conexión a PostgreSQL. |
| `POSTGRES_PASSWORD` | `mesaayuda_pass_2026` | Contraseña del usuario de PostgreSQL. |
| `JWT_SECRET` | `SuperSecretKeyForMesaAyudaIA2026OfimaTechLead!` | Clave secreta para firmar tokens JWT en el backend. |
| `JWT_ISSUER` | `MesaAyuda.Api` | Emisor (`iss`) esperado en los tokens JWT. |
| `JWT_AUDIENCE` | `MesaAyuda.Client` | Audiencia (`aud`) esperada en los tokens JWT. |
| `RAG_SERVICE_URL` | `http://rag-service:8000` | URL interna del microservicio RAG dentro de `mesaayuda-net`. |
| `LLM_PROVIDER` | `mock` | Proveedor de generación de respuestas (`mock`, `gemini`, `openai`). |
| `GEMINI_API_KEY` | *(vacío)* | Clave de API de Google Gemini (opcional si `LLM_PROVIDER=gemini`). |
| `OPENAI_API_KEY` | *(vacío)* | Clave de API de OpenAI (opcional si `LLM_PROVIDER=openai`). |
| `EMBEDDING_MODEL` | `all-MiniLM-L6-v2` | Modelo de embeddings utilizado para vectorizar el corpus. |
| `CHUNK_SIZE` | `650` | Tamaño máximo de fragmento de texto durante la indexación. |
| `CHUNK_OVERLAP` | `120` | Solapamiento entre fragmentos consecutivos para preservar contexto. |
| `SIMILARITY_THRESHOLD` | `0.35` | Umbral mínimo de similitud para considerar relevante un fragmento recuperado. |

## Resultados de Evaluación RAG

La batería automatizada sobre los **6 manuales de Ofima ERP** (`86` fragmentos indexados con búsqueda híbrida `0.70 Vector Coseno + 0.30 BM25Okapi`) y el dataset de 10 casos (`8` en dominio + `2` fuera de dominio) obtuvo los siguientes resultados empíricos (ver reporte completo en [evaluacion-rag.md](docs/evaluacion-rag.md)):

| Métrica | Resultado Obtenido |
| :--- | :---: |
| **Retrieval Hit Rate @ 4** | **100.00%** |
| **Answer Groundedness / Keyword Faithfulness** | **100.00%** |
| **Out-of-Domain Rejection Accuracy** | **100.00%** |
| **Latencia Promedio por Consulta** | `13.75 ms` |

## Pruebas Automatizadas e Integración Continua

- **Python RAG (`rag-service`):** `pytest -v` -> **6 passed** (chunking con metadatos, recuperación y citación `FAC-402`, rechazo fuera de dominio, bloqueo HTTP `400` de *prompt injection*, endpoints `/health` y `/ingesta`, caché LRU).
- **.NET 8 Backend (`backend`):** `dotnet test backend/MesaAyuda.sln` -> **10 passed** (**6** pruebas unitarias de dominio/aplicación + **4** pruebas de integración HTTP end-to-end con `WebApplicationFactory` y EF Core InMemory).
- **Angular 18 Frontend (`frontend`):** `npm test` (Jasmine/Karma en Chrome Headless) -> **4 passed** (`AuthService` con `HttpTestingController` y Signals + `CasosListComponent` con badges y eventos de filtro/selección).
- **GitHub Actions (`.github/workflows/ci.yml`):** ejecuta los 3 pipelines en paralelo ante cada `push` y `pull_request` a `main`.

## Documentación Técnica y Arquitectura

- [Arquitectura C4 (Docker Compose y Microsoft Azure) y ADRs](docs/01-arquitectura-y-adrs.md)
- [Plan de Evolución a 3 Meses, Estándares, Riesgos de IA y Bitácora](docs/02-plan-estandares-riesgos-bitacora.md)
- [Evaluación Empírica del Motor RAG (10 Casos)](docs/evaluacion-rag.md)

## Declaración de Uso de Asistentes de IA

En el desarrollo de este proyecto se utilizaron **Gemini** y **Antigravity** (IDE agéntico) de forma transparente y gobernada bajo el principio *Human-in-the-Loop*:

- **Alcance asistido:** generación incremental de scaffolding (estructura Clean Architecture .NET 8, endpoints FastAPI, componentes Angular Standalone), redacción inicial del corpus documental sintético estilo Ofima ERP, creación de suites de pruebas (`pytest`, `xUnit`, `Jasmine/Karma`) y diagramación Mermaid C4.
- **Supervisión, criterio e ingeniería humana:** definición de decisiones arquitectónicas (ADRs), validación de reglas de negocio colombianas (DIAN, CUNE, PUC, incapacidad), calibración empírica del buscador híbrido (eliminación de fragmentos de solo encabezado, filtrado de stopwords en español manteniendo códigos como `CON-301` y atenuación de piso coseno para rechazar consultas fuera de dominio), revisión de seguridad JWT/RBAC/Prompt Injection y verificación end-to-end mediante compilación, pruebas automatizadas y despliegue con Docker Compose.
- **Privacidad:** no se utilizaron datos reales, credenciales productivas ni información sensible de clientes (PII) en ningún prompt o ejemplo.
