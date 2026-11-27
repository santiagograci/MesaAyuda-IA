from __future__ import annotations

from pydantic import BaseModel, Field


class ChunkMetadata(BaseModel):
    """Metadatos asociados a un fragmento indexado del corpus."""

    documento: str
    seccion: str
    chunk_index: int = Field(..., ge=0)


class TextChunk(BaseModel):
    """Unidad textual fragmentada lista para indexación vectorial y léxica."""

    id: str
    texto: str
    metadata: ChunkMetadata


class IngestRequest(BaseModel):
    """Solicitud opcional para reindexar documentos desde una ruta específica."""

    corpus_path: str | None = None


class IngestResponse(BaseModel):
    """Resultado de una operación de ingesta e indexación documental."""

    documentos_procesados: int = Field(..., ge=0)
    fragmentos_indexados: int = Field(..., ge=0)
    estado: str


class PreguntaRequest(BaseModel):
    """Consulta en lenguaje natural dirigida al motor RAG."""

    pregunta: str = Field(..., min_length=3, max_length=1000)
    top_k: int = Field(default=4, ge=1, le=10)


class FuenteCitada(BaseModel):
    """Fragmento recuperado y citado como soporte de la respuesta generada."""

    documento: str
    seccion: str
    fragmento: str
    puntaje: float = Field(..., ge=0.0, le=1.0)


class PreguntaResponse(BaseModel):
    """Respuesta final del motor RAG con trazabilidad de fuentes y consumo."""

    respuesta: str
    fuentes: list[FuenteCitada]
    tokens_usados: int = Field(..., ge=0)
    anclado: bool
    proveedor_llm: str
