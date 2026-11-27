import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CasoDetalle,
  PRIORIDAD_INFO,
  PRIORIDAD_LIST,
  PrioridadCaso,
  ProblemDetailsResponse
} from '../../../core/models/api.models';
import { CasosService } from '../../../core/services/casos.service';

/**
 * Formulario reactivo para registrar un nuevo caso de soporte ERP
 * con guía operativa de niveles de prioridad.
 */
@Component({
  selector: 'app-caso-create',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="create-card card-surface">
      <header class="create-header">
        <div>
          <h2>Nuevo Caso de Soporte ERP</h2>
          <p>Describe el código de error, módulo afectado y pasos previos realizados.</p>
        </div>
        <button type="button" class="btn btn-sm" (click)="cancelled.emit()">Cancelar</button>
      </header>

      <form [formGroup]="form" (ngSubmit)="submit()" class="create-form">
        <div class="field">
          <label for="titulo">Título del incidente</label>
          <input
            id="titulo"
            type="text"
            class="form-control"
            formControlName="titulo"
            placeholder="Ej. Error FAC-402 al anular factura electrónica DIAN"
          />
          @if (form.controls.titulo.touched && form.controls.titulo.invalid) {
            <small class="field-error">El título debe tener entre 5 y 150 caracteres.</small>
          }
        </div>

        <div class="field">
          <label for="prioridad">Prioridad</label>
          <select id="prioridad" class="form-control" formControlName="prioridad">
            @for (p of prioridades; track p) {
              <option [value]="p">{{ p }} — {{ prioridadInfo[p].etiqueta }}</option>
            }
          </select>

          <div class="priority-guide-box" role="note" aria-label="Guía de prioridades">
            <div class="guide-active">
              <span class="badge" [class]="'badge badge-prioridad-' + form.controls.prioridad.value">
                {{ form.controls.prioridad.value }}
              </span>
              <span>{{ prioridadInfo[form.controls.prioridad.value].descripcion }}</span>
            </div>
            <ul class="guide-list">
              @for (p of prioridades; track p) {
                <li [class.current]="form.controls.prioridad.value === p">
                  <strong>{{ p }}:</strong> {{ prioridadInfo[p].descripcion }}
                </li>
              }
            </ul>
          </div>
        </div>

        <div class="field">
          <label for="descripcion">Descripción detallada</label>
          <textarea
            id="descripcion"
            rows="5"
            class="form-control"
            formControlName="descripcion"
            placeholder="Describe el flujo exacto, mensaje de error y número de documento si aplica..."
          ></textarea>
          @if (form.controls.descripcion.touched && form.controls.descripcion.invalid) {
            <small class="field-error">La descripción debe tener entre 15 y 2000 caracteres.</small>
          }
        </div>

        @if (errorMsg()) {
          <div class="alert-error" role="alert">{{ errorMsg() }}</div>
        }

        <div class="actions">
          <button type="button" class="btn" (click)="cancelled.emit()">Volver</button>
          <button type="submit" class="btn btn-primary" [disabled]="submitting()">
            @if (submitting()) {
              <span class="spinner"></span> Creando caso...
            } @else {
              Registrar Caso
            }
          </button>
        </div>
      </form>
    </section>
  `,
  styles: [
    `
      .create-card {
        padding: 1.5rem;
      }
      .create-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
        margin-bottom: 1.25rem;
        h2 {
          margin: 0 0 0.25rem;
          font-size: 1.25rem;
        }
        p {
          margin: 0;
          color: var(--text-secondary);
          font-size: 0.88rem;
        }
      }
      .create-form {
        display: grid;
        gap: 1rem;
      }
      .field {
        display: grid;
        gap: 0.35rem;
        label {
          font-size: 0.85rem;
          font-weight: 600;
        }
      }
      .priority-guide-box {
        margin-top: 0.35rem;
        padding: 0.75rem 0.9rem;
        border-radius: calc(var(--radius-card) * 0.75);
        background: var(--bg-surface-alt);
        border: 1px solid var(--border-color);
        display: grid;
        gap: 0.5rem;
      }
      .guide-active {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        flex-wrap: wrap;
        font-size: 0.84rem;
        font-weight: 600;
      }
      .guide-list {
        margin: 0;
        padding-left: 1.1rem;
        display: grid;
        gap: 0.25rem;
        font-size: 0.78rem;
        color: var(--text-secondary);
        li.current {
          color: var(--text-primary);
        }
      }
      .field-error {
        color: var(--danger-text);
        font-size: 0.78rem;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
        gap: 0.65rem;
      }
    `
  ]
})
export class CasoCreateComponent {
  private readonly fb = inject(FormBuilder);
  private readonly casosService = inject(CasosService);

  @Output() created = new EventEmitter<CasoDetalle>();
  @Output() cancelled = new EventEmitter<void>();

  readonly prioridades = PRIORIDAD_LIST;
  readonly prioridadInfo = PRIORIDAD_INFO;
  readonly submitting = signal<boolean>(false);
  readonly errorMsg = signal<string>('');

  readonly form = this.fb.nonNullable.group({
    titulo: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(150)]],
    prioridad: ['Media' as PrioridadCaso, [Validators.required]],
    descripcion: ['', [Validators.required, Validators.minLength(15), Validators.maxLength(2000)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMsg.set('');

    this.casosService.crearCaso(this.form.getRawValue()).subscribe({
      next: (detalle) => {
        this.submitting.set(false);
        this.created.emit(detalle);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.errorMsg.set(this.extractErrorMessage(err));
      }
    });
  }

  private extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.error) {
      const problem = err.error as ProblemDetailsResponse;
      if (problem.errors) {
        const firstKey = Object.keys(problem.errors)[0];
        if (firstKey && problem.errors[firstKey]?.length) {
          return problem.errors[firstKey][0];
        }
      }
      return problem.detail || problem.title || 'No se pudo registrar el caso.';
    }
    return 'Error al comunicarse con el backend.';
  }
}
