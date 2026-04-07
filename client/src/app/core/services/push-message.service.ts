import { Injectable } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface PushReceivedEvent {
  notificationType: number;
  relatedEntityId: number | null;
}

@Injectable({ providedIn: 'root' })
export class PushMessageService {
  private _pushReceived = new Subject<PushReceivedEvent>();
  pushReceived$: Observable<PushReceivedEvent> = this._pushReceived.asObservable();

  constructor() {
    if (typeof navigator !== 'undefined' && 'serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', (event: MessageEvent) => {
        if (event.data?.type === 'PUSH_RECEIVED') {
          this._pushReceived.next({
            notificationType: event.data.notificationType,
            relatedEntityId: event.data.relatedEntityId ?? null
          });
        }
      });
    }
  }
}
