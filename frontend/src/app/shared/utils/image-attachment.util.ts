/** Tamaño máximo permitido por captura de pantalla adjunta (2 MB). */
export const MAX_IMAGE_ATTACHMENT_BYTES = 2 * 1024 * 1024;

/** Formatos MIME admitidos para capturas de pantalla de soporte. */
export const ALLOWED_IMAGE_MIME_TYPES: readonly string[] = [
  'image/png',
  'image/jpeg',
  'image/webp'
];

/**
 * Valida tipo MIME, contenido no vacío y tamaño máximo (2 MB) de una imagen seleccionada.
 * Retorna un mensaje de error legible o `null` si es válida.
 */
export function validateImageAttachment(file: File): string | null {
  if (!ALLOWED_IMAGE_MIME_TYPES.includes(file.type.toLowerCase())) {
    return 'Formato no permitido. Adjunta únicamente imágenes PNG, JPG o WebP.';
  }

  if (file.size <= 0) {
    return 'La imagen seleccionada está vacía.';
  }

  if (file.size > MAX_IMAGE_ATTACHMENT_BYTES) {
    return 'La imagen supera el tamaño máximo permitido de 2 MB.';
  }

  return null;
}

/**
 * Convierte un archivo de imagen en una cadena Data URL Base64 lista para envío JSON.
 */
export function readImageAsDataUrl(file: File): Promise<string> {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === 'string') {
        resolve(reader.result);
      } else {
        reject(new Error('No fue posible leer la imagen seleccionada.'));
      }
    };
    reader.onerror = () => {
      reject(new Error('Error al procesar la imagen seleccionada.'));
    };
    reader.readAsDataURL(file);
  });
}
