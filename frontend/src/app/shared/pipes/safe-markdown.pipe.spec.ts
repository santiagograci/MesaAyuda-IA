import { TestBed } from '@angular/core/testing';
import { SafeMarkdownPipe } from './safe-markdown.pipe';

describe('SafeMarkdownPipe', () => {
  let pipe: SafeMarkdownPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SafeMarkdownPipe] });
    pipe = TestBed.inject(SafeMarkdownPipe);
  });

  function renderHtml(input: string): string {
    const safe = pipe.transform(input);
    const el = document.createElement('div');
    el.innerHTML = (safe as { changingThisBreaksApplicationSecurity?: string })
      .changingThisBreaksApplicationSecurity ?? String(safe);
    return el.innerHTML;
  }

  it('debe renderizar negritas, código en línea y listas sin asteriscos crudos', () => {
    const raw = [
      'Pasos para corregir **FAC-402**:',
      '1. Ir a `Facturación > Notas`',
      '2. Seleccionar **Nota Crédito NC-01**',
      '',
      '- Validar CUFE original',
      '- Confirmar envío DIAN'
    ].join('\n');

    const html = renderHtml(raw);
    expect(html).toContain('<strong>FAC-402</strong>');
    expect(html).toContain('<code class="md-code">Facturación &gt; Notas</code>');
    expect(html).toContain('<ol class="md-list">');
    expect(html).toContain('<ul class="md-list">');
    expect(html).not.toContain('**');
  });

  it('debe escapar etiquetas HTML peligrosas antes de formatear Markdown (XSS safe)', () => {
    const raw = 'Hola <script>alert(1)</script> y <img src=x onerror=alert(2)> **seguro**';
    const html = renderHtml(raw);
    expect(html).not.toContain('<script>');
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;script&gt;alert(1)&lt;/script&gt;');
    expect(html).toContain('<strong>seguro</strong>');
  });
});
