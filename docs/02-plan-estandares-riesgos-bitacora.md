# Plan de Evolución, Estándares, Riesgos de IA y Bitácora — MesaAyuda IA

## 1. Plan de Evolución a 3 Meses (Equipo: 2 .NET, 1 Angular, 1 Python/Datos)

| Horizonte | Épicas Prioritarias | Reparto por Rol |
| :--- | :--- | :--- |
| **Mes 1: Hardening, SSO y Observabilidad** | SSO **Microsoft Entra ID** (OIDC/PKCE), Azure Key Vault, trazabilidad distribuida **OpenTelemetry** (.NET -> FastAPI -> Postgres), rate limiting y pruebas k6. | **2 .NET:** Entra ID, OpenTelemetry, Rate Limit.<br>**1 Angular:** Flujo PKCE y sesiones.<br>**1 Python:** Logging JSON y métricas RAG. |
| **Mes 2: Memoria, Feedback Loop y Omnicanalidad** | Memoria conversacional multi-turno por ticket, feedback por fragmento (*útil / rechazado*), curación automática del corpus y conectores **IMAP / WhatsApp**. | **1 .NET + 1 Angular:** UI de feedback y conectores.<br>**1 .NET:** Ingesta omnicanal desacoplada.<br>**1 Python:** Reranking/curación y dataset continuo. |
| **Mes 3: Multi-tenancy, Agente ERP y Azure** | Aislamiento **multi-tenant** por empresa (`tenant_id` SQL + vectorial), evolución de RAG consultivo a **Agente ERP con Function Calling** (ejecución asistida de flujos como `CON-301` bajo confirmación), Cross-Encoder top-12 y despliegue Azure Container Apps. | **2 .NET:** Tenant resolver y CI/CD Azure.<br>**1 Angular:** Selector de tenant y confirmaciones agente.<br>**1 Python:** Function Calling y Azure AI Search. |

**Riesgos técnicos clave del roadmap:** latencia adicional del Cross-Encoder (acotar a top-10 con timeout `< 400 ms` y fallback al híbrido) y fugas entre tenants (mitigar con pruebas de integración por `tenant_id` en CI).

---

## 2. Estándares de Equipo y Gobernanza de IA

- **Estrategia de ramas:** **Trunk-Based Development** con `main` protegida y ramas cortas (`feat/*`, `fix/*`) integradas vía PR tras pasar los 3 jobs de CI (`rag-service-test`, `backend-build-and-test`, `frontend-build`).
- **Definition of Done (DoD):** compilación limpia en `Release`/producción, pruebas unitarias e integración cubriendo el cambio, comentarios XML/JSDoc actualizados, cero secretos en Git y aprobación de al menos 1 par en Code Review.
- **Criterios de Code Review:** invariantes encapsuladas en Dominio, consultas paginadas con `AsNoTracking()`, validación explícita (FluentValidation/DataAnnotations/Pydantic) y errores RFC 7807 (`ProblemDetails`).
- **Política de Gobernanza de IA:** (1) prohibido pegar PII, NITs reales o secretos en prompts; (2) revisión y ejecución humana obligatoria de todo código o SQL generado con IA; (3) cobertura de pruebas obligatoria y declaración de uso en PRs.

---

## 3. Matriz de Riesgos de IA en Producción y Mitigación

| Riesgo | Impacto | Mitigación Técnica Concreta |
| :--- | :---: | :--- |
| **Alucinaciones** | Alto | Umbral estricto `similarity_threshold=0.35`, prompt cerrado al contexto, citas `[Fuente: doc - seccion]` y **aprobación humana obligatoria del Analista**. |
| **Datos sensibles / PII** | Crítico | Enmascaramiento previo a LLMs externos, aislamiento por `ClienteId` en .NET y soporte de proveedor local (`mock` / LLM self-hosted) sin salida a internet. |
| **Prompt Injection** | Alto | Sanitización y bloqueo HTTP `400` en `security.py` (`PromptInjectionError`) con regex multi-idioma y separación estricta sistema/consulta. |
| **Costos por token** | Medio | Caché LRU de embeddings, `top_k <= 4`, `chunk_size=650`, persistencia de `TokensUsados` con dashboard FinOps y fallback automático a `grounded-mock`. |
| **Deriva de calidad RAG** | Medio | Benchmark automatizado `evaluate.py` sobre dataset dorado midiendo Hit Rate @ 4, Faithfulness y Out-of-Domain Rejection en cada actualización del corpus. |

---

## 4. Bitácora de Trabajo y Recorte Consciente de Alcance

| Bloque | Tiempo | Enfoque Ejecutado |
| :--- | :---: | :--- |
| **1. Base & Corpus** | 0.8 h | Compose de 4 servicios, `.env.example` y 6 manuales Ofima ERP (`FAC-402`, `CON-301`, `INV-108`, `SEC-423`, nómina, tesorería). |
| **2. Motor RAG + Eval** | 1.7 h | Chunking jerárquico, búsqueda híbrida ChromaDB + BM25, `security.py`, Strategy LLM, API de documentos (`/documentos`, `/ingesta/archivo`) y `evaluate.py` al **100%**. |
| **3. Backend .NET 8** | 1.8 h | Clean Architecture, JWT/RBAC, EF Core + Postgres, Polly, streaming SSE, deflexión RAG persistente, reapertura auditada, adjuntos PNG/JPG, SLA, CSAT, papelera y FinOps. |
| **4. Frontend Angular 18** | 1.2 h | Split-Screen Claro/Oscuro + máscaras, SLA visual, deflexión en vivo, CSAT, cierre por inactividad, búsqueda 300ms, gestor RAG, Markdown seguro, lightbox y micro-animaciones GPU. |
| **5. CI, Docs & QA** | 0.5 h | Pipeline GitHub Actions, documentación C4/ADRs/riesgos y validación integral local + contenedores Docker Compose. |

**Priorización de milla extra vs. recorte consciente de alcance:** tras asegurar el MVP en las primeras 6 horas, se priorizaron funcionalidades operativas probables de punta a punta en local —**streaming SSE**, **dashboard FinOps/CSAT/Deflexión RAG**, **SLAs visuales**, **deflexión preventiva persistente**, **reapertura controlada**, **evidencia visual con lightbox**, **renderizado Markdown seguro** y **gestor RAG**— sin dependencias externas. Se recortaron la **omnicanalidad IMAP/WhatsApp** y el **SSO Entra ID** (interfaces listas para Fase 2) para garantizar portabilidad 100% autocontenida con `docker compose up --build`.
