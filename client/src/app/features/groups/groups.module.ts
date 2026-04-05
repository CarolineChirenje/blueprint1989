import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule } from '@angular/forms';
import { QRCodeComponent } from 'angularx-qrcode';
import { GroupsRoutingModule } from './groups-routing.module';
import { SharedModule } from '../../shared/shared.module';
import { GroupListComponent } from './group-list/group-list.component';
import { GroupDetailComponent } from './group-detail/group-detail.component';
import { CreateGroupDialogComponent } from './create-group-dialog/create-group-dialog.component';
import { GroupMembersDialogComponent } from './group-members-dialog/group-members-dialog.component';
import { JoinGroupDialogComponent } from './join-group-dialog/join-group-dialog.component';

@NgModule({
  declarations: [
    GroupListComponent,
    GroupDetailComponent,
    CreateGroupDialogComponent,
    GroupMembersDialogComponent,
    JoinGroupDialogComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    GroupsRoutingModule,
    SharedModule,
    QRCodeComponent
  ]
})
export class GroupsModule {}
