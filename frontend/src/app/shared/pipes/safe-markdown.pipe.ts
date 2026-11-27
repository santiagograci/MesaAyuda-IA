import { Pipe, PipeTransform, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

/**
 * Renderiza Markdown básico (**negrita**, `código`, listas numeradas/viñetas y saltos de línea)
 * escapando HTML crudo previamente para prevenir XSS.
 */
@Pipe({
  name: 'safeMarkdown',
  standalone: true
})
export class SafeMarkdownPipe implements PipeTransform {
  private readonly sanitizer = inject(DomSanitizer);

  transform(value: string | null | undefined): SafeHtml {
    if (!value) {
      return '';
    }

    const escaped = this.escapeHtml(value);
    const lines = escaped.replace(/\r\n/g, '\n').split('\n');
    const blocks: string[] = [];

    let listType: 'ul' | 'ol' | null = null;
    let listItems: string[] = [];
    let paragraphLines: string[] = [];

    const flushParagraph = (): void => {
      if (paragraphLines.length > 0) {
        blocks.push(`<p class="md-p">${paragraphLines.join('<br>')}</p>`);
        paragraphLines = [];
      }
    };

    const flushList = (): void => {
      if (listType && listItems.length > 0) {
        const itemsHtml = listItems.map((item) => `<li>${item}</li>`).join('');
        blocks.push(`<${listType} class="md-list">${itemsHtml}</${listType}>`);
      }
      listType = null;
      listItems = [];
    };

    for (const rawLine of lines) {
      const trimmed = rawLine.trim();

      if (!trimmed) {
        flushParagraph();
        flushList();
        continue;
      }

      const ulMatch = rawLine.match(/^\s*(?:[-*•])\s+(.+)$/);
      if (ulMatch) {
        flushParagraph();
        if (listType && listType !== 'ul') {
          flushList();
        }
        listType = 'ul';
        listItems.push(this.formatInline(ulMatch[1]));
        continue;
      }

      const olMatch = rawLine.match(/^\s*\d+\.\s+(.+)$/);
      if (olMatch) {
        flushParagraph();
        if (listType && listType !== 'ol') {
          flushList();
        }
        listType = 'ol';
        listItems.push(this.formatInline(olMatch[1]));
        continue;
      }

      flushList();
      paragraphLines.push(this.formatInline(rawLine));
    }

    flushParagraph();
    flushList();

    return this.sanitizer.bypassSecurityTrustHtml(blocks.join(''));
  }

  private escapeHtml(text: string): string {
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  private formatInline(escapedLine: string): string {
    const codeTokens: string[] = [];
    const withCodePlaceholders = escapedLine.replace(/`([^`]+)`/g, (_match, code: string) => {
      const idx = codeTokens.length;
      codeTokens.push(`<code class="md-code">${code}</code>`);
      return `\u0000CODE_${idx}\u0000`;
    });

    const withBold = withCodePlaceholders.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');

    return withBold.replace(/\u0000CODE_(\d+)\u0000/g, (_match, indexStr: string) => {
      const idx = Number(indexStr);
      return codeTokens[idx] ?? '';
    });
  }
}
