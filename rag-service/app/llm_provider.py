from __future__ import annotations

import logging
import re
from abc import ABC, abstractmethod

import httpx

from app.config import settings
from app.models import TextChunk

logger = logging.getLogger(__name__)


class LLMProvider(ABC):
    """Interfaz Strategy para proveedores de generación de respuestas del motor RAG."""

    @abstractmethod
    def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        context_chunks: list[tuple[TextChunk, float]],
    ) -> tuple[str, int, str]:
        """Genera una respuesta anclada al contexto y retorna `(respuesta, tokens, proveedor)`."""


class GroundedExtractiveMockProvider(LLMProvider):
    """Proveedor determinista gratuito estrictamente anclado a los fragmentos recuperados."""

    PROVIDER_NAME = "grounded-mock"

    @staticmethod
    def _extract_clean_points(texto: str, max_lines: int = 12) -> list[str]:
        points: list[str] = []
        for raw_line in texto.splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or line.startswith("| :---"):
                continue
            if line.startswith("|") and line.endswith("|"):
                cells = [cell.strip() for cell in line.strip("|").split("|") if cell.strip()]
                if cells:
                    line = " — ".join(cells)
            line = re.sub(r"\s+", " ", line)
            if line not in points:
                points.append(line)
            if len(points) >= max_lines:
                break
        return points

    def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        context_chunks: list[tuple[TextChunk, float]],
    ) -> tuple[str, int, str]:
        if not context_chunks:
            respuesta = (
                "No se encontraron fragmentos de contexto suficientes para redactar "
                "una guía operativa."
            )
            tokens = max(1, (len(system_prompt) + len(user_prompt) + len(respuesta)) // 4)
            return respuesta, tokens, self.PROVIDER_NAME

        top_chunks = context_chunks[:4]
        sections_output: list[str] = [
            "De acuerdo con la documentación oficial de Ofima ERP, tenga en cuenta los siguientes lineamientos y pasos operativos:"
        ]
        seen_lines: set[str] = set()
        citations: list[str] = []

        for chunk, _score in top_chunks:
            doc = chunk.metadata.documento
            sec = chunk.metadata.seccion
            citation = f"[Fuente: {doc} - {sec}]"
            if citation not in citations:
                citations.append(citation)

            extracted = self._extract_clean_points(chunk.texto)
            unique_lines = [line for line in extracted if line not in seen_lines]
            if not unique_lines:
                continue

            seen_lines.update(unique_lines)
            sections_output.append(f"\n**{sec}** ({doc}):")
            for line in unique_lines:
                if re.match(r"^\d+\.\s", line) or line.startswith("- "):
                    sections_output.append(line)
                else:
                    sections_output.append(f"- {line}")

        if citations:
            sections_output.append("\nReferencias consultadas:\n" + "\n".join(citations))

        respuesta = "\n".join(sections_output).strip()
        tokens = max(1, (len(system_prompt) + len(user_prompt) + len(respuesta)) // 4)
        return respuesta, tokens, self.PROVIDER_NAME


class GeminiLLMProvider(LLMProvider):
    """Proveedor conectado a Gemini API con fallback automático al proveedor mock."""

    PROVIDER_NAME = "gemini"

    def __init__(
        self,
        api_key: str | None = None,
        model: str = "gemini-2.0-flash",
        timeout: float = 15.0,
        fallback: LLMProvider | None = None,
    ) -> None:
        self._api_key = (api_key if api_key is not None else settings.gemini_api_key).strip()
        self._model = model
        self._timeout = timeout
        self._fallback = fallback or GroundedExtractiveMockProvider()

    def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        context_chunks: list[tuple[TextChunk, float]],
    ) -> tuple[str, int, str]:
        if not self._api_key:
            logger.warning("GEMINI_API_KEY no configurada; usando fallback grounded-mock.")
            return self._fallback.generate(system_prompt, user_prompt, context_chunks)

        url = (
            f"https://generativelanguage.googleapis.com/v1beta/models/"
            f"{self._model}:generateContent?key={self._api_key}"
        )
        payload = {
            "system_instruction": {"parts": [{"text": system_prompt}]},
            "contents": [{"role": "user", "parts": [{"text": user_prompt}]}],
            "generationConfig": {"temperature": 0.1},
        }

        try:
            with httpx.Client(timeout=self._timeout) as client:
                response = client.post(url, json=payload)
                response.raise_for_status()
                data = response.json()

            candidates = data.get("candidates") or []
            parts = ((candidates[0].get("content") or {}).get("parts") or []) if candidates else []
            text = "".join(str(p.get("text", "")) for p in parts).strip()
            if not text:
                raise ValueError("Respuesta vacía de Gemini API.")

            usage = data.get("usageMetadata") or {}
            total_tokens = int(
                usage.get("totalTokenCount")
                or max(1, (len(system_prompt) + len(user_prompt) + len(text)) // 4)
            )
            return text, total_tokens, self.PROVIDER_NAME
        except Exception as exc:
            logger.warning("Fallo en GeminiLLMProvider (%s); usando fallback.", exc)
            return self._fallback.generate(system_prompt, user_prompt, context_chunks)


class OpenAILLMProvider(LLMProvider):
    """Proveedor conectado a OpenAI Chat Completions con fallback automático al proveedor mock."""

    PROVIDER_NAME = "openai"

    def __init__(
        self,
        api_key: str | None = None,
        model: str = "gpt-4o-mini",
        timeout: float = 15.0,
        fallback: LLMProvider | None = None,
    ) -> None:
        self._api_key = (api_key if api_key is not None else settings.openai_api_key).strip()
        self._model = model
        self._timeout = timeout
        self._fallback = fallback or GroundedExtractiveMockProvider()

    def generate(
        self,
        system_prompt: str,
        user_prompt: str,
        context_chunks: list[tuple[TextChunk, float]],
    ) -> tuple[str, int, str]:
        if not self._api_key:
            logger.warning("OPENAI_API_KEY no configurada; usando fallback grounded-mock.")
            return self._fallback.generate(system_prompt, user_prompt, context_chunks)

        url = "https://api.openai.com/v1/chat/completions"
        headers = {
            "Authorization": f"Bearer {self._api_key}",
            "Content-Type": "application/json",
        }
        payload = {
            "model": self._model,
            "temperature": 0.1,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
        }

        try:
            with httpx.Client(timeout=self._timeout) as client:
                response = client.post(url, headers=headers, json=payload)
                response.raise_for_status()
                data = response.json()

            choices = data.get("choices") or []
            text = (
                str(((choices[0].get("message") or {}).get("content")) or "").strip()
                if choices
                else ""
            )
            if not text:
                raise ValueError("Respuesta vacía de OpenAI API.")

            usage = data.get("usage") or {}
            total_tokens = int(
                usage.get("total_tokens")
                or max(1, (len(system_prompt) + len(user_prompt) + len(text)) // 4)
            )
            return text, total_tokens, self.PROVIDER_NAME
        except Exception as exc:
            logger.warning("Fallo en OpenAILLMProvider (%s); usando fallback.", exc)
            return self._fallback.generate(system_prompt, user_prompt, context_chunks)


def get_llm_provider(provider_name: str | None = None) -> LLMProvider:
    """Factory que retorna la implementación Strategy configurada en `LLM_PROVIDER`."""
    selected = (provider_name or settings.llm_provider).strip().lower()
    if selected == "gemini":
        return GeminiLLMProvider()
    if selected == "openai":
        return OpenAILLMProvider()
    return GroundedExtractiveMockProvider()
