from __future__ import annotations

import re
from pathlib import Path

from pypdf import PdfReader

from app.models import ChunkMetadata, TextChunk

_SUPPORTED_EXTENSIONS: frozenset[str] = frozenset({".md", ".txt", ".pdf"})
_SEPARATORS: tuple[str, ...] = ("\n\n", "\n", ". ", "; ", " ", "")


def clean_text(text: str) -> str:
    """Normaliza espacios, saltos de línea y caracteres nulos preservando Markdown."""
    if not text:
        return ""

    normalized = text.replace("\x00", "").replace("\r\n", "\n").replace("\r", "\n")
    normalized = normalized.replace("\u00a0", " ")

    cleaned_lines = [
        re.sub(r"[ \t]{2,}", " ", line.rstrip())
        for line in normalized.split("\n")
    ]
    joined = "\n".join(cleaned_lines)
    joined = re.sub(r"\n{3,}", "\n\n", joined)
    return joined.strip()


def parse_markdown_sections(content: str, filename: str) -> list[tuple[str, str]]:
    """Divide contenido Markdown por encabezados `##` y `###` conservando jerarquía."""
    cleaned = clean_text(content)
    if not cleaned:
        return []

    default_title = Path(filename).stem
    doc_title: str | None = None
    current_h2: str | None = None
    current_section: str = default_title
    buffer: list[str] = []
    has_body = False
    sections: list[tuple[str, str]] = []
    in_code_block = False

    def flush() -> None:
        nonlocal has_body
        body = "\n".join(buffer).strip()
        if body and has_body:
            sections.append((current_section, body))
        buffer.clear()
        has_body = False

    for line in cleaned.split("\n"):
        stripped = line.strip()
        if stripped.startswith("```"):
            in_code_block = not in_code_block
            buffer.append(line)
            has_body = True
            continue

        if not in_code_block:
            if line.startswith("# ") and not line.startswith("## "):
                flush()
                doc_title = line[2:].strip() or default_title
                current_h2 = None
                current_section = doc_title
                continue

            if line.startswith("## ") and not line.startswith("### "):
                flush()
                current_h2 = line[3:].strip() or default_title
                current_section = current_h2
                buffer.append(line)
                continue

            if line.startswith("### ") and not line.startswith("#### "):
                flush()
                h3_title = line[4:].strip() or default_title
                current_section = (
                    f"{current_h2} > {h3_title}" if current_h2 else h3_title
                )
                heading_prefix = f"## {current_h2}\n{line}" if current_h2 else line
                buffer.append(heading_prefix)
                continue

        if stripped:
            has_body = True
        buffer.append(line)

    flush()

    if not sections:
        return [(doc_title or default_title, cleaned)]
    return sections


def parse_pdf_pages(file_path: Path) -> list[tuple[str, str]]:
    """Extrae texto limpio por página desde un archivo PDF."""
    reader = PdfReader(str(file_path))
    pages: list[tuple[str, str]] = []
    for idx, page in enumerate(reader.pages):
        raw_text = page.extract_text() or ""
        cleaned = clean_text(raw_text)
        if cleaned:
            pages.append((f"Página {idx + 1}", cleaned))
    return pages


def parse_txt_sections(content: str, filename: str) -> list[tuple[str, str]]:
    """Limpia texto plano `.txt` preservando el nombre del documento como sección."""
    cleaned = clean_text(content)
    if not cleaned:
        return []
    return [(Path(filename).stem, cleaned)]


def sanitize_upload_filename(filename: str) -> str:
    """Normaliza el nombre de archivo subido y valida que tenga extensión soportada."""
    raw_name = Path(filename.strip()).name
    if not raw_name or raw_name in {".", ".."}:
        raise ValueError("El nombre del archivo es inválido.")

    ext = Path(raw_name).suffix.lower()
    if ext not in _SUPPORTED_EXTENSIONS:
        raise ValueError("Formato no soportado. Solo se permiten archivos .md, .txt o .pdf.")

    stem = re.sub(r"[^a-zA-Z0-9._-]+", "-", Path(raw_name).stem).strip("-.")
    if not stem:
        stem = "manual-erp"

    return f"{stem}{ext}"


def _split_segment(segment: str, chunk_size: int, separators: tuple[str, ...]) -> list[str]:
    """Divide recursivamente un segmento en unidades atómicas <= `chunk_size`."""
    text = segment.strip()
    if not text:
        return []
    if len(text) <= chunk_size:
        return [text]

    for idx, sep in enumerate(separators):
        if sep == "":
            return [
                text[start : start + chunk_size].strip()
                for start in range(0, len(text), chunk_size)
                if text[start : start + chunk_size].strip()
            ]

        if sep not in text:
            continue

        raw_parts = text.split(sep)
        parts: list[str] = []
        keep_punct = sep in {". ", "; "}
        punct = sep[0] if keep_punct else ""

        for part_idx, part in enumerate(raw_parts):
            piece = part.strip()
            if not piece:
                continue
            if keep_punct and part_idx < len(raw_parts) - 1 and not piece.endswith(punct):
                piece = f"{piece}{punct}"
            parts.append(piece)

        atomic_units: list[str] = []
        for piece in parts:
            if len(piece) > chunk_size:
                atomic_units.extend(_split_segment(piece, chunk_size, separators[idx + 1 :]))
            else:
                atomic_units.append(piece)

        if atomic_units:
            return atomic_units

    return [text[:chunk_size]]


def _extract_overlap_tail(previous_chunk: str, max_overlap: int) -> str:
    """Extrae una cola de solapamiento alineada a límite de palabra o línea."""
    if max_overlap <= 0 or not previous_chunk:
        return ""
    if len(previous_chunk) <= max_overlap:
        return previous_chunk

    tail = previous_chunk[-max_overlap:]
    boundary = re.search(r"[\s\n]+", tail)
    if boundary and boundary.end() < len(tail):
        aligned = tail[boundary.end() :].strip()
        if aligned:
            return aligned
    return tail.strip()


def chunk_text(text: str, chunk_size: int, chunk_overlap: int) -> list[str]:
    """Divide texto en fragmentos <= `chunk_size` aplicando solapamiento `chunk_overlap`."""
    if chunk_size <= 0:
        raise ValueError("chunk_size debe ser mayor que 0.")
    if chunk_overlap < 0:
        raise ValueError("chunk_overlap no puede ser negativo.")
    if chunk_overlap >= chunk_size:
        raise ValueError("chunk_overlap debe ser estrictamente menor que chunk_size.")

    cleaned = clean_text(text)
    if not cleaned:
        return []
    if len(cleaned) <= chunk_size:
        return [cleaned]

    units = _split_segment(cleaned, chunk_size, _SEPARATORS)
    if not units:
        return []

    chunks: list[str] = []
    current = ""

    for unit in units:
        if not current:
            current = unit
            continue

        candidate = f"{current}\n{unit}"
        if len(candidate) <= chunk_size:
            current = candidate
            continue

        chunks.append(current)
        available_for_overlap = chunk_size - len(unit) - 1
        effective_overlap = min(chunk_overlap, available_for_overlap)
        overlap_tail = _extract_overlap_tail(current, effective_overlap)
        current = f"{overlap_tail}\n{unit}" if overlap_tail else unit

    if current:
        chunks.append(current)

    return chunks


def load_and_chunk_corpus(
    corpus_path: str,
    chunk_size: int,
    chunk_overlap: int,
) -> tuple[int, list[TextChunk]]:
    """Carga archivos `.md` y `.pdf` de `corpus_path` y genera fragmentos tipados."""
    root = Path(corpus_path)
    if not root.exists():
        raise FileNotFoundError(f"La ruta del corpus no existe: {corpus_path}")
    if not root.is_dir():
        raise NotADirectoryError(f"La ruta del corpus no es un directorio: {corpus_path}")

    files = sorted(
        path
        for path in root.rglob("*")
        if path.is_file() and path.suffix.lower() in _SUPPORTED_EXTENSIONS
    )

    chunks: list[TextChunk] = []
    documents_processed = 0

    for file_path in files:
        suffix = file_path.suffix.lower()
        if suffix == ".md":
            content = file_path.read_text(encoding="utf-8", errors="replace")
            sections = parse_markdown_sections(content, file_path.name)
        elif suffix == ".txt":
            content = file_path.read_text(encoding="utf-8", errors="replace")
            sections = parse_txt_sections(content, file_path.name)
        else:
            sections = parse_pdf_pages(file_path)

        if not sections:
            continue

        documents_processed += 1
        rel_id = file_path.relative_to(root).with_suffix("").as_posix().replace("/", "_")
        doc_chunk_idx = 0

        for section_name, section_text in sections:
            section_chunks = chunk_text(section_text, chunk_size, chunk_overlap)
            for fragment in section_chunks:
                chunk_id = f"{rel_id}__chunk_{doc_chunk_idx:04d}"
                chunks.append(
                    TextChunk(
                        id=chunk_id,
                        texto=fragment,
                        metadata=ChunkMetadata(
                            documento=file_path.name,
                            seccion=section_name,
                            chunk_index=doc_chunk_idx,
                        ),
                    )
                )
                doc_chunk_idx += 1

    return documents_processed, chunks
