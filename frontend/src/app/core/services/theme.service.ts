import { Injectable, effect, signal } from '@angular/core';
import { ThemeMask, ThemeMode } from '../models/api.models';

interface PersistedTheme {
  mode: ThemeMode;
  activeMaskId: string;
  customAccent: string;
  customRadius: number;
}

const STORAGE_KEY = 'mesaayuda_theme_v1';

/**
 * Servicio reactivo basado en Angular Signals para gestionar el tema Claro/Oscuro,
 * las máscaras predefinidas y la máscara personalizada del usuario.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly presetMasks: readonly ThemeMask[] = [
    { id: 'ofima-blue', nombre: 'Ofima Azul', accentHex: '#0052CC', radiusPx: 10 },
    { id: 'emerald-erp', nombre: 'Esmeralda ERP', accentHex: '#059669', radiusPx: 10 },
    { id: 'indigo-ai', nombre: 'Índigo IA', accentHex: '#4F46E5', radiusPx: 12 }
  ];

  readonly mode = signal<ThemeMode>('light');
  readonly activeMaskId = signal<string>('ofima-blue');
  readonly customAccent = signal<string>('#0052CC');
  readonly customRadius = signal<number>(10);

  constructor() {
    this.restoreFromStorage();

    effect(() => {
      const currentMode = this.mode();
      const maskId = this.activeMaskId();
      const accent = this.customAccent();
      const radius = this.customRadius();

      if (typeof document !== 'undefined') {
        const root = document.documentElement;
        root.setAttribute('data-mode', currentMode);
        root.setAttribute('data-mask', maskId);
        root.style.setProperty('--radius-card', `${radius}px`);

        if (maskId === 'custom') {
          root.style.setProperty('--accent-primary', accent);
          root.style.setProperty('--accent-hover', this.darkenHex(accent, 14));
          root.style.setProperty('--accent-soft', this.hexToRgba(accent, 0.14));
        } else {
          root.style.removeProperty('--accent-primary');
          root.style.removeProperty('--accent-hover');
          root.style.removeProperty('--accent-soft');
        }
      }

      this.saveToStorage({
        mode: currentMode,
        activeMaskId: maskId,
        customAccent: accent,
        customRadius: radius
      });
    });
  }

  /** Alterna entre modo claro y modo oscuro. */
  toggleMode(): void {
    this.mode.update((current) => (current === 'light' ? 'dark' : 'light'));
  }

  /** Selecciona una de las máscaras predefinidas por su identificador. */
  selectPresetMask(id: string): void {
    const preset = this.presetMasks.find((m) => m.id === id);
    if (!preset) {
      return;
    }
    this.activeMaskId.set(preset.id);
    this.customAccent.set(preset.accentHex);
    this.customRadius.set(preset.radiusPx);
  }

  /** Aplica una máscara personalizada con color primario y radio de bordes en px. */
  applyCustomMask(accentHex: string, radiusPx: number): void {
    const clampedRadius = Math.max(2, Math.min(24, radiusPx));
    this.activeMaskId.set('custom');
    this.customAccent.set(accentHex);
    this.customRadius.set(clampedRadius);
  }

  /** Restablece la apariencia corporativa por defecto (Claro + Ofima Azul). */
  resetTheme(): void {
    this.mode.set('light');
    this.selectPresetMask('ofima-blue');
  }

  private restoreFromStorage(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return;
      }
      const parsed = JSON.parse(raw) as Partial<PersistedTheme>;
      if (parsed.mode === 'light' || parsed.mode === 'dark') {
        this.mode.set(parsed.mode);
      }
      if (typeof parsed.activeMaskId === 'string' && parsed.activeMaskId.length > 0) {
        this.activeMaskId.set(parsed.activeMaskId);
      }
      if (typeof parsed.customAccent === 'string' && /^#[0-9a-fA-F]{6}$/.test(parsed.customAccent)) {
        this.customAccent.set(parsed.customAccent);
      }
      if (typeof parsed.customRadius === 'number' && Number.isFinite(parsed.customRadius)) {
        this.customRadius.set(Math.max(2, Math.min(24, parsed.customRadius)));
      }
    } catch {
      // Ignora estado corrupto en almacenamiento local.
    }
  }

  private saveToStorage(state: PersistedTheme): void {
    if (typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch {
      // Ignora restricciones de almacenamiento privado.
    }
  }

  private darkenHex(hex: string, percent: number): string {
    const clean = hex.replace('#', '');
    if (clean.length !== 6) {
      return hex;
    }
    const factor = 1 - percent / 100;
    const r = Math.max(0, Math.round(parseInt(clean.slice(0, 2), 16) * factor));
    const g = Math.max(0, Math.round(parseInt(clean.slice(2, 4), 16) * factor));
    const b = Math.max(0, Math.round(parseInt(clean.slice(4, 6), 16) * factor));
    return `#${[r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')}`;
  }

  private hexToRgba(hex: string, alpha: number): string {
    const clean = hex.replace('#', '');
    if (clean.length !== 6) {
      return `rgba(0, 82, 204, ${alpha})`;
    }
    const r = parseInt(clean.slice(0, 2), 16);
    const g = parseInt(clean.slice(2, 4), 16);
    const b = parseInt(clean.slice(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }
}
