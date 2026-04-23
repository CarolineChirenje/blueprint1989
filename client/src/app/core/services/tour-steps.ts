/**
 * Tour step definitions for each system role.
 * Each step can optionally declare a `route` -- the TourService will navigate
 * there (and wait for DOM settle) before showing the step.
 */

export interface TourStepDef {
  id: string;
  title: string;
  text: string;
  attachTo?: { element: string; on: string };
  route?: string;
  classes?: string;
  /** If true, open the mobile hamburger nav before showing this step (<=767px). */
  openMobileNav?: boolean;
}

// Blueprint: Tour steps stripped -- add your own tour steps here.
export const memberTourSteps: TourStepDef[] = [];
export const adminTourSteps: TourStepDef[] = [];
export const superAdminTourSteps: TourStepDef[] = [];
