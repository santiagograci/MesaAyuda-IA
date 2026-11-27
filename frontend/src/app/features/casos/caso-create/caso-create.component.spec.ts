import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of } from 'rxjs';
import { CasosService } from '../../../core/services/casos.service';
import { CasoCreateComponent } from './caso-create.component';

describe('CasoCreateComponent (Deflexión preventiva RAG)', () => {
  let fixture: ComponentFixture<CasoCreateComponent>;
  let component: CasoCreateComponent;
  let casosServiceSpy: jasmine.SpyObj<CasosService>;

  beforeEach(async () => {
    casosServiceSpy = jasmine.createSpyObj<CasosService>('CasosService', [
      'consultarDeflexion',
      'crearCaso'
    ]);
    casosServiceSpy.consultarDeflexion.and.returnValue(
      of({
        tieneSugerencia: true,
        respuestaSugerida: 'Emita una Nota Crédito NC-01 referenciando el CUFE original.',
        fuentes: [
          {
            documento: '01-facturacion-electronica-y-anulaciones.md',
            seccion: 'Reglas de Anulación',
            fragmento: 'Use NC-01 ante FAC-402.',
            puntaje: 0.9
          }
        ]
      })
    );

    await TestBed.configureTestingModule({
      imports: [CasoCreateComponent],
      providers: [{ provide: CasosService, useValue: casosServiceSpy }]
    }).compileComponents();

    fixture = TestBed.createComponent(CasoCreateComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('debe consultar deflexión tras debounce de 700ms y permitir descartar la creación del ticket', fakeAsync(() => {
    const deflectedSpy = spyOn(component.deflected, 'emit');

    component.form.patchValue({
      titulo: 'Error FAC-402 al anular factura',
      descripcion: 'Al intentar anular factura DIAN muestra FAC-402 en pantalla.'
    });

    tick(650);
    expect(casosServiceSpy.consultarDeflexion).not.toHaveBeenCalled();

    tick(60);
    fixture.detectChanges();

    expect(casosServiceSpy.consultarDeflexion).toHaveBeenCalledTimes(1);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('Antes de crear el ticket, revisa si esta guía oficial resuelve tu consulta');
    expect(el.textContent).toContain('01-facturacion-electronica-y-anulaciones.md');

    const deflectBtn = el.querySelector('[data-testid="deflect-ticket-btn"]') as HTMLButtonElement;
    deflectBtn.click();
    fixture.detectChanges();
    tick(750);

    expect(deflectedSpy).toHaveBeenCalledTimes(1);
    expect(casosServiceSpy.consultarDeflexion).toHaveBeenCalledTimes(1);
    expect(casosServiceSpy.crearCaso).not.toHaveBeenCalled();
  }));

  it('debe rechazar archivos no imagen o mayores a 2 MB al adjuntar captura', () => {
    const el: HTMLElement = fixture.nativeElement;
    const input = el.querySelector('[data-testid="create-image-input"]') as HTMLInputElement;

    const badFile = new File(['hola'], 'documento.pdf', { type: 'application/pdf' });
    const dtBad = new DataTransfer();
    dtBad.items.add(badFile);
    Object.defineProperty(input, 'files', { value: dtBad.files, configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="create-image-error"]')?.textContent).toContain(
      'PNG, JPG o WebP'
    );

    const oversized = new File([new Uint8Array(2 * 1024 * 1024 + 5)], 'grande.png', {
      type: 'image/png'
    });
    const dtBig = new DataTransfer();
    dtBig.items.add(oversized);
    Object.defineProperty(input, 'files', { value: dtBig.files, configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(el.querySelector('[data-testid="create-image-error"]')?.textContent).toContain('2 MB');
  });
});
