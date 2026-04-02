import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CycleListComponent } from './components/cycle-list/cycle-list.component';
import { CycleDetailComponent } from './components/cycle-detail/cycle-detail.component';

const routes: Routes = [
  { path: '', component: CycleListComponent },
  { path: ':id', component: CycleDetailComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CyclesRoutingModule {}
