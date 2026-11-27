from __future__ import annotations

import re
import unicodedata

_REJECTION_MESSAGE = (
    "Consulta rechazada por políticas de seguridad: se detectó un intento de "
    "manipulación de instrucciones (prompt injection)."
)

_CONTROL_CHARS_RE = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f\u200b-\u200f\ufeff]")

_INJECTION_PATTERNS: tuple[re.Pattern[str], ...] = tuple(
    re.compile(pattern, re.IGNORECASE)
    for pattern in (
        r"\bignora\s+(?:todas\s+)?(?:tus\s+|las\s+)?instrucciones\b",
        r"\bignore\s+(?:all\s+)?(?:previous\s+|prior\s+|your\s+)?instructions\b",
        r"\bolvida\s+(?:todas\s+)?(?:tus\s+|las\s+)?(?:reglas|instrucciones)\b",
        r"\bforget\s+(?:your\s+|all\s+|previous\s+)?instructions\b",
        r"\bactua\s+como\b",
        r"\bpretend\s+to\s+be\b",
        r"\byou\s+are\s+now\b",
        r"\bahora\s+eres\b",
        r"\bsystem\s+prompt\b",
        r"\bprompt\s+del\s+sistema\b",
        r"\brevela\s+(?:tu|tus)\b",
        r"\bmuestra\s+tus\s+instrucciones\b",
        r"\bjailbreak\b",
        r"\bdan\s+mode\b",
        r"\bbypass\b",
    )
)


class PromptInjectionError(Exception):
    """Excepción lanzada cuando la consulta intenta manipular las instrucciones del sistema."""


def _normalize_for_detection(text: str) -> str:
    """Normaliza a minúsculas sin tildes ni espacios repetidos para evaluar patrones."""
    decomposed = unicodedata.normalize("NFKD", text.lower())
    without_accents = "".join(
        ch for ch in decomposed if not unicodedata.combining(ch)
    )
    return re.sub(r"\s+", " ", without_accents).strip()


def validate_and_sanitize_query(query: str) -> str:
    """Sanitiza la consulta del usuario y rechaza intentos de prompt injection."""
    without_control = _CONTROL_CHARS_RE.sub("", query)
    sanitized = re.sub(r"\s+", " ", without_control).strip()

    normalized = _normalize_for_detection(sanitized)
    for pattern in _INJECTION_PATTERNS:
        if pattern.search(normalized):
            raise PromptInjectionError(_REJECTION_MESSAGE)

    return sanitized
