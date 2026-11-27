import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { CasoDetalle } from '../../../core/models/api.models';
import { AuthService } from '../../../core/services/auth.service';
import { CasosService } from '../../../core/services/casos.service';
import { CasoDetailComponent } from './caso-detail.component';

describe('CasoDetailComponent (Protección contra borrado con cuenta regresiva)', () => {
  let fixture: ComponentFixture<CasoDetailComponent>;
  let component: CasoDetailComponent;
  let casosServiceSpy: jasmine.SpyObj<CasosService>;

  const mockCaso: CasoDetalle = {
    id: 'caso-delete-1',
    titulo: 'Error FAC-402 al anular factura electrónica',
    descripcion: 'Descripción de prueba para validar confirmación con cuenta regresiva de 3 segundos.',
    prioridad: 'Alta',
    estado: 'Abierto',
    fechaCreacion: '2026-11-26T15:00:00Z',
    clienteId: 'cli-1',
    clienteNombre: 'Carlos Mendoza',
    respuestas: []
  };

  beforeEach(async () => {
    casosServiceSpy = jasmine.createSpyObj<CasosService>('CasosService', ['eliminarCaso']);
    casosServiceSpy.eliminarCaso.and.returnValue(of(void 0));

    const authMock = {
      isAnalista: signal(true),
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
});
