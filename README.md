# MesaAyuda IA — Plataforma de Soporte Empresarial con RAG (Ofima S.A.S.)

Plataforma integral de mesa de ayuda empresarial potenciada por Generación Aumentada por Recuperación (RAG) sobre la base documental corporativa de **Ofima S.A.S.**

## Arquitectura

El sistema sigue una arquitectura desacoplada de **3 piezas** respaldada por PostgreSQL y orquestada en contenedores Docker sobre la red `mesaayuda-net`:

1. **Backend Core (`.NET 8 Clean Architecture` — `backend/`)**: API REST encargada de la gestión de tickets, usuarios, autenticación JWT, reglas de negocio, persistencia en PostgreSQL y comunicación con el servicio RAG.
2. **Frontend Web (`Angular 17+` — `frontend/`)**: Interfaz de usuario para solicitantes y agentes de soporte, servida mediante Nginx en contenedor.
3. **Servicio de Inteligencia y Recuperación (`Python FastAPI + ChromaDB` — `rag-service/`)**: Microservicio encargado de la ingesta del corpus documental (`corpus/`), generación de embeddings, búsqueda vectorial y síntesis de respuestas con soporte para modo `mock` o proveedores LLM externos.

### Servicios y Puertos

| Servicio | Tecnología | Puerto Host -> Contenedor | Rol |
| :--- | :--- | :--- | :--- |
| `postgres` | `postgres:16-alpine` | `5432:5432` | Base de datos relacional (`pgdata`) |
| `rag-service` | Python FastAPI + ChromaDB | `8000:8000` | Indexación, búsqueda semántica y endpoint `/health` |
| `backend` | .NET 8 Clean Architecture | `5080:8080` | API principal de MesaAyuda |
| `frontend` | Angular 17+ (Nginx) | `4200:80` | Aplicación web cliente |

## Inicio Rápido

Asegúrate de contar con Docker y Docker Compose instalados y ejecuta desde la raíz del proyecto:

```bash
cp .env.example .env && docker compose up --build
```

Una vez levantados los servicios, estarán disponibles en:

- **Frontend Web:** `http://localhost:4200`
- **Backend API (.NET):** `http://localhost:5080`
- **Servicio RAG (FastAPI):** `http://localhost:8000/health`
- **PostgreSQL:** `localhost:5432`

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
| `GEMINI_API_KEY` | *(vacío)* | Clave de API de Google Gemini (requerida si `LLM_PROVIDER=gemini`). |
| `OPENAI_API_KEY` | *(vacío)* | Clave de API de OpenAI (requerida si `LLM_PROVIDER=openai`). |
| `EMBEDDING_MODEL` | `all-MiniLM-L6-v2` | Modelo de embeddings utilizado para vectorizar el corpus. |
| `CHUNK_SIZE` | `650` | Tamaño máximo de fragmento de texto durante la indexación. |
| `CHUNK_OVERLAP` | `120` | Solapamiento entre fragmentos consecutivos para preservar contexto. |
| `SIMILARITY_THRESHOLD` | `0.35` | Umbral mínimo de similitud para considerar relevante un fragmento recuperado. |

## Resultados de Evaluación RAG

> *Sección reservada. Aquí se documentarán las métricas de recuperación y generación (Precision@K, Recall@K, Faithfulness, latencia y casos de prueba) una vez ejecutada la batería de evaluación sobre el corpus.*

## Declaración de Uso de Asistentes de IA

> *Sección reservada. Aquí se detallarán las herramientas de IA empleadas durante el desarrollo, su alcance en la arquitectura, generación de código y validaciones realizadas.*
