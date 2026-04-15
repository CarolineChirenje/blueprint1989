import { Component, forwardRef, Input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'app-password-field',
  templateUrl: './password-field.component.html',
  styleUrls: ['./password-field.component.css'],
  standalone: false,
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => PasswordFieldComponent),
    multi: true
  }]
})
export class PasswordFieldComponent implements ControlValueAccessor {
  @Input() placeholder = '';
  @Input() inputId = '';
  @Input() autocomplete = 'current-password';

  showPassword = false;
  value = '';
  isDisabled = false;

  private onChange = (_: string) => {};
  onTouched = () => {};

  writeValue(val: string): void {
    this.value = val ?? '';
  }

  registerOnChange(fn: (_: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.isDisabled = isDisabled;
  }

  onInput(event: Event): void {
    this.value = (event.target as HTMLInputElement).value;
    this.onChange(this.value);
  }

  toggle(): void {
    this.showPassword = !this.showPassword;
  }
}
