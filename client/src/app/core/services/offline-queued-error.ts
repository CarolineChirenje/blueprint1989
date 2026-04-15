/** Thrown when a submission was captured into the offline queue instead of sent to the server. */
export class OfflineQueuedError extends Error {
  constructor() {
    super('Entry saved to offline queue');
    this.name = 'OfflineQueuedError';
  }
}
