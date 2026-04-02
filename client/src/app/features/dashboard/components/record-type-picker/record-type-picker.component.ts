import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';

@Component({
  selector: 'app-record-type-picker',
  templateUrl: './record-type-picker.component.html',
  styleUrls: ['./record-type-picker.component.css'],
  standalone: false
})
export class RecordTypePickerComponent {
  @Input() visible: boolean = false;
  @Output() typeSelected = new EventEmitter<'diabetes' | 'bp'>();
  @Output() pickCancelled = new EventEmitter<void>();

  select(type: 'diabetes' | 'bp'): void {
    this.typeSelected.emit(type);
  }

  cancel(): void {
    this.pickCancelled.emit();
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.visible) {
      this.cancel();
    }
  }
}
