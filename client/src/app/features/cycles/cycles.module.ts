import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { CyclesRoutingModule } from './cycles-routing.module';
import { SharedModule } from '../../shared/shared.module';
import { CycleDetailComponent } from './components/cycle-detail/cycle-detail.component';
import { AddExpenseDialogComponent } from './components/add-expense-dialog/add-expense-dialog.component';
import { AddPaymentDialogComponent } from './components/add-payment-dialog/add-payment-dialog.component';
import { RespondPaymentDialogComponent } from './components/respond-payment-dialog/respond-payment-dialog.component';
import { CreateCycleDialogComponent } from './components/create-cycle-dialog/create-cycle-dialog.component';
import { DisputeExpenseDialogComponent } from './components/dispute-expense-dialog/dispute-expense-dialog.component';

@NgModule({
  declarations: [
    CycleDetailComponent,
    AddExpenseDialogComponent,
    AddPaymentDialogComponent,
    RespondPaymentDialogComponent,
    CreateCycleDialogComponent,
    DisputeExpenseDialogComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CyclesRoutingModule,
    SharedModule
  ]
})
export class CyclesModule {}
