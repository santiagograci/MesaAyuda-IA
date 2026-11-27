from __future__ import annotations

import logging
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import Body, FastAPI, HTTPException

from app.config import settings
from app.llm_provider import get_llm_provider
from app.models import (
    IngestRequest,
    IngestResponse,
    PreguntaRequest,
    PreguntaResponse,
)
from app.rag_engine import RagEngine
from app.security import PromptInjectionError
from app.vector_store import HybridVectorStore

logger = logging.getLogger(__name__)

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
        return engine.ingest(corpus_path=target_path)
    except (FileNotFoundError, NotADirectoryError, ValueError) as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@app.post("/preguntar", response_model=PreguntaResponse)
def ask(request: PreguntaRequest) -> PreguntaResponse:
    """Responde preguntas técnicas sobre Ofima ERP con validación de seguridad y citas."""
    engine = get_rag_engine()
    try:
        return engine.ask(request)
    except PromptInjectionError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
