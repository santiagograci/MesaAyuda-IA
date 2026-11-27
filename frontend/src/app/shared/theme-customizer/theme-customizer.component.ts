import { Component, inject, signal } from '@angular/core';
import { ThemeService } from '../../core/services/theme.service';

/**
 * Control reutilizable en cabecera para alternar Claro/Oscuro, elegir máscaras predefinidas
 * o diseñar una máscara personalizada con vista previa inmediata.
 */
@Component({
  selector: 'app-theme-customizer',
  standalone: true,
  template: `
    <div class="customizer-wrapper">
      <button
        type="button"
        class="btn btn-sm"
        (click)="themeService.toggleMode()"
        [attr.aria-label]="themeService.mode() === 'light' ? 'Cambiar a modo oscuro' : 'Cambiar a modo claro'"
      >
        {{ themeService.mode() === 'light' ? '🌙 Oscuro' : '☀️ Claro' }}
      </button>

      <button
        type="button"
        class="btn btn-sm btn-soft"
        (click)="togglePopover()"
        [attr.aria-expanded]="isOpen()"
      >
        🎨 Máscara
      </button>

      @if (isOpen()) {
        <div class="popover card-surface" role="dialog" aria-label="Personalización de tema">
          <div class="popover-header">
            <strong>Máscaras de Diseño</strong>
            <button type="button" class="btn btn-sm" (click)="themeService.resetTheme()">
              Restablecer
            </button>
          </div>

          <div class="preset-list">
            @for (mask of themeService.presetMasks; track mask.id) {
              <button
                type="button"
                class="preset-chip"
                [class.active]="themeService.activeMaskId() === mask.id"
                (click)="themeService.selectPresetMask(mask.id)"
              >
                <span class="swatch" [style.background]="mask.accentHex"></span>
                <span>{{ mask.nombre }}</span>
              </button>
            }
          </div>

          <div class="custom-box">
            <div class="custom-title">Máscara Personalizada</div>
            <label class="field-row">
              <span>Color primario</span>
              <input
                type="color"
                [value]="themeService.customAccent()"
                (input)="onColorChange($event)"
              />
            </label>
            <label class="field-row">
              <span>Bordes ({{ themeService.customRadius() }} px)</span>
              <input
                type="range"
                min="4"
                max="20"
                [value]="themeService.customRadius()"
                (input)="onRadiusChange($event)"
              />
            </label>
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .customizer-wrapper {
        position: relative;
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
      }
      .popover {
        position: absolute;
        top: calc(100% + 0.5rem);
        right: 0;
        width: 280px;
        padding: 1rem;
        z-index: 50;
      }
      .popover-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.75rem;
      }
      .preset-list {
        display: grid;
        gap: 0.45rem;
        margin-bottom: 0.9rem;
      }
      .preset-chip {
        display: flex;
        align-items: center;
        gap: 0.55rem;
        padding: 0.45rem 0.65rem;
        border-radius: calc(var(--radius-card) * 0.7);
        border: 1px solid var(--border-color);
        background: var(--bg-surface-alt);
        cursor: pointer;
        font-size: 0.85rem;
        text-align: left;
      }
      .preset-chip.active {
        border-color: var(--accent-primary);
        background: var(--accent-soft);
        font-weight: 600;
      }
      .swatch {
        width: 14px;
        height: 14px;
        border-radius: 50%;
        flex-shrink: 0;
      }
      .custom-box {
        border-top: 1px solid var(--border-color);
        padding-top: 0.75rem;
        display: grid;
        gap: 0.6rem;
      }
      .custom-title {
        font-size: 0.8rem;
        font-weight: 600;
        color: var(--text-secondary);
      }
      .field-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.82rem;
      }
    `
  ]
})
export class ThemeCustomizerComponent {
  readonly themeService = inject(ThemeService);
  readonly isOpen = signal<boolean>(false);

  togglePopover(): void {
    this.isOpen.update((v) => !v);
  }

  onColorChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.themeService.applyCustomMask(input.value, this.themeService.customRadius());
  }

  onRadiusChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.themeService.applyCustomMask(this.themeService.customAccent(), Number(input.value));
  }
}
