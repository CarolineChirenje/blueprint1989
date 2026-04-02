import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SharedModule } from '../../shared/shared.module';
import { OfflineQueueRoutingModule } from './offline-queue-routing.module';
import { OfflineQueueComponent } from './components/offline-queue.component';

@NgModule({
  declarations: [OfflineQueueComponent],
  imports: [
    CommonModule,
    SharedModule,
    OfflineQueueRoutingModule,
  ]
})
export class OfflineQueueModule { }
