import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import Shepherd from 'shepherd.js';
import type { StepOptions, StepOptionsButton } from 'shepherd.js';
import { offset } from '@floating-ui/dom';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';
import {
  TourStepDef,
  memberTourSteps,
  adminTourSteps,
  superAdminTourSteps,
} from './tour-steps';

@Injectable({ providedIn: 'root' })
export class TourService {
  private tour: InstanceType<typeof Shepherd.Tour> | null = null;
  private apiUrl = `${environment.apiUrl}/tour`;

  constructor(
    private http: HttpClient,
    private router: Router,
    private auth: AuthService,
    private zone: NgZone
  ) {}

  /** Whether the current user has already completed (or dismissed) the tour. */
  isTourCompleted(): boolean {
    return this.auth.isTourCompleted();
  }

  /** Launch the role-appropriate tour. If a tour is already running, cancel it first. */
  startTour(role: string): void {
    if (this.tour) {
      this.tour.cancel();
      this.tour = null;
    }

    const steps = this.getStepsForRole(role);
    this.tour = this.createTour(steps);
    this.tour.start();
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private getStepsForRole(role: string): TourStepDef[] {
    switch (role) {
      case 'SuperAdmin':
        return superAdminTourSteps;
      case 'Admin':
        return adminTourSteps;
      default:
        return memberTourSteps;
    }
  }

  private createTour(steps: TourStepDef[]): InstanceType<typeof Shepherd.Tour> {
    const isMobile = window.innerWidth <= 767;

    const tour = new Shepherd.Tour({
      useModalOverlay: true,
      defaultStepOptions: {
        scrollTo: { behavior: 'smooth', block: 'center' },
        cancelIcon: { enabled: true },
        modalOverlayOpeningPadding: 8,
        modalOverlayOpeningRadius: 6,
        floatingUIOptions: {
          middleware: [offset(12)],
        },
      },
    });

    steps.forEach((def, idx) => {
      const isFirst = idx === 0;
      const isLast = idx === steps.length - 1;

      const buttons: StepOptionsButton[] = [];

      // Skip button (left-aligned via CSS class)
      if (!isLast) {
        buttons.push({
          text: 'Skip tour',
          classes: 'shepherd-btn-skip',
          action: () => tour.cancel(),
        });
      }

      // Back button
      if (!isFirst) {
        buttons.push({
          text: 'Back',
          classes: 'shepherd-btn-secondary',
          action: () => tour.back(),
        });
      }

      // Next / Start / Done button
      buttons.push({
        text: isFirst ? "Let's go!" : isLast ? 'Done' : 'Next',
        classes: 'shepherd-btn-primary',
        action: () => (isLast ? tour.complete() : tour.next()),
      });

      const stepOptions: StepOptions = {
        id: def.id,
        title: def.title,
        text: def.text,
        buttons,
        classes: def.classes || '',
      };

      if (def.attachTo) {
        stepOptions.attachTo = {
          element: def.attachTo.element,
          on: def.attachTo.on as any,
        };
      }

      // beforeShowPromise: navigate + open mobile nav if needed
      stepOptions.beforeShowPromise = () =>
        this.prepareStep(def, isMobile);

      tour.addStep(stepOptions);
    });

    // On complete or cancel → persist server-side
    tour.on('complete', () => this.completeTour());
    tour.on('cancel', () => this.completeTour());

    return tour;
  }

  /**
   * Navigate to the step's route (if specified) and optionally open
   * the mobile hamburger nav so navbar targets are visible.
   */
  private prepareStep(def: TourStepDef, isMobile: boolean): Promise<void> {
    return new Promise<void>((resolve) => {
      const navigate = def.route && this.router.url !== def.route;
      const openNav = def.openMobileNav && isMobile;

      if (navigate) {
        this.zone.run(() => {
          this.router.navigate([def.route!]).then(() => {
            if (openNav) this.openMobileNav();
            // Allow the component to render
            setTimeout(resolve, 400);
          });
        });
      } else {
        if (openNav) this.openMobileNav();
        // Small delay to allow any DOM changes
        setTimeout(resolve, openNav ? 200 : 50);
      }
    });
  }

  /** Click the hamburger button to open mobile nav if it's closed. */
  private openMobileNav(): void {
    const hamburger = document.querySelector<HTMLButtonElement>('.hamburger-btn');
    const nav = document.querySelector('.main-nav');
    if (hamburger && nav && !nav.classList.contains('open')) {
      hamburger.click();
    }
  }

  /** Persist tour completion server-side and update local user object. */
  private completeTour(): void {
    this.auth.markTourCompletedLocally();
    this.http.post(`${this.apiUrl}/complete`, {}).subscribe({
      error: (err) => console.warn('Failed to persist tour completion', err),
    });
    this.tour = null;
  }
}
