# Plan de Evolución, Estándares, Riesgos de IA y Bitácora — MesaAyuda IA

## 1. Plan de Evolución a 3 Meses (Equipo de 4 Desarrolladores)

Equipo base: **2 Backend .NET**, **1 Frontend Angular**, **1 Python / IA & Datos**.

| Horizonte | Entregables Clave | Reparto por Rol |
| :--- | :--- | :--- |
| **Mes 1: Hardening, SSO y Observabilidad** | Integración SSO con **Microsoft Entra ID** (OIDC), rotación de secretos en Azure Key Vault, trazabilidad distribuida **OpenTelemetry** (correlación API .NET -> FastAPI -> Postgres), rate limiting por usuario y pruebas de carga k6. | **2 .NET:** Entra ID + OpenTelemetry + Rate Limit.<br>**1 Angular:** Flujo PKCE SSO + manejo de expiración.<br>**1 Python/Datos:** Structured logging JSON + métricas de latencia RAG. |
| **Mes 2: Streaming, Memoria y Feedback Loop** | Respuestas en **streaming SSE** para sugerencias largas, memoria conversacional por hilo de caso (últimos N turnos), captura de feedback del analista (*útil / editada / rechazada*) y pipeline automático de curación del corpus. | **1 .NET + 1 Angular:** Proxy/consumo SSE y UI de feedback.<br>**1 .NET:** Modelo de métricas de calidad por analista.<br>**1 Python/Datos:** Endpoint `/preguntar/stream` y dataset continuo de evaluación. |
| **Mes 3: Multi-tenancy, Re-ranking y Azure Prod** | Aislamiento **multi-tenant** por empresa cliente Ofima (filtros en PostgreSQL y particionamiento/metadato `tenant_id` en índice vectorial), **re-ranking Cross-Encoder** sobre top-12 candidatos y despliegue productivo en Azure Container Apps con Blue/Green. | **2 .NET:** Tenant resolver + Row-Level/Query Filters + CI/CD Azure.<br>**1 Angular:** Selector corporativo de tenant y dashboard SLA.<br>**1 Python/Datos:** Cross-Encoder + migración a Azure AI Search / Qdrant. |

**Riesgos operativos del roadmap:** latencia adicional del Cross-Encoder (mitigar ejecutando sobre top-10 con límite `< 400 ms` y fallback al ranking híbrido) y deriva de permisos multi-tenant (mitigar con pruebas de integración automáticas por tenant en CI).

---

## 2. Estándares de Equipo y Gobernanza de IA

- **Estrategia de ramas:** **GitHub Flow / Trunk-Based** con `main` protegida y ramas cortas (`feat/<ticket>-descripcion`, `fix/<ticket>-descripcion`) integradas vía Pull Request con CI obligatorio en verde (`rag-service-test`, `backend-build-and-test`, `frontend-build`).
- **Definition of Done (DoD):** código compilando sin warnings en modo Release/producción, pruebas unitarias y de integración nuevas cubriendo el cambio, documentación OpenAPI/Swagger o JSDoc actualizada, cero secretos en repositorio y aprobación de al menos 1 par en Code Review.
- **Criterios de Code Review:** invariantes de negocio encapsuladas en Dominio (nunca en controladores), consultas paginadas con `AsNoTracking()`, validación explícita de inputs (FluentValidation / Pydantic) y manejo uniforme de errores (`ProblemDetails`).
- **Política de Gobernanza de Asistentes de IA:**
  1. **Prohibición estricta de PII/datos reales:** nunca copiar NITs reales de clientes, nóminas, llaves privadas ni volcados de producción en prompts de asistentes externos.
  2. **Revisión humana obligatoria:** todo código, migración EF Core, consulta SQL o regla tributaria generada con asistencia de IA debe ser revisada, ejecutada y probada localmente por el desarrollador responsable.
  3. **Trazabilidad en PRs:** los Pull Requests declaran qué módulos fueron acelerados con IA y qué pruebas garantizan su comportamiento determinista.

---

## 3. Matriz de Riesgos de IA en Producción

| Riesgo | Impacto | Probabilidad | Mitigación Técnica Concreta |
| :--- | :---: | :---: | :--- |
| **1. Alucinaciones** (inventar rutas ERP, códigos o porcentajes tributarios) | Alto | Media | Umbral estricto `similarity_threshold=0.35` (`anclado=false` si no supera), prompt restrictivo solo con contexto, citas obligatorias `[Fuente: doc - sección]` y **aprobación humana obligatoria del Analista** antes de publicar al cliente. |
| **2. Filtración de datos sensibles / PII** hacia LLMs externos | Crítico | Baja | Redacción/enmascaramiento previo de patrones (cédulas, correos, tokens) antes de salir de la VPC, uso de proveedor local/privado para datos sensibles y separación de contexto por `ClienteId` en .NET. |
| **3. Prompt Injection** desde tickets maliciosos | Alto | Media | Capa `security.py` con sanitización de caracteres de control y bloqueo HTTP `400` por regex multi-idioma (`PromptInjectionError`), más delimitación estricta entre instrucciones del sistema y texto del usuario. |
| **4. Costos descontrolados por token** (Gemini/OpenAI) | Medio | Media | Caché LRU en memoria para embeddings de consulta, `top_k` acotado (`<= 4` en producción), `chunk_size=650`, registro de `tokens_usados` por consulta, cuotas por usuario y fallback automático a `grounded-mock`. |
| **5. Deriva o degradación del RAG** al crecer el corpus | Medio | Media | Suite automatizada `evaluate.py` ejecutada ante cada cambio del corpus midiendo **Hit Rate @ 4**, **Faithfulness** y **Out-of-Domain Rejection**, bloqueando despliegues si cae por debajo del 90%. |

---

## 4. Bitácora de Trabajo y Recorte Consciente de Alcance

| Bloque | Tiempo | Actividad y Decisiones de Priorización |
| :--- | :---: | :--- |
| **1. Base Repo & Corpus ERP** | 0.8 h | Compose de 4 servicios, `.env` unificado y 6 manuales Ofima ERP con códigos (`FAC-402`, `CON-301`, `INV-108`, `SEC-423`) y reglas DIAN/laborales reales. |
| **2. Motor RAG Python + Eval** | 1.7 h | Ingesta con chunking por secciones, búsqueda híbrida ChromaDB + BM25, mitigación de prompt injection, Strategy LLM, gestión en vivo (`/documentos`, `/ingesta/archivo`) y **100% en evaluación empírica** (`docs/evaluacion-rag.md`). |
| **3. Backend .NET 8 Clean Arch** | 1.8 h | Dominio DDD (máquina de estados, SLA por prioridad, escalamiento auditado, CSAT, papelera soft-delete), JWT por roles, EF Core + Postgres, cliente RAG con Polly, streaming SSE, deflexión preventiva y KPIs FinOps. |
| **4. Frontend Angular 18 Signals** | 1.2 h | Split-Screen Claro/Oscuro + máscaras, SLA visual, deflexión antes de crear caso, encuesta CSAT 1–5 estrellas, cierre por inactividad (10m), búsqueda 300ms, gestor RAG modal y panel KPI ejecutivo. |
| **5. CI, Docs y Validación E2E** | 0.5 h | Pipeline GitHub Actions (3 jobs), arquitectura C4/ADRs, matriz de riesgos y validación completa con `docker compose up --build`. |

**Priorización de milla extra vs. recorte deliberado de alcance:** se priorizó implementar capacidades de alto valor operativo directamente probables de extremo a extremo en local —**motor SLA por severidad**, **deflexión RAG preventiva**, **encuesta CSAT con KPI**, **streaming SSE**, **papelera 30d con soft-delete** y **escalamiento auditado**— porque fortalecen el flujo central de soporte sin introducir dependencias externas. En contraste, se recortaron deliberadamente la **omnicanalidad IMAP/WhatsApp** y el **SSO corporativo con Microsoft Entra ID** (dejando los puertos e interfaces desacoplados para Fase 2) con el fin de preservar la portabilidad autocontenida del sistema mediante un único `docker compose up --build` sin credenciales de terceros.
