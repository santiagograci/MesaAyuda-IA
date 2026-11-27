# Evaluación Empírica del Motor RAG — MesaAyuda IA

Fecha de ejecución del benchmark: `2026-11-27 02:32:07 UTC` (`rag-service/evaluation/evaluate.py`).

## Configuración Evaluada

| Parámetro | Valor |
| :--- | :--- |
| Modelo de Embeddings | `all-MiniLM-L6-v2` |
| Estrategia de Recuperación | Híbrida (`0.70 Vector Coseno + 0.30 BM25Okapi`) |
| Tamaño de Fragmento / Solapamiento | `650` / `120` caracteres |
| Umbral de Similitud (`similarity_threshold`) | `0.35` |
| Proveedor LLM Evaluado | `grounded-mock` |
| Documentos / Fragmentos Indexados | `6` manuales / `86` fragmentos |

## Métricas Globales Obtenidas

| Métrica | Resultado |
| :--- | :---: |
| **Retrieval Hit Rate @ 4** | **100.00%** |
| **Answer Groundedness / Keyword Faithfulness** | **100.00%** |
| **Out-of-Domain Rejection Accuracy** | **100.00%** |
| Latencia Promedio por Consulta | `13.75 ms` |

## Resultados Detallados por Pregunta (Dataset de 10 Casos)

| ID | Tipo | Documento Esperado | Documento Recuperado (Top-1) | Puntaje | Estado | Veredicto |
| :--- | :--- | :--- | :--- | :---: | :--- | :---: |
| P1 | `in_domain` | `01-facturacion-electronica-y-anulaciones.md` | `01-facturacion-electronica-y-anulaciones.md` | 0.8424 | Anclado (3/3 kw) | **PASS** |
| P2 | `in_domain` | `01-facturacion-electronica-y-anulaciones.md` | `01-facturacion-electronica-y-anulaciones.md` | 0.7183 | Anclado (2/2 kw) | **PASS** |
| P3 | `in_domain` | `02-cierre-contable-y-conciliacion.md` | `02-cierre-contable-y-conciliacion.md` | 0.7806 | Anclado (3/3 kw) | **PASS** |
| P4 | `in_domain` | `02-cierre-contable-y-conciliacion.md` | `02-cierre-contable-y-conciliacion.md` | 0.7702 | Anclado (2/2 kw) | **PASS** |
| P5 | `in_domain` | `03-inventarios-kardex-y-bodegas.md` | `03-inventarios-kardex-y-bodegas.md` | 0.7884 | Anclado (2/2 kw) | **PASS** |
| P6 | `in_domain` | `04-nomina-electronica-y-novedades.md` | `04-nomina-electronica-y-novedades.md` | 0.6499 | Anclado (2/2 kw) | **PASS** |
| P7 | `in_domain` | `05-cuentas-por-pagar-y-tesoreria.md` | `05-cuentas-por-pagar-y-tesoreria.md` | 0.7793 | Anclado (2/2 kw) | **PASS** |
| P8 | `in_domain` | `06-seguridad-roles-y-auditoria-erp.md` | `06-seguridad-roles-y-auditoria-erp.md` | 0.8224 | Anclado (3/3 kw) | **PASS** |
| P9 | `out_of_domain` | `N/A (Fuera de dominio)` | `Ninguno` | 0.0000 | Rechazado (`anclado=False`) | **PASS** |
| P10 | `out_of_domain` | `N/A (Fuera de dominio)` | `Ninguno` | 0.0000 | Rechazado (`anclado=False`) | **PASS** |

## Verificación Complementaria de Seguridad e Ingesta (`pytest -v`)

La suite automatizada en `rag-service/tests/test_rag_service.py` (**7 passed**) valida adicionalmente el bloqueo HTTP `400` ante intentos de *prompt injection* (`test_prompt_injection_mitigation`), la preservación de metadatos en los 6 manuales (`test_chunking_preserves_metadata`), la caché LRU de consultas y la ingesta dinámica vía `GET /documentos` + `POST /ingesta/archivo` (`test_list_documents_and_upload_txt_manual`).
