import { Component, Input } from '@angular/core';

/**
 * Componente vectorial oficial de marca Ofima S.A.S. con isotipo de 5 bloques
 * y tipografía adaptativa para modo claro/oscuro en variantes 'full' y 'compact'.
 */
@Component({
  selector: 'app-ofima-logo',
  standalone: true,
  template: `
    <div class="ofima-brand" [class.full]="variant === 'full'" [class.compact]="variant === 'compact'">
      <svg
        class="ofima-icon"
        [attr.width]="variant === 'full' ? 46 : 34"
        [attr.height]="variant === 'full' ? 46 : 34"
        viewBox="0 0 64 64"
        fill="none"
        aria-hidden="true"
      >
        <g transform="translate(5, 0) skewX(-8)">
          <rect x="10" y="6" width="18" height="14" rx="5" fill="#0095FF" />
          <rect x="6" y="24" width="24" height="16" rx="5" fill="#0095FF" />
          <rect x="10" y="44" width="18" height="14" rx="5" fill="#C5C9CE" />
          <rect x="35" y="14" width="19" height="15" rx="5" fill="#C5C9CE" />
          <rect x="35" y="34" width="19" height="15" rx="5" fill="#0095FF" />
        </g>
      </svg>

      @if (variant === 'full') {
        <div class="brand-copy">
          <div class="wordmark-row">
            <span class="wordmark wordmark-lg">Ofima</span>
            <span class="badge badge-ia">MesaAyuda IA</span>
          </div>
          <span class="tagline">Líderes en Transformación Digital</span>
        </div>
      } @else {
        <div class="brand-copy-compact">
          <span class="wordmark wordmark-sm">Ofima</span>
          <span class="divider" aria-hidden="true"></span>
          <span class="compact-title">MesaAyuda IA — Soporte Empresarial con RAG</span>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .ofima-brand {
        display: inline-flex;
        align-items: center;
        gap: 0.75rem;
      }
      .ofima-icon {
        flex-shrink: 0;
        display: block;
      }
      .brand-copy {
        display: grid;
        gap: 0.15rem;
      }
      .wordmark-row {
        display: flex;
        align-items: center;
        gap: 0.55rem;
        flex-wrap: wrap;
      }
      .wordmark {
        font-family: 'Poppins', 'Montserrat', 'Inter', system-ui, sans-serif;
        font-weight: 800;
        letter-spacing: -0.03em;
        line-height: 1;
        color: var(--logo-wordmark, #0b2239);
      }
      .wordmark-lg {
        font-size: 1.75rem;
      }
      .wordmark-sm {
        font-size: 1.25rem;
      }
      .tagline {
        font-size: 0.78rem;
        font-weight: 600;
        color: var(--text-secondary);
        letter-spacing: 0.01em;
      }
      .brand-copy-compact {
        display: flex;
        align-items: center;
        gap: 0.65rem;
        flex-wrap: wrap;
      }
      .divider {
        width: 1px;
        height: 1.15rem;
        background: var(--border-color);
      }
      .compact-title {
        font-size: 0.88rem;
        font-weight: 600;
        color: var(--text-secondary);
      }
    `
  ]
})
export class OfimaLogoComponent {
  @Input() variant: 'full' | 'compact' = 'compact';
}
