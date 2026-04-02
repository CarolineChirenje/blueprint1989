
import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { ReleaseNotesComponent } from './release-notes.component';

const routes: Routes = [
  { path: '', component: ReleaseNotesComponent }
];

@NgModule({
  declarations: [ReleaseNotesComponent],
  imports: [
    CommonModule,
    RouterModule.forChild(routes),
    MatExpansionModule,
    MatCardModule,
    MatChipsModule,
    MatDividerModule,
    MatIconModule
  ],
})
export class ReleaseNotesModule {}

