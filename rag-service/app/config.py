from __future__ import annotations

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Configuración centralizada del servicio RAG leída desde entorno o `.env`."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
        case_sensitive=False,
    )

    corpus_path: str = "/app/corpus"
    chroma_persist_dir: str = "/tmp/chroma_data"
    collection_name: str = "manuales_erp"
    embedding_model: str = "all-MiniLM-L6-v2"
    chunk_size: int = 650
    chunk_overlap: int = 120
    similarity_threshold: float = 0.35
    llm_provider: str = "mock"
    gemini_api_key: str = ""
    openai_api_key: str = ""


settings = Settings()
