import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PushTestComponent } from './push-test.component';

@NgModule({
  declarations: [PushTestComponent],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule.forChild([
      { path: '', component: PushTestComponent }
    ])
  ]
})
export class PushTestModule {}
