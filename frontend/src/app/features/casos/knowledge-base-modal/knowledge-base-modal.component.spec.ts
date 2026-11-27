import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DocumentosService } from '../../../core/services/documentos.service';
import { KnowledgeBaseModalComponent } from './knowledge-base-modal.component';

describe('KnowledgeBaseModalComponent', () => {
  let fixture: ComponentFixture<KnowledgeBaseModalComponent>;
  let component: KnowledgeBaseModalComponent;
  let documentosServiceSpy: jasmine.SpyObj<DocumentosService>;

  beforeEach(async () => {
    documentosServiceSpy = jasmine.createSpyObj<DocumentosService>('DocumentosService', [
      'listarDocumentos',
      'ingestarArchivo'
    ]);

    documentosServiceSpy.listarDocumentos.and.returnValue(
      of({
        documentos: [
          { documento: '01-facturacion-electronica-y-anulaciones.md', fragmentos: 14 },
          { documento: '02-cierre-contable-y-conciliacion.md', fragmentos: 16 }
        ],
        totalDocumentos: 2,
        totalFragmentos: 30
      })
    );

    await TestBed.configureTestingModule({
      imports: [KnowledgeBaseModalComponent],
      providers: [{ provide: DocumentosService, useValue: documentosServiceSpy }]
    }).compileComponents();

    fixture = TestBed.createComponent(KnowledgeBaseModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('debe listar los documentos indexados con su conteo de fragmentos', () => {
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('01-facturacion-electronica-y-anulaciones.md');
    expect(el.textContent).toContain('14 fragmentos');
    expect(el.textContent).toContain('2 documentos · 30 fragmentos');
  });

  it('debe subir un manual y mostrar feedback de chunks indexados', () => {
    const file = new File(['# Manual POS\nError POS-777'], '07-manual-pos.md', {
      type: 'text/markdown'
    });
    documentosServiceSpy.ingestarArchivo.and.returnValue(
      of({
        archivo: '07-manual-pos.md',
        fragmentosArchivo: 5,
        totalDocumentos: 3,
        totalFragmentos: 35,
        estado: 'ok'
      })
    );

    component.selectedFile.set(file);
    component.onSubmit(new Event('submit'));
    fixture.detectChanges();

    expect(documentosServiceSpy.ingestarArchivo).toHaveBeenCalledWith(file);
    const el: HTMLElement = fixture.nativeElement;
    expect(el.textContent).toContain('07-manual-pos.md');
    expect(el.textContent).toContain('5');
    expect(el.textContent).toContain('3 manuales / 35 fragmentos');
  });
});
