import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CasoDetalle } from '../../../core/models/api.models';
import { AuthService } from '../../../core/services/auth.service';
import { CasoDetailComponent } from './caso-detail.component';

describe('CasoDetailComponent', () => {
  let fixture: ComponentFixture<CasoDetailComponent>;
  let component: CasoDetailComponent;
  let httpMock: HttpTestingController;
  const isAnalistaSignal = signal<boolean>(true);

  const mockAuthService = {
    isAnalista: isAnalistaSignal.asReadonly(),
    isCliente: () => !isAnalistaSignal()
  };

  const baseCaso: CasoDetalle = {
    id: 'caso-qa-1',
    titulo: 'Error FAC-402 al anular factura electrónica',
    descripcion: 'El sistema impide anular directamente una factura aceptada por DIAN.',
    prioridad: 'Alta',
    estado: 'Abierto',
    fechaCreacion: '2026-11-26T20:00:00Z',
    clienteId: 'cli-1',
    clienteNombre: 'Carlos Mendoza - Contabilidad',
    respuestas: []
  };

  beforeEach(async () => {
    isAnalistaSignal.set(true);

    await TestBed.configureTestingModule({
      imports: [CasoDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: mockAuthService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CasoDetailComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('debe actualizar reactivamente borradores de IA pendientes y permitir aprobación al Analista', () => {
    component.caso = baseCaso;
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Historial de Atención (0)');
    expect(fixture.nativeElement.textContent).not.toContain('Borradores de IA pendientes de aprobación');

    const casoConBorrador: CasoDetalle = {
      ...baseCaso,
      respuestas: [
        {
          id: 'resp-ia-1',
          texto: 'Emita una Nota Crédito NC-01 referenciando el CUFE original.',
          esGeneradaPorIA: true,
          aprobadaPorAnalista: false,
          autorNombre: 'MesaAyuda IA',
          fuentesCitadas: [
            {
              documento: '01-facturacion-electronica-y-anulaciones.md',
              seccion: 'Reglas de Anulación Directa vs. Nota Crédito',
              fragmento: 'Error FAC-402 requiere Nota Crédito NC-01.',
              puntaje: 0.84
            }
          ],
          fechaCreacion: '2026-11-26T20:05:00Z'
        }
      ]
    };

    component.caso = casoConBorrador;
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Borradores de IA pendientes de aprobación (1)');
    expect(text).toContain('01-facturacion-electronica-y-anulaciones.md');
    expect(text).toContain('Aprobar y Publicar al Cliente');
    expect(text).toContain('Historial de Atención (0)');

    const textarea = fixture.nativeElement.querySelector('textarea[id="edit-resp-ia-1"]') as HTMLTextAreaElement;
    expect(textarea).not.toBeNull();
    expect(textarea.value).toContain('Nota Crédito NC-01');
  });

  it('debe ocultar borradores IA sin aprobar y controles de Analista cuando el usuario es Cliente', () => {
    isAnalistaSignal.set(false);
    component.caso = {
      ...baseCaso,
      estado: 'EnProceso',
      respuestas: [
        {
          id: 'resp-ia-draft',
          texto: 'Borrador interno sin aprobar.',
          esGeneradaPorIA: true,
          aprobadaPorAnalista: false,
          autorNombre: 'MesaAyuda IA',
          fuentesCitadas: [],
          fechaCreacion: '2026-11-26T20:05:00Z'
        },
        {
          id: 'resp-ia-ok',
          texto: 'Respuesta aprobada oficialmente para el cliente.',
          esGeneradaPorIA: true,
          aprobadaPorAnalista: true,
          autorNombre: 'Santiago Graciano - Analista Soporte',
          fuentesCitadas: [],
          fechaCreacion: '2026-11-26T20:06:00Z'
        }
      ]
    };
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Borrador interno sin aprobar.');
    expect(text).not.toContain('Aprobar y Publicar al Cliente');
    expect(text).not.toContain('Eliminar');
    expect(text).toContain('Respuesta aprobada oficialmente para el cliente.');
    expect(text).toContain('Historial de Atención (1)');
  });

  it('debe bloquear acciones cuando el caso pasa reactivamente a estado Cerrado', () => {
    component.caso = { ...baseCaso, estado: 'Resuelto' };
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Pasar a Cerrado');

    component.caso = { ...baseCaso, estado: 'Cerrado' };
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Pasar a Cerrado');
    expect(text).not.toContain('Pedir sugerencia a la IA');
    expect(text).toContain('Cerrado');
  });
});
