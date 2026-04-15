/**
 * Tour step definitions for each system role.
 * Each step can optionally declare a `route` — the TourService will navigate
 * there (and wait for DOM settle) before showing the step.
 */

export interface TourStepDef {
  id: string;
  title: string;
  text: string;
  attachTo?: { element: string; on: string };
  route?: string;
  classes?: string;
  /** If true, open the mobile hamburger nav before showing this step (≤767px). */
  openMobileNav?: boolean;
}

// ─── Shared Constants ───────────────────────────────────────────────────────
// Single source of truth — change a value here and every step reflects it.

const APP_NAME = 'Batanai';
const TAGLINE  = 'Bambanani';

const CLASSES = {
  WELCOME: 'shepherd-welcome',
} as const;

const ROUTES = {
  DASHBOARD:       '/dashboard',
  GROUPS:          '/groups',
  MGMT_USERS:      '/management/users',
  MGMT_APP_CONFIG: '/management/app-config',
} as const;

const SEL = {
  DASHBOARD_GREETING:    '#dashboard-greeting',
  OUTSTANDING_BALANCE:   '#outstanding-balance-section',
  NAV_GROUPS:            '#nav-groups',
  NEW_GROUP_BTN:         '#new-group-btn',
  NOTIFICATION_BELL:     '#notification-bell',
  HELP_MENU_BTN:         '#help-menu-btn',
  USER_MENU:             '#user-menu',
  NAV_MANAGEMENT:        '#nav-management',
  USERS_TABLE:           '#users-table',
  CONFIG_HEADER:         '#config-header',
} as const;

/** Factory for the final "done" step — keeps id + classes consistent. */
function buildDoneStep(title: string, text: string): TourStepDef {
  return { id: 'tour-done', title, text, classes: CLASSES.WELCOME };
}

// ─── Member Tour ────────────────────────────────────────────────────────────

export const memberTourSteps: TourStepDef[] = [
  {
    id: 'welcome',
    title: `${APP_NAME} - ${TAGLINE}`,
    text: `
      <p>${APP_NAME} makes it easy to <strong>share expenses</strong> and run
      <strong>rotating savings groups</strong> (Mukando) with friends, family or colleagues.</p>
      <p>Let us take a quick tour of the key features.</p>
    `,
    classes: CLASSES.WELCOME,
    route: ROUTES.DASHBOARD,
  },
  {
    id: 'dashboard-greeting',
    title: 'Your Dashboard',
    text: 'This is your home base. You will see a personalised greeting and today\'s date here.',
    attachTo: { element: SEL.DASHBOARD_GREETING, on: 'bottom' },
    route: ROUTES.DASHBOARD,
  },
  {
    id: 'outstanding-balance',
    title: 'Outstanding Balance',
    text: 'Your total outstanding balance across all active cycles is shown here, so you always know where you stand.',
    attachTo: { element: SEL.OUTSTANDING_BALANCE, on: 'bottom' },
    route: ROUTES.DASHBOARD,
  },
  {
    id: 'nav-groups',
    title: 'Groups',
    text: 'All your expense-sharing groups live here. Click <strong>Groups</strong> to view, create or join a group.',
    attachTo: { element: SEL.NAV_GROUPS, on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'groups-page',
    title: 'Create or Join a Group',
    text: `
      <p>Use <strong>New Group</strong> to create one or <strong>Join Group</strong>
      to enter a group code someone shared with you.</p>
      <p>Inside a group you will manage shared expense cycles and Mukando rounds.</p>
    `,
    attachTo: { element: SEL.NEW_GROUP_BTN, on: 'bottom' },
    route: ROUTES.GROUPS,
  },
  {
    id: 'notification-bell',
    title: 'Notifications',
    text: 'Invitations, payment confirmations, reminders and other updates appear here. A red badge shows your unread count.',
    attachTo: { element: SEL.NOTIFICATION_BELL, on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'help-menu',
    title: 'Help & Info',
    text: 'Find release notes, app info and you can <strong>re-launch this tour</strong> anytime from this menu.',
    attachTo: { element: SEL.HELP_MENU_BTN, on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'user-menu',
    title: 'Your Profile',
    text: 'Manage your profile, change your password, set up biometric login and configure notification preferences.',
    attachTo: { element: SEL.USER_MENU, on: 'bottom' },
  },
  buildDoneStep(
    'You are All Set!',
    `
      <p>That is everything to get started. Your next step:</p>
      <p><strong>Create a group</strong> or <strong>join one</strong> and start sharing expenses - ${TAGLINE}!</p>
    `,
  ),
];

// ─── Admin Tour ─────────────────────────────────────────────────────────────
// All member steps + management-specific steps inserted before the final "done" step.

const adminExtraSteps: TourStepDef[] = [
  {
    id: 'nav-management',
    title: 'Management',
    text: 'As an administrator you have access to the <strong>Management</strong> section — user accounts, app configuration, and feedback reports.',
    attachTo: { element: SEL.NAV_MANAGEMENT, on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'user-management',
    title: 'User Management',
    text: 'View all users, edit roles, activate or deactivate accounts and manage group memberships.',
    attachTo: { element: SEL.USERS_TABLE, on: 'top' },
    route: ROUTES.MGMT_USERS,
  },
  {
    id: 'app-config',
    title: 'App Configuration',
    text: 'Configure runtime settings like SMTP, JWT expiry, password policies and more. Changes take effect immediately unless a restart is required.',
    attachTo: { element: SEL.CONFIG_HEADER, on: 'bottom' },
    route: ROUTES.MGMT_APP_CONFIG,
  },
];

// Member steps minus the final "done", then admin extras, then admin done.
export const adminTourSteps: TourStepDef[] = [
  ...memberTourSteps.slice(0, -1),
  ...adminExtraSteps,
  buildDoneStep(
    'You are All Set, Admin!',
    `
      <p>You have full admin access. Besides sharing expenses, you can manage
      users and app settings from the <strong>Management</strong> menu.</p>
      <p>Explore the management tools anytime, ${TAGLINE}!</p>
    `,
  ),
];

// ─── SuperAdmin Tour ────────────────────────────────────────────────────────
// Same as Admin — no SuperAdmin-only UI pages currently exist.
export const superAdminTourSteps: TourStepDef[] = adminTourSteps;
