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

// ── Shared base steps (all roles) ────────────────────────────────────────────

const welcomeStep: TourStepDef = {
  id: 'welcome',
  title: 'Welcome to Blueprint1989',
  text: 'This quick tour covers the key areas of the platform. Press <strong>Skip tour</strong> or the X button at any time to exit.',
  route: '/dashboard',
  classes: 'shepherd-center',
};

const dashboardStep: TourStepDef = {
  id: 'dashboard',
  title: 'Dashboard',
  text: 'Your home base at-a-glance view of your activity on the platform.',
  attachTo: { element: '#nav-dashboard', on: 'bottom' },
  route: '/dashboard',
  openMobileNav: true,
};

const notificationStep: TourStepDef = {
  id: 'notifications',
  title: 'Notifications',
  text: 'The bell shows unread in-app notifications. Click it to read platform messages, system announcements, resolved reports and more.',
  attachTo: { element: '#notification-bell', on: 'bottom-end' },
  route: '/dashboard',
};

const userMenuStep: TourStepDef = {
  id: 'user-menu',
  title: 'Your Account',
  text: 'Click your name to access your profile, security settings (MFA &amp; biometrics), linked devices, notification preferences and your submitted feedback reports.',
  attachTo: { element: '#user-menu', on: 'bottom-end' },
  route: '/dashboard',
};

const helpMenuStep: TourStepDef = {
  id: 'help-menu',
  title: 'Help',
  text: 'The Help menu contains release notes, an About page, and this tour so you can relaunch it any time.',
  attachTo: { element: '#help-menu-btn', on: 'bottom' },
  route: '/dashboard',
  openMobileNav: true,
};

const doneStep: TourStepDef = {
  id: 'done',
  title: "You're all set!",
  text: "That's the essentials. Use the <strong>floating button</strong> at the bottom-right of any screen to submit feedback or report a bug.",
  route: '/dashboard',
  classes: 'shepherd-center',
};

// ── Admin / SuperAdmin extra step ─────────────────────────────────────────────

const managementStep: TourStepDef = {
  id: 'management',
  title: 'Management',
  text: 'The Management menu gives you access to user management, app configuration and feedback report review.',
  attachTo: { element: '#nav-management-link', on: 'bottom' },
  route: '/dashboard',
  openMobileNav: true,
};

const superAdminManagementStep: TourStepDef = {
  id: 'management',
  title: 'Management',
  text: 'The Management menu covers user management, app configuration, feedback reports and as SuperAdmin the ability to trigger a graceful service restart.',
  attachTo: { element: '#nav-management-link', on: 'bottom' },
  route: '/dashboard',
  openMobileNav: true,
};

// ── Exported step arrays ──────────────────────────────────────────────────────

export const memberTourSteps: TourStepDef[] = [
  welcomeStep,
  dashboardStep,
  notificationStep,
  userMenuStep,
  helpMenuStep,
  doneStep,
];

export const adminTourSteps: TourStepDef[] = [
  welcomeStep,
  dashboardStep,
  notificationStep,
  userMenuStep,
  managementStep,
  helpMenuStep,
  doneStep,
];

export const superAdminTourSteps: TourStepDef[] = [
  welcomeStep,
  dashboardStep,
  notificationStep,
  userMenuStep,
  superAdminManagementStep,
  helpMenuStep,
  doneStep,
];
