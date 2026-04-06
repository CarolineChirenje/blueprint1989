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

// ─── Member Tour ────────────────────────────────────────────────────────────

export const memberTourSteps: TourStepDef[] = [
  {
    id: 'welcome',
    title: 'Batanai -Bambanani',
    text: `
      <p>Batanai makes it easy to <strong>share expenses</strong> and run
      <strong>rotating savings groups</strong> (Mukando) with friends, family or colleagues.</p>
      <p>Let us take a quick tour of the key features.</p>
    `,
    classes: 'shepherd-welcome',
    route: '/dashboard',
  },
  {
    id: 'dashboard-greeting',
    title: 'Your Dashboard',
    text: 'This is your home base. You will see a personalised greeting and today\'s date here.',
    attachTo: { element: '#dashboard-greeting', on: 'bottom' },
    route: '/dashboard',
  },
  {
    id: 'outstanding-balance',
    title: 'Outstanding Balance',
    text: 'Your total outstanding balance across all active cycles is shown here so you always know where you stand.',
    attachTo: { element: '#outstanding-balance-section', on: 'bottom' },
    route: '/dashboard',
  },
  {
    id: 'nav-groups',
    title: 'Groups',
    text: 'All your expense-sharing groups live here. Click <strong>Groups</strong> to view, create, or join a group.',
    attachTo: { element: '#nav-groups', on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'groups-page',
    title: 'Create or Join a Group',
    text: `
      <p>Use <strong>New Group</strong> to create one, or <strong>Join Group</strong>
      to enter a group code someone shared with you.</p>
      <p>Inside a group you'll manage shared expense cycles and Mukando rounds.</p>
    `,
    attachTo: { element: '#new-group-btn', on: 'bottom' },
    route: '/groups',
  },
  {
    id: 'notification-bell',
    title: 'Notifications',
    text: 'Invitations, payment confirmations, reminders and other updates appear here. A red badge shows your unread count.',
    attachTo: { element: '#notification-bell', on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'help-menu',
    title: 'Help & Info',
    text: 'Find release notes, app info and you can <strong>re-launch this tour</strong> anytime from this menu.',
    attachTo: { element: '#help-menu-btn', on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'user-menu',
    title: 'Your Profile',
    text: 'Manage your profile, change your password, set up biometric login and configure notification preferences.',
    attachTo: { element: '#user-menu', on: 'bottom' },
  },
  {
    id: 'tour-done',
    title: 'You are All Set',
    text: `
      <p>That is everything to get started. Your next step:</p>
      <p><strong>Create a group</strong> or <strong>join one</strong> and start sharing expenses asibambaneni!</p>
    `,
    classes: 'shepherd-welcome',
  },
];

// ─── Admin Tour ─────────────────────────────────────────────────────────────
// All member steps + management-specific steps inserted before the final "done" step.

const adminExtraSteps: TourStepDef[] = [
  {
    id: 'nav-management',
    title: 'Management',
    text: 'As an administrator you have access to the <strong>Management</strong> section — user accounts, app configuration and feedback reports.',
    attachTo: { element: '#nav-management', on: 'bottom' },
    openMobileNav: true,
  },
  {
    id: 'user-management',
    title: 'User Management',
    text: 'View all users, edit roles, activate or deactivate accounts and manage group memberships.',
    attachTo: { element: '#users-table', on: 'top' },
    route: '/management/users',
  },
  {
    id: 'app-config',
    title: 'App Configuration',
    text: 'Configure runtime settings like SMTP, JWT expiry, password policies and more. Changes take effect immediately unless a restart is required.',
    attachTo: { element: '#config-header', on: 'bottom' },
    route: '/management/app-config',
  },
];

const adminDoneStep: TourStepDef = {
  id: 'tour-done',
  title: 'You are All Set',
  text: `
    <p>You have full admin access. Besides sharing expenses, you can manage
    users and app settings from the <strong>Management</strong> menu.</p>
    <p>Explore the management tools anytime Asibambaneni!</p>
  `,
  classes: 'shepherd-welcome',
};

// Member steps minus the final "done", then admin extras, then admin done.
export const adminTourSteps: TourStepDef[] = [
  ...memberTourSteps.slice(0, -1),
  ...adminExtraSteps,
  adminDoneStep,
];

// ─── SuperAdmin Tour ────────────────────────────────────────────────────────
// Same as Admin — no SuperAdmin-only UI pages currently exist.
export const superAdminTourSteps: TourStepDef[] = adminTourSteps;
