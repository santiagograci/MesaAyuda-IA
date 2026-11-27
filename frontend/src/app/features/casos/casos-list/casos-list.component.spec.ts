import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { CasoResumen } from '../../../core/models/api.models';
import { CasosListComponent } from './casos-list.component';

describe('CasosListComponent', () => {
  let fixture: ComponentFixture<CasosListComponent>;
  let component: CasosListComponent;

  const mockCasos: CasoResumen[] = [
    {
      id: 'caso-1',
      titulo: 'Error FAC-402 al anular factura electrónica DIAN',
      prioridad: 'Alta',
      estado: 'Abierto',
      fechaCreacion: '2026-11-26T15:00:00Z',
      clienteNombre: 'Carlos Mendoza - Contabilidad',
      totalRespuestas: 1,
      tieneSugerenciaPendiente: true
    },
    {
      id: 'caso-2',
      titulo: 'Cierre contable bloqueado por cuenta puente 999905',
      prioridad: 'Critica',
      estado: 'Resuelto',
      fechaCreacion: '2026-11-26T14:00:00Z',
      clienteNombre: 'Laura Gómez - Tesorería',
      totalRespuestas: 2,
      tieneSugerenciaPendiente: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CasosListComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(CasosListComponent);
    component = fixture.componentInstance;
    component.casos = mockCasos;
    component.totalCount = 2;
    component.totalPages = 1;
    component.page = 1;
    fixture.detectChanges();
  });

  it('debe renderizar los casos paginados con sus badges de estado, prioridad e indicador IA', () => {
    const el: HTMLElement = fixture.nativeElement;

    expect(el.textContent).toContain('Error FAC-402 al anular factura electrónica DIAN');
    expect(el.textContent).toContain('Cierre contable bloqueado por cuenta puente 999905');
    expect(el.textContent).toContain('2 registros');
    expect(el.textContent).toContain('Página 1 de 1');

    expect(el.querySelector('.badge-estado-Abierto')).not.toBeNull();
    expect(el.querySelector('.badge-estado-Resuelto')).not.toBeNull();
    expect(el.querySelector('.badge-prioridad-Alta')).not.toBeNull();
    expect(el.querySelector('.badge-prioridad-Critica')).not.toBeNull();
    expect(el.textContent).toContain('✨ IA pendiente');
  });

  it('debe emitir estadoFilterChange al cambiar el filtro y selectCaso al elegir un caso', () => {
    const filterSpy = spyOn(component.estadoFilterChange, 'emit');
    const selectSpy = spyOn(component.selectCaso, 'emit');

    const el: HTMLElement = fixture.nativeElement;
    const filterButtons = Array.from(el.querySelectorAll('.filter-pill')) as HTMLButtonElement[];
    const abiertoBtn = filterButtons.find((b) => b.textContent?.trim() === 'Abierto');

    expect(abiertoBtn).toBeDefined();
    abiertoBtn!.click();
    expect(filterSpy).toHaveBeenCalledWith('Abierto');

    const firstCaseCard = el.querySelector('.case-item') as HTMLButtonElement;
    firstCaseCard.click();
    expect(selectSpy).toHaveBeenCalledWith('caso-1');
  });

  it('debe mostrar la pestaña de papelera para Analista y emitir restaurarCaso y restaurarDemo', () => {
    const toggleSpy = spyOn(component.togglePapelera, 'emit');
    const restoreSpy = spyOn(component.restaurarCaso, 'emit');
    const demoSpy = spyOn(component.restaurarDemo, 'emit');

    component.isAnalista = true;
    component.papeleraCount = 1;
    component.papeleraCasos = [
      {
        id: 'del-1',
        titulo: 'Caso eliminado de prueba',
        prioridad: 'Media',
        estado: 'Abierto',
        fechaCreacion: '2026-11-26T14:00:00Z',
        fechaEliminacion: '2026-11-26T16:00:00Z',
        clienteNombre: 'Carlos Mendoza'
      }
    ];
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const trashTab = el.querySelector('.trash-tab') as HTMLButtonElement;
    expect(trashTab.textContent).toContain('Papelera (1)');
    trashTab.click();
    expect(toggleSpy).toHaveBeenCalledWith(true);

    component.papeleraMode = true;
    fixture.detectChanges();

    expect(el.textContent).toContain('Caso eliminado de prueba');
    const restoreBtn = Array.from(el.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Restaurar caso')
    ) as HTMLButtonElement;
    restoreBtn.click();
    expect(restoreSpy).toHaveBeenCalledWith('del-1');

    const demoBtn = Array.from(el.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Restaurar Casos Demo')
    ) as HTMLButtonElement;
    demoBtn.click();
    expect(demoSpy).toHaveBeenCalled();
  });

  it('debe emitir busquedaChange con debounce de 300ms y prioridadFilterChange al cambiar prioridad', fakeAsync(() => {
    const searchSpy = spyOn(component.busquedaChange, 'emit');
    const prioSpy = spyOn(component.prioridadFilterChange, 'emit');

    const el: HTMLElement = fixture.nativeElement;
    const searchInput = el.querySelector('[data-testid="cases-search-input"]') as HTMLInputElement;
    searchInput.value = 'CON-301';
    searchInput.dispatchEvent(new Event('input'));

    tick(200);
    expect(searchSpy).not.toHaveBeenCalled();

    tick(100);
    expect(searchSpy).toHaveBeenCalledWith('CON-301');

    const prioSelect = el.querySelector('[data-testid="priority-filter-select"]') as HTMLSelectElement;
    prioSelect.value = 'Critica';
    prioSelect.dispatchEvent(new Event('change'));
    expect(prioSpy).toHaveBeenCalledWith('Critica');
  }));
});
