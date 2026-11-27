import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';

/**
 * Componente raíz que inicializa el motor de temas y renderiza las rutas activas.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent {
  // Se inyecta al arranque para aplicar el tema persistido en localStorage desde el inicio.
  private readonly _themeService = inject(ThemeService);
}
