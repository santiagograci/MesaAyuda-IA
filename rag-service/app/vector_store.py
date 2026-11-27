from __future__ import annotations

import functools
import re
import unicodedata
import uuid
from pathlib import Path
from typing import Any

import chromadb
from chromadb.config import Settings as ChromaSettings
from rank_bm25 import BM25Okapi
from sentence_transformers import SentenceTransformer

from app.config import settings
from app.models import ChunkMetadata, TextChunk

_STOPWORDS: frozenset[str] = frozenset(
    {
        "a", "al", "algo", "algun", "alguna", "algunas", "alguno", "algunos", "ante",
        "antes", "como", "con", "contra", "cual", "cuales", "cuando", "de", "del",
        "desde", "donde", "e", "el", "ella", "ellas", "ello", "ellos", "en", "entre",
        "era", "es", "esa", "esas", "ese", "eso", "esos", "esta", "estas", "este",
        "esto", "estos", "fue", "ha", "hacia", "hasta", "hay", "la", "las", "le",
        "les", "lo", "los", "mas", "me", "mi", "mis", "muy", "ni", "no", "nos", "o",
        "para", "pero", "por", "porque", "que", "quien", "quienes", "se", "sea",
        "segun", "si", "sin", "sobre", "son", "su", "sus", "tambien", "te", "tiene",
        "toda", "todas", "todo", "todos", "tu", "tus", "un", "una", "unas", "uno",
        "unos", "y", "ya", "yo",
        "the", "is", "are", "to", "of", "in", "for", "on", "with", "as", "at", "by",
        "an", "and", "or",
    }
)


def _tokenize(text: str) -> list[str]:
    """Tokeniza texto filtrando stopwords y preservando códigos alfanuméricos ERP."""
    normalized = unicodedata.normalize("NFKD", text.lower())
    without_accents = "".join(
        ch for ch in normalized if not unicodedata.combining(ch)
    )
    hyphen_codes = [
        code.replace("-", "")
        for code in re.findall(r"[a-z]{2,4}-[a-z0-9]{2,4}", without_accents)
    ]
    words = [
        token
        for token in re.findall(r"[a-z0-9]+", without_accents)
        if token not in _STOPWORDS and len(token) > 1
    ]
    return words + hyphen_codes


class HybridVectorStore:
    """Almacén híbrido que combina búsqueda vectorial (ChromaDB) y léxica (BM25)."""

    def __init__(
        self,
        embedding_model: str | None = None,
        persist_dir: str | None = None,
        collection_name: str | None = None,
        in_memory: bool = False,
        embedding_cache_size: int = 256,
        embedder: Any | None = None,
    ) -> None:
        self.embedding_model_name = embedding_model or settings.embedding_model
        self.persist_dir = persist_dir or settings.chroma_persist_dir
        self.in_memory = in_memory
        if in_memory and collection_name is None:
            self.collection_name = f"{settings.collection_name}_{uuid.uuid4().hex[:8]}"
        else:
            self.collection_name = collection_name or settings.collection_name

        self._embedder: Any = (
            embedder
            if embedder is not None
            else SentenceTransformer(self.embedding_model_name)
        )

        chroma_settings = ChromaSettings(anonymized_telemetry=False)
        if self.in_memory:
            self._client = chromadb.EphemeralClient(settings=chroma_settings)
        else:
            Path(self.persist_dir).mkdir(parents=True, exist_ok=True)
            self._client = chromadb.PersistentClient(
                path=self.persist_dir,
                settings=chroma_settings,
            )

        self._collection = self._client.get_or_create_collection(
            name=self.collection_name,
            metadata={"hnsw:space": "cosine"},
        )

        self._chunks: list[TextChunk] = []
        self._chunk_by_id: dict[str, TextChunk] = {}
        self._bm25: BM25Okapi | None = None

        self._encode_query_cached = functools.lru_cache(maxsize=embedding_cache_size)(
            self._encode_query_raw
        )

        if self._collection.count() > 0:
            self._hydrate_from_collection()

    def _encode_query_raw(self, normalized_query: str) -> tuple[float, ...]:
        vector = self._embedder.encode(
            [normalized_query],
            normalize_embeddings=True,
            convert_to_numpy=True,
            show_progress_bar=False,
        )[0]
        return tuple(float(x) for x in vector)

    def _get_query_embedding(self, query: str) -> list[float]:
        normalized_query = " ".join(query.strip().split())
        return list(self._encode_query_cached(normalized_query))

    def _hydrate_from_collection(self) -> None:
        data = self._collection.get(include=["documents", "metadatas"])
        ids = data.get("ids") or []
        documents = data.get("documents") or []
        metadatas = data.get("metadatas") or []

        hydrated: list[TextChunk] = []
        for chunk_id, doc_text, meta in zip(ids, documents, metadatas):
            meta_dict = meta or {}
            hydrated.append(
                TextChunk(
                    id=str(chunk_id),
                    texto=str(doc_text or ""),
                    metadata=ChunkMetadata(
                        documento=str(meta_dict.get("documento", "")),
                        seccion=str(meta_dict.get("seccion", "")),
                        chunk_index=int(meta_dict.get("chunk_index", 0)),
                    ),
                )
            )

        self._set_lexical_index(hydrated)

    def _set_lexical_index(self, chunks: list[TextChunk]) -> None:
        self._chunks = list(chunks)
        self._chunk_by_id = {chunk.id: chunk for chunk in self._chunks}
        if self._chunks:
            tokenized_corpus = [_tokenize(chunk.texto) for chunk in self._chunks]
            self._bm25 = BM25Okapi(tokenized_corpus)
        else:
            self._bm25 = None

    def index_chunks(self, chunks: list[TextChunk]) -> int:
        """Recrea la colección vectorial e indexa los fragmentos en ChromaDB y BM25."""
        try:
            self._client.delete_collection(name=self.collection_name)
        except Exception:
            pass

        self._collection = self._client.get_or_create_collection(
            name=self.collection_name,
            metadata={"hnsw:space": "cosine"},
        )
        self._encode_query_cached.cache_clear()

        if not chunks:
            self._set_lexical_index([])
            return 0

        texts = [chunk.texto for chunk in chunks]
        embeddings = self._embedder.encode(
            texts,
            normalize_embeddings=True,
            convert_to_numpy=True,
            show_progress_bar=False,
        )

        batch_size = 500
        for start in range(0, len(chunks), batch_size):
            end = start + batch_size
            batch_chunks = chunks[start:end]
            batch_embeddings = [
                [float(v) for v in vec] for vec in embeddings[start:end]
            ]
            self._collection.add(
                ids=[c.id for c in batch_chunks],
                documents=[c.texto for c in batch_chunks],
                metadatas=[c.metadata.model_dump() for c in batch_chunks],
                embeddings=batch_embeddings,
            )

        self._set_lexical_index(chunks)
        return len(chunks)

    def search(self, query: str, top_k: int = 4) -> list[tuple[TextChunk, float]]:
        """Ejecuta búsqueda híbrida (0.70 vectorial + 0.30 BM25) y retorna top_k."""
        if not query.strip() or top_k <= 0:
            return []

        total_indexed = self.count()
        if total_indexed == 0:
            return []

        query_embedding = self._get_query_embedding(query)
        n_results = min(total_indexed, max(top_k * 5, 100))

        vector_scores: dict[str, float] = {}
        results = self._collection.query(
            query_embeddings=[query_embedding],
            n_results=n_results,
            include=["distances"],
        )
        result_ids = (results.get("ids") or [[]])[0]
        result_distances = (results.get("distances") or [[]])[0]

        for chunk_id, distance in zip(result_ids, result_distances):
            cosine_sim = max(0.0, min(1.0, 1.0 - float(distance)))
            vector_scores[str(chunk_id)] = cosine_sim

        bm25_scores: dict[str, float] = {}
        max_bm25 = 0.0
        if self._bm25 is not None and self._chunks:
            query_tokens = _tokenize(query)
            if query_tokens:
                raw_scores = [max(0.0, float(s)) for s in self._bm25.get_scores(query_tokens)]
                max_bm25 = max(raw_scores, default=0.0)
                if max_bm25 > 0.0:
                    denom = max(max_bm25, 6.0)
                    for chunk, raw_score in zip(self._chunks, raw_scores):
                        if raw_score > 0.0:
                            bm25_scores[chunk.id] = raw_score / denom

        candidate_ids = set(vector_scores.keys()) | set(bm25_scores.keys())
        scored_results: list[tuple[TextChunk, float]] = []

        for chunk_id in candidate_ids:
            chunk = self._chunk_by_id.get(chunk_id)
            if chunk is None:
                continue
            v_score = vector_scores.get(chunk_id, 0.0)
            b_score = bm25_scores.get(chunk_id, 0.0)
            combined_raw = 0.70 * v_score + 0.30 * b_score
            if max_bm25 == 0.0 and v_score < 0.75:
                combined_raw *= 0.50
            combined = round(max(0.0, min(1.0, combined_raw)), 4)
            scored_results.append((chunk, combined))

        scored_results.sort(key=lambda item: (-item[1], item[0].id))
        return scored_results[:top_k]

    def count(self) -> int:
        """Retorna la cantidad de fragmentos actualmente indexados."""
        return int(self._collection.count())
