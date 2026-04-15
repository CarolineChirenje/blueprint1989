import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardRoutingModule } from './dashboard-routing.module';
import { DashboardComponent } from './components/dashboard.component';
import { RecordTypePickerComponent } from './components/record-type-picker/record-type-picker.component';

@NgModule({
  declarations: [
    DashboardComponent,
    RecordTypePickerComponent
  ],
  imports: [
    CommonModule,
    DashboardRoutingModule
  ]
})
export class DashboardModule { }
