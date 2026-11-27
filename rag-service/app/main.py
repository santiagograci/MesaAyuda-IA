from __future__ import annotations

import logging
import re
import time
import uuid
from collections.abc import AsyncIterator, Awaitable, Callable
from contextlib import asynccontextmanager
from contextvars import ContextVar
from pathlib import Path

from fastapi import Body, FastAPI, File, HTTPException, Request, Response, UploadFile

from app.config import settings
from app.llm_provider import get_llm_provider
from app.models import (
    DocumentosResponse,
    IngestaArchivoResponse,
    IngestRequest,
    IngestResponse,
    PreguntaRequest,
    PreguntaResponse,
)
from app.rag_engine import RagEngine
from app.security import PromptInjectionError
from app.vector_store import HybridVectorStore

logger = logging.getLogger(__name__)

CORRELATION_HEADER = "X-Correlation-ID"
_CORRELATION_ID_RE = re.compile(r"^[a-zA-Z0-9._-]{1,64}$")
correlation_id_var: ContextVar[str] = ContextVar("correlation_id", default="-")


def resolve_correlation_id(incoming: str | None) -> str:
    """Valida el identificador de correlación entrante o genera un UUID v4 nuevo."""
    if incoming:
        candidate = incoming.strip()
        if _CORRELATION_ID_RE.match(candidate):
            return candidate
    return str(uuid.uuid4())

_vector_store: HybridVectorStore | None = None
_rag_engine: RagEngine | None = None


def get_vector_store() -> HybridVectorStore:
    """Retorna la instancia compartida del almacén híbrido."""
    global _vector_store
    if _vector_store is None:
        _vector_store = HybridVectorStore()
    return _vector_store


def get_rag_engine() -> RagEngine:
    """Retorna la instancia compartida del motor RAG."""
    global _rag_engine
    if _rag_engine is None:
        _rag_engine = RagEngine(
            vector_store=get_vector_store(),
            llm_provider=get_llm_provider(),
        )
    return _rag_engine


@asynccontextmanager
async def lifespan(_app: FastAPI) -> AsyncIterator[None]:
    """Inicializa el almacén vectorial e ingesta el corpus automáticamente si está vacío."""
    store = get_vector_store()
    engine = get_rag_engine()
    corpus_dir = Path(settings.corpus_path)

    if store.count() == 0 and corpus_dir.is_dir():
        try:
            result = engine.ingest(str(corpus_dir))
            logger.info(
                "Ingesta inicial completada: %d documentos, %d fragmentos.",
                result.documentos_procesados,
                result.fragmentos_indexados,
            )
        except Exception as exc:
            logger.warning("No se pudo ejecutar la ingesta automática inicial: %s", exc)

    yield


app = FastAPI(
    title="MesaAyuda IA - RAG Service",
    version="1.0.0",
    lifespan=lifespan,
)


@app.middleware("http")
async def correlation_id_middleware(
    request: Request,
    call_next: Callable[[Request], Awaitable[Response]],
) -> Response:
    """Captura o genera X-Correlation-ID, registra log estructurado y lo devuelve en la respuesta."""
    cid = resolve_correlation_id(request.headers.get(CORRELATION_HEADER))
    token = correlation_id_var.set(cid)
    request.state.correlation_id = cid
    start = time.perf_counter()

    try:
        response = await call_next(request)
    except Exception:
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        logger.exception(
            "http_request_error correlation_id=%s method=%s path=%s duration_ms=%.2f",
            cid,
            request.method,
            request.url.path,
            elapsed_ms,
        )
        correlation_id_var.reset(token)
        raise

    elapsed_ms = (time.perf_counter() - start) * 1000.0
    response.headers[CORRELATION_HEADER] = cid
    logger.info(
        "http_request correlation_id=%s method=%s path=%s status=%d duration_ms=%.2f",
        cid,
        request.method,
        request.url.path,
        response.status_code,
        elapsed_ms,
    )
    correlation_id_var.reset(token)
    return response


@app.get("/health")
def health() -> dict[str, str | int]:
    """Endpoint de monitoreo de salud y estado de indexación del servicio RAG."""
    store = get_vector_store()
    return {
        "status": "healthy",
        "embedding_model": settings.embedding_model,
        "indexed_chunks": store.count(),
        "llm_provider": settings.llm_provider,
    }


@app.post("/ingesta", response_model=IngestResponse)
def ingest(request: IngestRequest | None = Body(default=None)) -> IngestResponse:
    """Indexa o reindexa los manuales del ERP en ChromaDB y BM25."""
    engine = get_rag_engine()
    target_path = request.corpus_path if request is not None else None
    try:
        result = engine.ingest(corpus_path=target_path)
        logger.info(
            "rag_ingest correlation_id=%s documentos=%d fragmentos=%d",
            correlation_id_var.get(),
            result.documentos_procesados,
            result.fragmentos_indexados,
        )
        return result
    except (FileNotFoundError, NotADirectoryError, ValueError) as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.get("/documentos", response_model=DocumentosResponse)
def list_documents() -> DocumentosResponse:
    """Lista los manuales únicos indexados en ChromaDB con su conteo de fragmentos."""
    engine = get_rag_engine()
    result = engine.list_documents()
    logger.info(
        "rag_documents correlation_id=%s total_documentos=%d total_fragmentos=%d",
        correlation_id_var.get(),
        result.total_documentos,
        result.total_fragmentos,
    )
    return result


@app.post("/ingesta/archivo", response_model=IngestaArchivoResponse)
async def ingest_uploaded_file(file: UploadFile = File(...)) -> IngestaArchivoResponse:
    """Guarda un manual `.md`, `.txt` o `.pdf` en `/corpus` y lo indexa inmediatamente."""
    engine = get_rag_engine()
    content = await file.read()
    try:
        result = engine.ingest_uploaded_file(
            filename=file.filename or "",
            content=content,
        )
        logger.info(
            "rag_ingest_file correlation_id=%s archivo=%s fragmentos=%d",
            correlation_id_var.get(),
            result.archivo,
            result.fragmentos_archivo,
        )
        return result
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/preguntar", response_model=PreguntaResponse)
def ask(request: PreguntaRequest) -> PreguntaResponse:
    """Responde preguntas técnicas sobre Ofima ERP con validación de seguridad y citas."""
    engine = get_rag_engine()
    try:
        result = engine.ask(request)
        logger.info(
            "rag_ask correlation_id=%s anclado=%s fuentes=%d tokens=%d provider=%s",
            correlation_id_var.get(),
            result.anclado,
            len(result.fuentes),
            result.tokens_usados,
            result.proveedor_llm,
        )
        return result
    except PromptInjectionError as exc:
        logger.warning(
            "rag_prompt_injection correlation_id=%s detail=%s",
            correlation_id_var.get(),
            str(exc),
        )
        raise HTTPException(status_code=400, detail=str(exc)) from exc
