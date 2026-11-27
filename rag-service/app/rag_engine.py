from __future__ import annotations

import re

from app.config import settings
from app.ingestion import load_and_chunk_corpus
from app.llm_provider import LLMProvider
from app.models import (
    FuenteCitada,
    IngestResponse,
    PreguntaRequest,
    PreguntaResponse,
    TextChunk,
)
from app.security import validate_and_sanitize_query
from app.vector_store import HybridVectorStore

NO_INFO_MESSAGE = (
    "No dispongo de información suficiente en los manuales oficiales del ERP "
    "para responder a esta consulta."
)

_SYSTEM_PROMPT = (
    "Eres el asistente técnico oficial de soporte para Ofima ERP (Ofima S.A.S.). "
    "Responde EXCLUSIVAMENTE utilizando la información presente en los fragmentos "
    "del contexto proporcionado. Está estrictamente prohibido especular, inventar "
    "rutas de menú, códigos de error, cuentas contables o porcentajes que no "
    "aparezcan en el contexto. Si la información no es suficiente, indícalo de forma "
    "clara y cita siempre el documento y la sección de origen."
)


def _truncate_fragment(text: str, max_length: int = 320) -> str:
    cleaned = re.sub(r"\s+", " ", text).strip()
    if len(cleaned) <= max_length:
        return cleaned
    return cleaned[: max_length - 3].rstrip() + "..."


def _build_user_prompt(
    query: str, relevant_chunks: list[tuple[TextChunk, float]]
) -> str:
    context_blocks: list[str] = []
    for idx, (chunk, score) in enumerate(relevant_chunks, start=1):
        context_blocks.append(
            f"[{idx}] Documento: {chunk.metadata.documento} | "
            f"Sección: {chunk.metadata.seccion} | Puntaje: {score:.4f}\n"
            f"{chunk.texto}"
        )
    joined_context = "\n\n---\n\n".join(context_blocks)
    return (
        f"Contexto oficial del ERP:\n{joined_context}\n\n"
        f"Pregunta del usuario:\n{query}\n\n"
        "Instrucción: Redacta una respuesta técnica clara en español basada "
        "únicamente en el contexto anterior y cita las fuentes."
    )


class RagEngine:
    """Motor principal de ingesta y consulta RAG con anclaje estricto."""

    NO_INFO_MESSAGE = NO_INFO_MESSAGE

    def __init__(
        self,
        vector_store: HybridVectorStore,
        llm_provider: LLMProvider,
        similarity_threshold: float | None = None,
    ) -> None:
        self.vector_store = vector_store
        self.llm_provider = llm_provider
        self._similarity_threshold = similarity_threshold

    def ingest(self, corpus_path: str | None = None) -> IngestResponse:
        """Procesa los manuales desde `corpus_path` y los indexa en el almacén híbrido."""
        target_path = corpus_path or settings.corpus_path
        docs_count, chunks = load_and_chunk_corpus(
            corpus_path=target_path,
            chunk_size=settings.chunk_size,
            chunk_overlap=settings.chunk_overlap,
        )
        indexed_count = self.vector_store.index_chunks(chunks)
        return IngestResponse(
            documentos_procesados=docs_count,
            fragmentos_indexados=indexed_count,
            estado="ok",
        )

    def ask(self, request: PreguntaRequest) -> PreguntaResponse:
        """Valida seguridad, recupera fragmentos relevantes y genera respuesta anclada."""
        sanitized_query = validate_and_sanitize_query(request.pregunta)
        results = self.vector_store.search(sanitized_query, top_k=request.top_k)

        threshold = (
            self._similarity_threshold
            if self._similarity_threshold is not None
            else settings.similarity_threshold
        )

        if not results or results[0][1] < threshold:
            return PreguntaResponse(
                respuesta=self.NO_INFO_MESSAGE,
                fuentes=[],
                tokens_usados=0,
                anclado=False,
                proveedor_llm="none",
            )

        min_chunk_score = threshold * 0.75
        relevant_chunks = [
            (chunk, score) for chunk, score in results if score >= min_chunk_score
        ]
        if not relevant_chunks:
            relevant_chunks = [results[0]]

        user_prompt = _build_user_prompt(sanitized_query, relevant_chunks)
        respuesta, tokens_usados, proveedor_llm = self.llm_provider.generate(
            system_prompt=_SYSTEM_PROMPT,
            user_prompt=user_prompt,
            context_chunks=relevant_chunks,
        )

        fuentes = [
            FuenteCitada(
                documento=chunk.metadata.documento,
                seccion=chunk.metadata.seccion,
                fragmento=_truncate_fragment(chunk.texto, max_length=320),
                puntaje=round(max(0.0, min(1.0, float(score))), 4),
            )
            for chunk, score in relevant_chunks
        ]

        return PreguntaResponse(
            respuesta=respuesta,
            fuentes=fuentes,
            tokens_usados=tokens_usados,
            anclado=True,
            proveedor_llm=proveedor_llm,
        )
