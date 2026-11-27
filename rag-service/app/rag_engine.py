from __future__ import annotations

import re
import shutil
from pathlib import Path

from app.config import settings
from app.ingestion import load_and_chunk_corpus, sanitize_upload_filename
from app.llm_provider import LLMProvider
from app.models import (
    DocumentoIndexado,
    DocumentosResponse,
    FuenteCitada,
    IngestaArchivoResponse,
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

    def list_documents(self) -> DocumentosResponse:
        """Devuelve la lista de documentos únicos indexados y sus fragmentos."""
        pairs = self.vector_store.list_documents()
        documentos = [
            DocumentoIndexado(documento=doc, fragmentos=count)
            for doc, count in pairs
        ]
        return DocumentosResponse(
            documentos=documentos,
            total_documentos=len(documentos),
            total_fragmentos=sum(item.fragmentos for item in documentos),
        )

    def ingest_uploaded_file(
        self,
        filename: str,
        content: bytes,
        corpus_path: str | None = None,
    ) -> IngestaArchivoResponse:
        """Guarda un manual subido, ejecuta reindexación completa e invalida caché."""
        max_bytes = 5 * 1024 * 1024
        safe_name = sanitize_upload_filename(filename)

        if not content:
            raise ValueError("El archivo subido está vacío.")
        if len(content) > max_bytes:
            raise ValueError("El archivo excede el tamaño máximo permitido de 5 MB.")

        target_dir = Path(corpus_path or settings.corpus_path)
        target_dir.mkdir(parents=True, exist_ok=True)
        target_file = target_dir / safe_name
        backup_bytes = target_file.read_bytes() if target_file.exists() else None
        target_file.write_bytes(content)

        try:
            ingest_result = self.ingest(str(target_dir))
            doc_counts = dict(self.vector_store.list_documents())
            fragmentos_archivo = doc_counts.get(safe_name, 0)
            if fragmentos_archivo == 0:
                raise ValueError(
                    "No se pudo extraer contenido indexable del archivo proporcionado."
                )
        except Exception:
            if backup_bytes is not None:
                target_file.write_bytes(backup_bytes)
            elif target_file.exists():
                target_file.unlink()
            try:
                self.ingest(str(target_dir))
            except Exception:
                pass
            raise

        return IngestaArchivoResponse(
            archivo=safe_name,
            fragmentos_archivo=fragmentos_archivo,
            total_documentos=ingest_result.documentos_procesados,
            total_fragmentos=ingest_result.fragmentos_indexados,
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
