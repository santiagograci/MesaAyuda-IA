import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { from, of } from 'rxjs';
import { CasoDetalle, RagStreamEvent } from '../../../core/models/api.models';
import { AuthService } from '../../../core/services/auth.service';
import { CasosService } from '../../../core/services/casos.service';
import { CasoDetailComponent } from './caso-detail.component';

describe('CasoDetailComponent (Borrado con cuenta regresiva y escalamiento de prioridad)', () => {
  let fixture: ComponentFixture<CasoDetailComponent>;
  let component: CasoDetailComponent;
  let casosServiceSpy: jasmine.SpyObj<CasosService>;
  let isAnalistaSignal: ReturnType<typeof signal<boolean>>;

  const mockCaso: CasoDetalle = {
    id: 'caso-delete-1',
    titulo: 'Error FAC-402 al anular factura electrónica',
    descripcion: 'Descripción de prueba para validar confirmación con cuenta regresiva de 3 segundos.',
    prioridad: 'Alta',
    estado: 'Abierto',
    fechaCreacion: '2026-11-26T15:00:00Z',
    clienteId: 'cli-1',
    clienteNombre: 'Carlos Mendoza',
    respuestas: [],
    fechaLimiteSla: '2026-11-26T23:00:00Z',
    estadoSla: 'EnTiempo',
    calificacionCsat: null,
    comentarioCsat: null
  };

  beforeEach(async () => {
    isAnalistaSignal = signal(true);
    casosServiceSpy = jasmine.createSpyObj<CasosService>('CasosService', [
      'eliminarCaso',
      'cambiarPrioridad',
      'sugerirRespuestaStream',
      'registrarCsat',
      'reabrirCaso',
      'agregarRespuesta'
    ]);
    casosServiceSpy.eliminarCaso.and.returnValue(of(void 0));

    const authMock = {
      isAnalista: isAnalistaSignal,
      isAuthenticated: signal(true),
      currentUser: signal(null)
    };

    await TestBed.configureTestingModule({
      imports: [CasoDetailComponent],
      providers: [
        { provide: CasosService, useValue: casosServiceSpy },
        { provide: AuthService, useValue: authMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CasoDetailComponent);
    component = fixture.componentInstance;
    component.caso = mockCaso;
    fixture.detectChanges();
  });

  it('debe exigir cuenta regresiva de 3 segundos antes de habilitar la eliminación lógica', fakeAsync(() => {
    const elimSpy = spyOn(component.casoEliminado, 'emit');
    const el: HTMLElement = fixture.nativeElement;

    const deleteBtn = Array.from(el.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Eliminar'
    ) as HTMLButtonElement;
    expect(deleteBtn).toBeDefined();

    deleteBtn.click();
    fixture.detectChanges();

    expect(el.textContent).toContain('Pasará a la papelera de reciclaje');

    const getConfirmBtn = () =>
      el.querySelector('[data-testid="confirm-delete-btn"]') as HTMLButtonElement;

    expect(getConfirmBtn().disabled).toBeTrue();
    expect(getConfirmBtn().textContent).toContain('3s');

    tick(1000);
    fixture.detectChanges();
    expect(getConfirmBtn().disabled).toBeTrue();
    expect(getConfirmBtn().textContent).toContain('2s');

    tick(1000);
    fixture.detectChanges();
    expect(getConfirmBtn().disabled).toBeTrue();
    expect(getConfirmBtn().textContent).toContain('1s');

    tick(1000);
    fixture.detectChanges();
    expect(getConfirmBtn().disabled).toBeFalse();
    expect(getConfirmBtn().textContent).toContain('Sí, eliminar caso');

    getConfirmBtn().click();
    fixture.detectChanges();

    expect(casosServiceSpy.eliminarCaso).toHaveBeenCalledWith('caso-delete-1');
    expect(elimSpy).toHaveBeenCalledWith('caso-delete-1');
  }));

  it('debe permitir al Cliente elevar severidad solo a niveles superiores y emitir casoActualizado', () => {
    isAnalistaSignal.set(false);
    component.caso = { ...mockCaso, prioridad: 'Media' };
    fixture.detectChanges();

    const updatedDto: CasoDetalle = {
      ...mockCaso,
      prioridad: 'Critica',
      respuestas: [
        {
          id: 'resp-audit-1',
          texto: '🔺 [Cambio de Prioridad: Media ➔ Critica] Justificación: Bloqueo completo de facturación.',
          esGeneradaPorIA: false,
          aprobadaPorAnalista: true,
          autorNombre: 'Carlos Mendoza',
          fuentesCitadas: [],
          fechaCreacion: '2026-11-26T16:00:00Z'
        }
      ]
    };
    casosServiceSpy.cambiarPrioridad.and.returnValue(of(updatedDto));
    const updatedSpy = spyOn(component.casoActualizado, 'emit');

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).not.toContain('Pedir sugerencia a la IA');
    const toggleBtn = el.querySelector('[data-testid="toggle-priority-btn"]') as HTMLButtonElement;
    expect(toggleBtn.textContent).toContain('Elevar severidad');

    toggleBtn.click();
    fixture.detectChanges();

    const options = Array.from(el.querySelectorAll('#nuevaPrioridad option')).map(
      (o) => (o as HTMLOptionElement).value
    );
    expect(options).toEqual(['Alta', 'Critica']);

    component.priorityForm.setValue({
      nuevaPrioridad: 'Critica',
      justificacion: 'Bloqueo completo de facturación.'
    });
    const confirmBtn = el.querySelector('[data-testid="confirm-priority-btn"]') as HTMLButtonElement;
    confirmBtn.click();
    fixture.detectChanges();

    expect(casosServiceSpy.cambiarPrioridad).toHaveBeenCalledWith(
      'caso-delete-1',
      'Critica',
      'Bloqueo completo de facturación.'
    );
    expect(updatedSpy).toHaveBeenCalledWith(updatedDto);
    expect(el.textContent).toContain('[Cambio de Prioridad: Media ➔ Critica]');

    // En Critica el Cliente ya no ve el botón de elevar severidad
    expect(el.querySelector('[data-testid="toggle-priority-btn"]')).toBeNull();
  });

  it('debe permitir al Analista ajustar la prioridad en cualquier dirección distinta a la actual', () => {
    isAnalistaSignal.set(true);
    component.caso = { ...mockCaso, prioridad: 'Alta' };
    fixture.detectChanges();

    const adjustedDto: CasoDetalle = {
      ...mockCaso,
      prioridad: 'Baja',
      respuestas: [
        {
          id: 'resp-audit-2',
          texto: '🔺 [Cambio de Prioridad: Alta ➔ Baja] Justificación: Existe alternativa temporal.',
          esGeneradaPorIA: false,
          aprobadaPorAnalista: true,
          autorNombre: 'Santiago Graciano',
          fuentesCitadas: [],
          fechaCreacion: '2026-11-26T16:05:00Z'
        }
      ]
    };
    casosServiceSpy.cambiarPrioridad.and.returnValue(of(adjustedDto));

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Pedir sugerencia a la IA');
    const toggleBtn = el.querySelector('[data-testid="toggle-priority-btn"]') as HTMLButtonElement;
    expect(toggleBtn.textContent).toContain('Ajustar prioridad');

    toggleBtn.click();
    fixture.detectChanges();

    const options = Array.from(el.querySelectorAll('#nuevaPrioridad option')).map(
      (o) => (o as HTMLOptionElement).value
    );
    expect(options).toEqual(['Baja', 'Media', 'Critica']);

    component.priorityForm.setValue({
      nuevaPrioridad: 'Baja',
      justificacion: 'Existe alternativa temporal.'
    });
    const confirmBtn = el.querySelector('[data-testid="confirm-priority-btn"]') as HTMLButtonElement;
    confirmBtn.click();
    fixture.detectChanges();

    expect(casosServiceSpy.cambiarPrioridad).toHaveBeenCalledWith(
      'caso-delete-1',
      'Baja',
      'Existe alternativa temporal.'
    );
    expect(el.textContent).toContain('[Cambio de Prioridad: Alta ➔ Baja]');
  });

  it('debe consumir streaming SSE de sugerencia IA y actualizar el borrador al evento done', () => {
    isAnalistaSignal.set(true);
    component.caso = mockCaso;
    fixture.detectChanges();

    const streamedCaso: CasoDetalle = {
      ...mockCaso,
      respuestas: [
        {
          id: 'ia-stream-1',
          texto: 'Emita Nota Crédito NC-01 en Facturación > Notas.',
          esGeneradaPorIA: true,
          aprobadaPorAnalista: false,
          autorNombre: 'MesaAyuda IA',
          fuentesCitadas: [
            {
              documento: '01-facturacion-electronica-y-anulaciones.md',
              seccion: 'Reglas de Anulación',
              fragmento: 'Use NC-01 para revertir factura aceptada.',
              puntaje: 0.91
            }
          ],
          fechaCreacion: '2026-11-26T16:10:00Z'
        }
      ]
    };

    const streamEvents: RagStreamEvent[] = [
      {
        type: 'sources',
        fuentes: streamedCaso.respuestas[0].fuentesCitadas,
        tokensUsados: 245
      },
      { type: 'token', chunk: 'Emita Nota Crédito ' },
      { type: 'token', chunk: 'NC-01 en Facturación > Notas.' },
      { type: 'done', caso: streamedCaso }
    ];

    casosServiceSpy.sugerirRespuestaStream.and.returnValue(from(streamEvents));

    const updatedSpy = spyOn(component.casoActualizado, 'emit');
    const el: HTMLElement = fixture.nativeElement;
    const iaBtn = Array.from(el.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Pedir sugerencia a la IA')
    ) as HTMLButtonElement;
    iaBtn.click();
    fixture.detectChanges();

    expect(casosServiceSpy.sugerirRespuestaStream).toHaveBeenCalledWith('caso-delete-1');
    expect(updatedSpy).toHaveBeenCalledWith(streamedCaso);
    expect(el.textContent).toContain('Borradores de IA pendientes de aprobación (1)');
    expect(el.textContent).toContain('01-facturacion-electronica-y-anulaciones.md');
  });

  it('debe mostrar pill SLA y permitir al Cliente calificar con 1-5 estrellas un caso Resuelto', () => {
    isAnalistaSignal.set(false);
    component.caso = {
      ...mockCaso,
      estado: 'Resuelto',
      estadoSla: 'Cumplido',
      calificacionCsat: null,
      comentarioCsat: null
    };
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('✅ SLA Cumplido');
    expect(el.textContent).toContain('¿Cómo calificarías la solución recibida?');

    const ratedCaso: CasoDetalle = {
      ...mockCaso,
      estado: 'Resuelto',
      estadoSla: 'Cumplido',
      calificacionCsat: 5,
      comentarioCsat: 'Excelente guía del soporte.'
    };
    casosServiceSpy.registrarCsat.and.returnValue(of(ratedCaso));
    const updatedSpy = spyOn(component.casoActualizado, 'emit');

    const star5Btn = el.querySelector('[data-testid="csat-star-5"]') as HTMLButtonElement;
    star5Btn.click();
    component.csatComentario.set('Excelente guía del soporte.');
    fixture.detectChanges();

    const submitCsatBtn = el.querySelector('[data-testid="submit-csat-btn"]') as HTMLButtonElement;
    submitCsatBtn.click();
    fixture.detectChanges();

    expect(casosServiceSpy.registrarCsat).toHaveBeenCalledWith(
      'caso-delete-1',
      5,
      'Excelente guía del soporte.'
    );
    expect(updatedSpy).toHaveBeenCalledWith(ratedCaso);
    expect(el.textContent).toContain('⭐ 5 / 5');
    expect(el.textContent).toContain('Excelente guía del soporte.');
  });

  it('debe renderizar Markdown limpio, permitir reabrir un caso Cerrado y abrir el lightbox de evidencia', () => {
    const pngValido =
      'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';
    isAnalistaSignal.set(false);
    component.caso = {
      ...mockCaso,
      estado: 'Cerrado',
      estadoSla: 'Cumplido',
      imagenAdjuntaBase64: pngValido,
      respuestas: [
        {
          id: 'resp-md-1',
          texto: 'Ejecuta **Nota Crédito NC-01** en `Facturación > Notas`.',
          esGeneradaPorIA: true,
          aprobadaPorAnalista: true,
          autorNombre: 'Santiago Graciano',
          fuentesCitadas: [],
          fechaCreacion: '2026-11-26T16:15:00Z',
          imagenAdjuntaBase64: pngValido
        }
      ]
    };
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.resp-body strong')?.textContent).toBe('Nota Crédito NC-01');
    expect(el.querySelector('.resp-body code')?.textContent).toBe('Facturación > Notas');
    expect(el.querySelector('.resp-body')?.textContent).not.toContain('**');

    // Abrir y cerrar lightbox de la captura del caso
    const caseThumb = el.querySelector('[data-testid="case-attachment-thumb"]') as HTMLButtonElement;
    expect(caseThumb).toBeTruthy();
    caseThumb.click();
    fixture.detectChanges();
    expect(el.querySelector('.lightbox-img')).toBeTruthy();

    const closeLightboxBtn = el.querySelector('[data-testid="close-lightbox-btn"]') as HTMLButtonElement;
    closeLightboxBtn.click();
    fixture.detectChanges();
    expect(el.querySelector('.lightbox-img')).toBeNull();

    // Reabrir caso Cerrado
    const reopenedCaso: CasoDetalle = {
      ...mockCaso,
      estado: 'EnProceso',
      estadoSla: 'EnTiempo'
    };
    casosServiceSpy.reabrirCaso.and.returnValue(of(reopenedCaso));
    const updatedSpy = spyOn(component.casoActualizado, 'emit');

    const toggleReopenBtn = el.querySelector('[data-testid="toggle-reopen-btn"]') as HTMLButtonElement;
    toggleReopenBtn.click();
    fixture.detectChanges();

    component.reopenForm.setValue({
      motivo: 'El inconveniente volvió a presentarse en la emisión electrónica.'
    });
    const confirmReopenBtn = el.querySelector('[data-testid="confirm-reopen-btn"]') as HTMLButtonElement;
    confirmReopenBtn.click();
    fixture.detectChanges();

    expect(casosServiceSpy.reabrirCaso).toHaveBeenCalledWith(
      'caso-delete-1',
      'El inconveniente volvió a presentarse en la emisión electrónica.'
    );
    expect(updatedSpy).toHaveBeenCalledWith(reopenedCaso);
  });
});
