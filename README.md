# MesaAyuda IA — Plataforma de Soporte Empresarial con RAG (Ofima S.A.S.)

**MesaAyuda IA** es una plataforma integral de mesa de ayuda inteligente para **Ofima ERP**, construida con arquitectura desacoplada de 4 contenedores Docker, recuperación documental híbrida (Vectorial + Léxica BM25), control de acceso por roles con JWT, resiliencia distribuida y validación humana obligatoria (*Human-in-the-Loop*) antes de publicar respuestas asistidas por IA.

---

## 1. Resumen Ejecutivo y Arquitectura

| Componente | Tecnología | Puerto (Host -> Contenedor) | Responsabilidad Principal |
| :--- | :--- | :---: | :--- |
| **Frontend Web** | Angular 18+ (Standalone + Signals + Nginx) | `4200:80` | Portal Split-Screen Claro/Oscuro + máscaras, `X-Correlation-ID`, SLA visual, deflexión RAG, CSAT, streaming SSE y proxy `/api/` |
| **Backend Core** | .NET 8 Clean Architecture + EF Core + Polly | `5080:8080` | API RESTful JWT (`Cliente` / `Analista`), correlación `X-Correlation-ID`, Rate Limiting IA (`20/min`), SLA, CSAT, SSE y FinOps |
| **Servicio RAG** | Python 3.11 FastAPI + ChromaDB + BM25 | `8000:8000` | Ingesta de `.md`/`.txt`/`.pdf`, búsqueda híbrida, logs estructurados por `X-Correlation-ID`, anti-*prompt injection* y Strategy LLM |
| **Base de Datos** | PostgreSQL 16 Alpine | `5432:5432` | Persistencia relacional con volumen `pgdata` y migraciones automáticas de EF Core |

### Patrones de Diseño y Arquitectura Implementados
- **.NET 8 Clean Architecture:** separación estricta en `MesaAyuda.Domain`, `MesaAyuda.Application`, `MesaAyuda.Infrastructure` y `MesaAyuda.Api`, con entidades ricas (`Caso`, `Respuesta`), patrones **Repository**, **Unit of Work**, **Dependency Injection**, **Options**, FluentValidation, `ProblemDetails` RFC 7807 y `Microsoft.Extensions.Http.Resilience` (Polly v8: `15s` por intento, `2` reintentos con jitter y Circuit Breaker).
- **Angular 18 Moderno:** Standalone Components, Angular Signals, lazy loading por ruta (`loadComponent`), interceptor funcional JWT, guards de autenticación, debounce RxJS (`700 ms` deflexión / `300 ms` búsqueda) y temas dinámicos CSS Custom Properties.
- **Python FastAPI RAG Híbrido:** búsqueda `0.70 Vector Coseno (all-MiniLM-L6-v2) + 0.30 BM25Okapi`, chunking por encabezados (`650 / 120`), umbral estricto (`0.35`), caché LRU de embeddings y patrón **Strategy** (`GroundedExtractiveMockProvider`, `GeminiLLMProvider`, `OpenAILLMProvider`) con fallback automático gratuito.

---

## 2. Inicio Rápido (Un Solo Comando)

Con **Docker** y **Docker Compose** instalados, ejecuta desde la raíz del repositorio:

```bash
cp .env.example .env && docker compose up --build
```

Para detener los servicios (`--volumes` opcional si deseas reiniciar la base de datos):

```bash
docker compose down
```

### URLs de Acceso y Monitoreo
- **Portal Web (Angular):** `http://localhost:4200`
- **API .NET & Swagger UI:** `http://localhost:5080/swagger` (Health check: `http://localhost:5080/health`)
- **Microservicio RAG (FastAPI Docs):** `http://localhost:8000/docs` (Health check: `http://localhost:8000/health`)
- **PostgreSQL:** `localhost:5432`

### Usuarios de Demostración (Precargados Automáticamente)
Al arrancar, el backend aplica las migraciones EF Core y siembra 3 cuentas de prueba (accesibles también con **1 clic** desde la pantalla de login):

| Rol | Nombre | Correo electrónico | Contraseña |
| :--- | :--- | :--- | :--- |
| **Analista** | Santiago Graciano - Analista Soporte | `analista@ofima.com` | `Ofima2026*` |
| **Cliente** | Carlos Mendoza - Contabilidad | `cliente@empresa.com` | `Ofima2026*` |
| **Cliente** | Laura Gómez - Tesorería | `cliente2@empresa.com` | `Ofima2026*` |

### Variables de Entorno (`.env.example`)
El archivo [`.env.example`](.env.example) centraliza sin exponer secretos reales las variables de PostgreSQL (`POSTGRES_*`), JWT (`JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`), proveedor LLM (`RAG_SERVICE_URL`, `LLM_PROVIDER=mock|gemini|openai`, `GEMINI_API_KEY`, `OPENAI_API_KEY`) y parámetros de ChromaDB/chunking (`CORPUS_PATH`, `CHROMA_PERSIST_DIR`, `COLLECTION_NAME`, `EMBEDDING_MODEL`, `CHUNK_SIZE`, `CHUNK_OVERLAP`, `SIMILARITY_THRESHOLD`). Para producción en Azure o despliegue privado, puede extenderse el Strategy hacia Azure OpenAI u Ollama self-hosted.

---

## 3. Declaración Explícita del Corpus de Documentos (`/corpus`)

Al no haberse adjuntado el paquete externo de manuales en el enunciado de la prueba, se diseñó y redactó un **corpus técnico propio en [`/corpus`](corpus)** compuesto por **6 manuales operativos en Markdown** (superando el mínimo de 5 documentos solicitado), alineados con módulos y normatividad real de **Ofima ERP**:

1. `01-facturacion-electronica-y-anulaciones.md` — Reglas DIAN, rechazo `FAC-402` y emisión de Nota Crédito `NC-01` por CUFE.
2. `02-cierre-contable-y-conciliacion.md` — Bloqueo de cierre `CON-301`, comprobantes en borrador y conciliación de cuenta puente `999905`.
3. `03-inventarios-kardex-y-bodegas.md` — Valorización de inventarios, traslados entre bodegas y error `INV-108`.
4. `04-nomina-electronica-y-novedades.md` — Nómina electrónica DIAN, novedades laborales e incapacidades.
5. `05-cuentas-por-pagar-y-tesoreria.md` — Causación de facturas de proveedores, retenciones y egresos de tesorería.
6. `06-seguridad-roles-y-auditoria-erp.md` — Políticas de acceso, bloqueo `SEC-423` con 2FA y trazabilidad de auditoría.

Adicionalmente, el motor RAG soporta ingesta dinámica de archivos **`.md`, `.txt` y `.pdf`** mediante `POST /ingesta`, `POST /ingesta/archivo` y el modal **📚 Base Conocimiento RAG** en el portal web.

---

## 4. Alcance Implementado (MVP + Milla Extra) y Recorte Consciente

- **Funcionalidades Base (MVP):** autenticación JWT por roles (`Cliente`/`Analista`), CRUD paginado de casos con control de propiedad, máquina de estados con invariantes (`Abierto -> EnProceso -> Resuelto -> Cerrado` impidiendo cerrar sin respuesta publicada), consulta RAG con citas verificables y flujo obligatorio de edición/aprobación del Analista antes de publicar al cliente.
- **Capacidades de Milla Extra Implementadas:**
  - **Streaming SSE en Tiempo Real (`POST /api/casos/{id}/sugerencia/stream`):** emisión progresiva de fuentes y tokens hacia Angular sin buffering en Nginx.
  - **Dashboard Ejecutivo Operativo y FinOps (`GET /api/casos/metricas`):** 6 tarjetas KPI con Signals (*Casos Activos*, *Severidad Crítica*, *Adopción IA*, *Consumo FinOps RAG* en tokens, *Satisfacción CSAT* y *Deflexión RAG* persistente con `%` de ahorro operativo).
  - **Motor de SLAs Visuales:** cálculo automático por severidad (`Critica` 2h, `Alta` 8h, `Media` 24h, `Baja` 72h) con indicadores `EnTiempo`, `PorVencer`, `Vencido` y `Cumplido`.
  - **Deflexión Preventiva Persistente (`POST /api/casos/deflexion` y `POST /api/casos/deflexion/registrar`):** consulta RAG con debounce (`700 ms`) en `+ Nuevo Caso` antes de registrar el ticket y persistencia en PostgreSQL del ahorro real cuando el usuario evita abrir el caso.
  - **Reapertura Controlada Auditada (`POST /api/casos/{id}/reabrir`):** permite al cliente propietario o al analista reabrir casos `Resuelto` o `Cerrado` hacia `EnProceso` con motivo obligatorio (>= 10 caracteres), recálculo SLA y trazabilidad en el historial.
  - **Evidencia Visual y Experiencia UI:** adjuntos de capturas PNG/JPG/WebP (máx. 2 MB) en creación y respuestas con vista previa y modal *lightbox*, renderizado seguro de Markdown (`SafeMarkdownPipe`) y micro-animaciones GPU con soporte `prefers-reduced-motion`.
  - **Encuesta CSAT (`POST /api/casos/{id}/csat`):** calificación de 1 a 5 estrellas y comentario opcional del cliente dueño al resolver/cerrar.
  - **Escalamiento de Prioridad Auditado (`PATCH /api/casos/{id}/prioridad`):** justificación obligatoria (>= 10 caracteres) registrada en historial.
  - **Protección contra Borrado y Papelera de Reciclaje:** soft delete (`Eliminado`/`FechaEliminacion`), confirmación con cuenta regresiva de 3s, restauración individual y restauración de casos demo.
  - **Seguridad, Observabilidad y Productividad UI:** correlación distribuida `X-Correlation-ID` de punta a punta (Angular ➔ .NET 8 `CorrelationIdMiddleware` + `CorrelationIdDelegatingHandler` ➔ FastAPI), **Rate Limiting FinOps** nativo en .NET 8 (`"AiEndpointsPolicy"`, `20 req/min` con `429 ProblemDetails` en endpoints IA), cierre por inactividad (10m), búsqueda instantánea (`300 ms`) y gestor en vivo de manuales RAG.
- **Arquitectura Target en Azure y Recorte Consciente:** se excluyeron omnicanalidad IMAP/WhatsApp y SSO Entra ID del runtime local para garantizar portabilidad autocontenida con `docker compose up --build`; el plano objetivo Azure contempla **Blob Storage + Service Bus** (ingesta asíncrona), **SAS Tokens** (adjuntos), **PostgreSQL RLS + Azure AI Search** (multi-tenant), **Entra ID + Key Vault + Azure AI Content Safety (DLP)** y **Agente ERP con Function Calling / MCP** (ver [01-arquitectura-y-adrs.md](docs/01-arquitectura-y-adrs.md) y [02-plan-estandares-riesgos-bitacora.md](docs/02-plan-estandares-riesgos-bitacora.md)).

---

## 5. Evaluación Empírica RAG, Pruebas y CI/CD

La evaluación automatizada sobre el corpus de 6 manuales (`86` fragmentos) y 10 casos (`8` en dominio + `2` fuera de dominio) alcanzó **100.00%** en las tres métricas (reporte completo en [evaluacion-rag.md](docs/evaluacion-rag.md)):

| Suite / Pipeline | Comando | Resultado Verificado | Cobertura Principal |
| :--- | :--- | :---: | :--- |
| **Python RAG** | `pytest -v` | **8 passed** | Chunking + metadatos, citación `FAC-402`, anclaje negativo, *prompt injection* HTTP `400`, LRU cache, `/documentos`, ingesta `.txt` y `X-Correlation-ID` |
| **.NET 8 Backend** | `dotnet test backend/MesaAyuda.sln` | **27 passed** | **15** unitarias (dominio, SLA, prioridad, CSAT, reapertura, deflexión, imágenes, soft-delete) + **12** integración (`X-Correlation-ID`, Rate Limiting `429` y flujos E2E) |
| **Angular 18 Frontend** | `npm test` (Karma/ChromeHeadless) | **22 passed** | `AuthService`, `IdleTimeoutService`, `SafeMarkdownPipe`, `CasosListComponent`, `CasoCreateComponent`, `CasoDetailComponent` y `KnowledgeBaseModalComponent` |
| **Evaluación RAG** | `python -m evaluation.evaluate` | **100% (10/10)** | Hit Rate @ 4 (`100%`), Faithfulness (`100%`), Out-of-Domain Rejection (`100%`), latencia media `13.75 ms` |

El workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) ejecuta los 3 pipelines en paralelo ante cada `push` y `pull_request` sobre `main`.

---

## 6. Documentación de Liderazgo Técnico (`/docs`)

- [Arquitectura C4 (Docker Compose y Microsoft Azure) y ADRs](docs/01-arquitectura-y-adrs.md)
- [Plan de Evolución a 3 Meses, Estándares, Riesgos de IA y Bitácora](docs/02-plan-estandares-riesgos-bitacora.md)
- [Evaluación Empírica del Motor RAG (Dataset de 10 Casos)](docs/evaluacion-rag.md)

---

## 7. Declaración Transparente del Uso de Asistentes de IA

En el desarrollo de **MesaAyuda IA** se utilizaron **Google Gemini** y **Antigravity** (IDE agéntico) como *pair programmer* bajo supervisión estricta *Human-in-the-Loop*:

- **Uso asistido por IA:** apoyo en scaffolding incremental (.NET 8 Clean Architecture, FastAPI, componentes Standalone Angular 18), generación inicial de borradores del corpus sintético ERP, estructuración de pruebas automatizadas (`pytest`, `xUnit`, `Jasmine/Karma`), diagramación Mermaid C4 y revisión de consistencia documental.
- **Criterio, diseño y validación humana del candidato:** definición de la arquitectura y ADRs, modelado de datos e invariantes de dominio (`Caso`/`Respuesta`, SLA, CSAT, máquina de estados), calibración del buscador híbrido (ajuste de pesos `0.70/0.30`, stopwords en español preservando códigos ERP y umbral `0.35`), endurecimiento de seguridad (JWT/RBAC, sanitización y bloqueo de *prompt injection*), resolución de incidencias de integración y ejecución/validación real de todas las suites y contenedores Docker.
- **Privacidad:** todo el código, ejemplos y manuales son 100 % sintéticos; no se utilizaron datos reales ni información PII de clientes.
