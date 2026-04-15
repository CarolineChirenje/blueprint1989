import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { OfflineQueueComponent } from './components/offline-queue.component';

const routes: Routes = [
  { path: '', component: OfflineQueueComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class OfflineQueueRoutingModule { }
