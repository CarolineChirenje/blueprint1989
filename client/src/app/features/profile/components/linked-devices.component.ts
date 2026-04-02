import { Component, OnInit } from '@angular/core';
import { DeviceService } from '../../../core/services/device.service';
import { UserDeviceDto, InstallPromptStatus } from '../../../shared/models/device.model';

@Component({
  selector: 'app-linked-devices',
  templateUrl: './linked-devices.component.html',
  styleUrls: ['./linked-devices.component.css'],
  standalone: false
})
export class LinkedDevicesComponent implements OnInit {
  devices: UserDeviceDto[] = [];
  loading = true;
  errorMessage = '';
  successMessage = '';
  currentClientId: string;

  // Rename state
  renamingDeviceId: number | null = null;
  renameValue = '';

  constructor(private deviceService: DeviceService) {
    this.currentClientId = this.deviceService.getClientId();
  }

  ngOnInit(): void {
    this.loadDevices();
  }

  loadDevices(): void {
    this.loading = true;
    this.deviceService.getDevices().subscribe({
      next: devices => {
        this.devices = devices;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load devices.';
        this.loading = false;
      }
    });
  }

  isCurrentDevice(device: UserDeviceDto): boolean {
    return device.clientId === this.currentClientId;
  }

  canResetPrompt(device: UserDeviceDto): boolean {
    return this.isCurrentDevice(device) &&
      (device.installStatus === InstallPromptStatus.Unknown ||
       device.installStatus === InstallPromptStatus.RemindLater ||
       device.installStatus === InstallPromptStatus.NeverAskAgain);
  }

  resetInstallPrompt(): void {
    this.deviceService.resetInstallPrompt().subscribe({
      next: () => {
        const d = this.devices.find(x => x.clientId === this.currentClientId);
        if (d) {
          d.installStatus = InstallPromptStatus.Unknown;
          d.nextPromptAt = null;
        }
        this.successMessage = 'Install prompt reset — you will be asked again on your next visit.';
        setTimeout(() => { this.successMessage = ''; }, 4000);
      },
      error: () => { this.errorMessage = 'Failed to reset install prompt.'; }
    });
  }

  removeDevice(device: UserDeviceDto): void {
    if (!confirm(`Remove "${this.getDeviceLabel(device)}"? It will no longer appear in this list.`)) {
      return;
    }
    this.deviceService.deleteDevice(device.id).subscribe({
      next: () => {
        this.devices = this.devices.filter(d => d.id !== device.id);
        this.successMessage = 'Device removed successfully.';
        setTimeout(() => { this.successMessage = ''; }, 3000);
      },
      error: () => {
        this.errorMessage = 'Failed to remove device.';
      }
    });
  }

  getDeviceLabel(device: UserDeviceDto): string {
    if (device.friendlyName) return device.friendlyName;
    const parts: string[] = [];
    if (device.deviceManufacturer) parts.push(device.deviceManufacturer);
    if (device.deviceModel) parts.push(device.deviceModel);
    if (parts.length === 0) parts.push(device.deviceType || 'Unknown device');
    return parts.join(' ');
  }

  startRename(device: UserDeviceDto): void {
    this.renamingDeviceId = device.id;
    this.renameValue = device.friendlyName ?? this.getDeviceLabel(device);
  }

  cancelRename(): void {
    this.renamingDeviceId = null;
    this.renameValue = '';
  }

  saveRename(device: UserDeviceDto): void {
    const name = this.renameValue.trim();
    if (!name) return;
    this.deviceService.renameDevice(device.id, name).subscribe({
      next: () => {
        device.friendlyName = name;
        this.cancelRename();
        this.successMessage = 'Device renamed successfully.';
        setTimeout(() => { this.successMessage = ''; }, 3000);
      },
      error: () => {
        this.errorMessage = 'Failed to rename device.';
      }
    });
  }

  isDesktop(device: UserDeviceDto): boolean {
    return device.deviceType === 'Desktop';
  }

  formatDate(dateStr: string): string {
    // Server omits 'Z' due to legacy Npgsql timestamp behaviour — force UTC interpretation
    const utc = dateStr.endsWith('Z') || /[+\-]\d{2}:\d{2}$/.test(dateStr)
      ? dateStr : dateStr + 'Z';
    return new Date(utc).toLocaleString();
  }
}
