import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CycleDetailComponent } from './components/cycle-detail/cycle-detail.component';

const routes: Routes = [
  { path: '', redirectTo: '/groups', pathMatch: 'full' },
  { path: ':id', component: CycleDetailComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class CyclesRoutingModule {}
