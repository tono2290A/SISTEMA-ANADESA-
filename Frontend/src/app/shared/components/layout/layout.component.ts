import { Component, OnDestroy, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Subscription, fromEvent, take } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { PermisoService } from '../../../core/services/permiso.service';
import { AparienciaService } from '../../../core/services/apariencia.service';
import { Usuario } from '../../../core/models/models';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './layout.component.html',
  styleUrls: ['./layout.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LayoutComponent implements OnInit, OnDestroy {
  sidebarCollapsed = false;
  currentUser: Usuario | null = null;
  toasts: { type: string; message: string }[] = [];
  // Fail-closed: sin permisos confirmados solo se muestra Dashboard.
  tieneInicio = true;
  tieneParticipantes = false;
  tieneAcademico = false;
  tieneActividades = false;
  tieneConfiguracion = false;
  tieneMonitoreo = false;
  monitoreoAbierto = false;
  tieneEvaluacion = false;
  evaluacionAbierto = false;
  tieneReportes = false;
  esMovil = false;
  menuMovilAbierto = false;
  nombreSistema = 'ANADESA';
  logoUrl = '/LOGO_ANADESA.jpg';
  private subRuta?: Subscription;
  private subResize?: Subscription;
  private subApariencia?: Subscription;

  constructor(
    private authService: AuthService,
    private permisoService: PermisoService,
    private aparienciaService: AparienciaService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.nombreSistema = this.aparienciaService.datos.nombre_sistema;
    this.logoUrl = this.aparienciaService.datos.logo || '/LOGO_ANADESA.jpg';
    this.subApariencia = this.aparienciaService.datos$.pipe(take(1)).subscribe(datos => {
      this.nombreSistema = datos.nombre_sistema;
      this.logoUrl = datos.logo || '/LOGO_ANADESA.jpg';
    });
    this.currentUser = this.authService.getCurrentUser();
    this.revisarMenus();
    this.permisoService.cargar().subscribe({
      next: () => this.revisarMenus(),
      error: () => this.revisarMenus()
    });
    this.detectarMovil();
    this.subResize = fromEvent(window, 'resize').subscribe(() => this.detectarMovil());
    this.monitoreoAbierto = this.router.url.startsWith('/monitoreo');
    this.evaluacionAbierto = this.router.url.startsWith('/evaluacion');
    this.subRuta = this.router.events.subscribe(ev => {
      if (ev instanceof NavigationEnd) {
        this.monitoreoAbierto = ev.urlAfterRedirects.startsWith('/monitoreo');
        this.evaluacionAbierto = ev.urlAfterRedirects.startsWith('/evaluacion');
        // Los permisos pueden haberse cargado en un guard despues de un fallo
        // anterior: recalcular el menu en cada navegacion.
        this.revisarMenus();
        if (this.esMovil) this.menuMovilAbierto = false;
      }
    });
  }

  ngOnDestroy() {
    this.subRuta?.unsubscribe();
    this.subResize?.unsubscribe();
    this.subApariencia?.unsubscribe();
  }

  /** Pantallas de tablet/telefono: el menu lateral pasa a drawer deslizable. */
  private detectarMovil() {
    this.esMovil = window.innerWidth <= 992;
    if (!this.esMovil) this.menuMovilAbierto = false;
  }

  alternarMenuMovil() {
    this.menuMovilAbierto = !this.menuMovilAbierto;
  }

  cerrarMenuMovil() {
    this.menuMovilAbierto = false;
  }

  toggleMonitoreo() {
    this.monitoreoAbierto = !this.monitoreoAbierto;
  }

  toggleEvaluacion() {
    this.evaluacionAbierto = !this.evaluacionAbierto;
  }

  private revisarMenus() {
    const ok = this.permisoService.estaCargado();
    // El rol "Facilitador Mujeres" nunca ve el Registro Academico (es solo
    // para Educacion); los demas roles lo ven si su rol tiene el permiso.
    const esMujeres = this.authService.getRoles().some(r => (r || '').includes('Mujer'));
    // Sin permisos confirmados (carga en curso o fallo) solo Dashboard.
    this.tieneInicio = !ok || this.permisoService.puede(1, 'Consultar');
    this.tieneParticipantes = ok && this.permisoService.puede(3, 'Consultar');
    this.tieneAcademico = ok && !esMujeres && this.permisoService.puede(4, 'Consultar');
    this.tieneActividades = ok && this.permisoService.puede(5, 'Consultar');
    this.tieneMonitoreo = ok && this.permisoService.puede(6, 'Consultar');
    this.tieneEvaluacion = ok && this.permisoService.puede(7, 'Consultar');
    this.tieneReportes = ok && this.permisoService.puede(8, 'Consultar');
    this.tieneConfiguracion = ok && this.permisoService.puede(9, 'Consultar');
    this.cdr.markForCheck();
  }

  getInitials(): string {
    if (!this.currentUser) return '?';
    const n = this.currentUser.primer_nombre?.[0] || '';
    const a = this.currentUser.primer_apellido?.[0] || '';
    return (n + a).toUpperCase();
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  showToast(type: string, message: string) {
    this.toasts.push({ type, message });
    setTimeout(() => {
      this.toasts.shift();
    }, 4000);
  }
}
