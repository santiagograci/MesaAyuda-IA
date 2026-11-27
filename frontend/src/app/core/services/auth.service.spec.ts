import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LoginResponse } from '../models/api.models';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('login() debe guardar el token en localStorage y actualizar los Signals de sesión y rol', () => {
    const mockResponse: LoginResponse = {
      token: 'jwt.mock.token.2026',
      userId: '11111111-1111-1111-1111-111111111111',
      nombre: 'Santiago Graciano - Analista Soporte',
      email: 'analista@ofima.com',
      rol: 'Analista'
    };

    let received: LoginResponse | undefined;
    service.login({ email: 'analista@ofima.com', password: 'Ofima2026*' }).subscribe((res) => {
      received = res;
    });

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      email: 'analista@ofima.com',
      password: 'Ofima2026*'
    });
    req.flush(mockResponse);

    expect(received).toEqual(mockResponse);
    expect(service.getToken()).toBe('jwt.mock.token.2026');
    expect(service.currentUser()).toEqual(mockResponse);
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isAnalista()).toBeTrue();
    expect(service.isCliente()).toBeFalse();

    const persisted = localStorage.getItem('mesaayuda_session_v1');
    expect(persisted).toContain('jwt.mock.token.2026');
  });

  it('logout() debe limpiar localStorage, restablecer Signals y navegar a /login', () => {
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    service.login({ email: 'cliente@empresa.com', password: 'Ofima2026*' }).subscribe();
    const req = httpMock.expectOne('/api/auth/login');
    req.flush({
      token: 'jwt.cliente.token',
      userId: '22222222-2222-2222-2222-222222222222',
      nombre: 'Carlos Mendoza - Contabilidad',
      email: 'cliente@empresa.com',
      rol: 'Cliente'
    } satisfies LoginResponse);

    expect(service.isAuthenticated()).toBeTrue();

    service.logout();

    expect(service.currentUser()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.isAnalista()).toBeFalse();
    expect(service.getToken()).toBeNull();
    expect(localStorage.getItem('mesaayuda_session_v1')).toBeNull();
    expect(navigateSpy).toHaveBeenCalledWith(['/login']);
  });
});
