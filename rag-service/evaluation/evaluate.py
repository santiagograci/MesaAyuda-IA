from __future__ import annotations

import json
import sys
import time
import unicodedata
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

RAG_SERVICE_DIR = Path(__file__).resolve().parents[1]
if str(RAG_SERVICE_DIR) not in sys.path:
    sys.path.insert(0, str(RAG_SERVICE_DIR))

from app.config import settings
from app.llm_provider import GroundedExtractiveMockProvider
from app.models import PreguntaRequest
from app.rag_engine import RagEngine
from app.vector_store import HybridVectorStore


def _normalize(text: str) -> str:
    decomposed = unicodedata.normalize("NFKD", text.lower())
    return "".join(ch for ch in decomposed if not unicodedata.combining(ch))


def _resolve_paths() -> tuple[Path, Path, Path]:
    dataset_path = Path(__file__).resolve().parent / "dataset_10_preguntas.json"
    corpus_path = RAG_SERVICE_DIR.parent / "corpus"
    if not corpus_path.is_dir():
        corpus_path = Path("/app/corpus")

    docs_dir = RAG_SERVICE_DIR.parent / "docs"
    if not docs_dir.parent.is_dir():
        docs_dir = Path("/app/docs")
    output_path = docs_dir / "evaluacion-rag.md"
    return dataset_path, corpus_path, output_path


def run_evaluation() -> dict[str, Any]:
    """Ejecuta la batería de 10 preguntas sobre el motor RAG y genera el reporte Markdown."""
    dataset_path, corpus_path, output_path = _resolve_paths()
    dataset: list[dict[str, Any]] = json.loads(
        dataset_path.read_text(encoding="utf-8")
    )

    vector_store = HybridVectorStore(in_memory=True)
    llm_provider = GroundedExtractiveMockProvider()
    engine = RagEngine(vector_store=vector_store, llm_provider=llm_provider)

    ingest_res = engine.ingest(str(corpus_path))

    in_domain_hits = 0
    in_domain_total = 0
    faithfulness_ratios: list[float] = []
    out_domain_rejected = 0
    out_domain_total = 0
    latencies_ms: list[float] = []
    rows: list[dict[str, Any]] = []

    for item in dataset:
        qid = str(item["id"])
        qtype = str(item["tipo"])
        pregunta = str(item["pregunta"])
        expected_doc = item.get("documento_esperado")
        expected_keywords: list[str] = list(item.get("palabras_clave_esperadas") or [])

        start = time.perf_counter()
        response = engine.ask(PreguntaRequest(pregunta=pregunta, top_k=4))
        elapsed_ms = (time.perf_counter() - start) * 1000.0
        latencies_ms.append(elapsed_ms)

        retrieved_docs = [f.documento for f in response.fuentes]
        top1_doc = retrieved_docs[0] if retrieved_docs else "Ninguno"
        top1_score = response.fuentes[0].puntaje if response.fuentes else 0.0

        if qtype == "in_domain":
            in_domain_total += 1
            hit = bool(expected_doc and expected_doc in retrieved_docs)
            if hit:
                in_domain_hits += 1

            normalized_answer = _normalize(response.respuesta)
            if expected_keywords:
                matched_kw = sum(
                    1 for kw in expected_keywords if _normalize(kw) in normalized_answer
                )
                kw_ratio = matched_kw / len(expected_keywords)
            else:
                kw_ratio = 1.0 if response.anclado else 0.0
            faithfulness_ratios.append(kw_ratio)

            passed = response.anclado and hit and kw_ratio == 1.0
            estado = f"Anclado ({matched_kw}/{len(expected_keywords)} kw)"
            veredicto = "PASS" if passed else "FAIL"
        else:
            out_domain_total += 1
            rejected = not response.anclado and len(response.fuentes) == 0
            if rejected:
                out_domain_rejected += 1
            estado = "Rechazado (anclado=False)" if rejected else "Falso positivo"
            veredicto = "PASS" if rejected else "FAIL"

        rows.append(
            {
                "id": qid,
                "tipo": qtype,
                "documento_esperado": expected_doc or "N/A (Fuera de dominio)",
                "documento_recuperado": top1_doc,
                "puntaje_top1": top1_score,
                "estado": estado,
                "veredicto": veredicto,
                "latencia_ms": round(elapsed_ms, 2),
            }
        )

    hit_rate = (in_domain_hits / in_domain_total * 100.0) if in_domain_total else 0.0
    faithfulness = (
        sum(faithfulness_ratios) / len(faithfulness_ratios) * 100.0
        if faithfulness_ratios
        else 0.0
    )
    rejection_acc = (
        out_domain_rejected / out_domain_total * 100.0 if out_domain_total else 0.0
    )
    avg_latency = sum(latencies_ms) / len(latencies_ms) if latencies_ms else 0.0

    summary = {
        "documentos_procesados": ingest_res.documentos_procesados,
        "fragmentos_indexados": ingest_res.fragmentos_indexados,
        "hit_rate": round(hit_rate, 2),
        "faithfulness": round(faithfulness, 2),
        "rejection_accuracy": round(rejection_acc, 2),
        "avg_latency_ms": round(avg_latency, 2),
        "rows": rows,
    }

    _print_console_report(summary)
    _write_markdown_report(output_path, summary)
    return summary


def _print_console_report(summary: dict[str, Any]) -> None:
    print("\n=== Evaluación RAG - MesaAyuda IA ===")
    print(
        f"Corpus: {summary['documentos_procesados']} documentos | "
        f"{summary['fragmentos_indexados']} fragmentos indexados"
    )
    print(f"1. Retrieval Hit Rate @ 4:              {summary['hit_rate']:.2f}%")
    print(f"2. Answer Groundedness / Faithfulness:  {summary['faithfulness']:.2f}%")
    print(f"3. Out-of-Domain Rejection Accuracy:    {summary['rejection_accuracy']:.2f}%")
    print(f"4. Latencia promedio por consulta:      {summary['avg_latency_ms']:.2f} ms\n")

    for row in summary["rows"]:
        print(
            f"[{row['veredicto']}] {row['id']} ({row['tipo']}) | "
            f"Esperado: {row['documento_esperado']} | "
            f"Top-1: {row['documento_recuperado']} ({row['puntaje_top1']:.4f}) | "
            f"{row['estado']}"
        )


def _write_markdown_report(output_path: Path, summary: dict[str, Any]) -> None:
    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    lines = [
        "# Evaluación Empírica del Motor RAG — MesaAyuda IA",
        "",
        f"Fecha de ejecución: `{timestamp}`",
        "",
        "## Configuración Evaluada",
        "",
        "| Parámetro | Valor |",
        "| :--- | :--- |",
        f"| Modelo de Embeddings | `{settings.embedding_model}` |",
        "| Estrategia de Recuperación | Híbrida (`0.70 Vector Coseno + 0.30 BM25Okapi`) |",
        f"| Tamaño de Fragmento (`chunk_size`) | `{settings.chunk_size}` caracteres |",
        f"| Solapamiento (`chunk_overlap`) | `{settings.chunk_overlap}` caracteres |",
        f"| Umbral de Similitud (`similarity_threshold`) | `{settings.similarity_threshold}` |",
        f"| Proveedor LLM Evaluado | `{GroundedExtractiveMockProvider.PROVIDER_NAME}` |",
        f"| Documentos Indexados | `{summary['documentos_procesados']}` |",
        f"| Fragmentos Indexados | `{summary['fragmentos_indexados']}` |",
        "",
        "## Métricas Globales Obtenidas",
        "",
        "| Métrica | Resultado |",
        "| :--- | :---: |",
        f"| **Retrieval Hit Rate @ 4** | **{summary['hit_rate']:.2f}%** |",
        f"| **Answer Groundedness / Keyword Faithfulness** | **{summary['faithfulness']:.2f}%** |",
        f"| **Out-of-Domain Rejection Accuracy** | **{summary['rejection_accuracy']:.2f}%** |",
        f"| Latencia Promedio por Consulta | {summary['avg_latency_ms']:.2f} ms |",
        "",
        "## Resultados Detallados por Pregunta (Dataset de 10 Casos)",
        "",
        "| ID | Tipo | Documento Esperado | Documento Recuperado (Top-1) | Puntaje Top-1 | Estado | Veredicto |",
        "| :--- | :--- | :--- | :--- | :---: | :--- | :---: |",
    ]

    for row in summary["rows"]:
        lines.append(
            f"| {row['id']} | `{row['tipo']}` | `{row['documento_esperado']}` | "
            f"`{row['documento_recuperado']}` | {row['puntaje_top1']:.4f} | "
            f"{row['estado']} | **{row['veredicto']}** |"
        )

    lines.append("")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


if __name__ == "__main__":
    run_evaluation()
