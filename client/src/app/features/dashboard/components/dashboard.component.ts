import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { Role } from '../../../shared/models/user.model';

@Component({
    selector: 'app-dashboard',
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.css'],
    standalone: false
})
export class DashboardComponent implements OnInit, OnDestroy {
  userName: string = '';
  currentTime: Date = new Date();
  showRecordPicker: boolean = false;
  private clockInterval: ReturnType<typeof setInterval> | null = null;
  
  constructor(
    private auth: AuthService,
    private router: Router
  ) {}
  
  ngOnInit() {
    this.userName = this.auth.getUserDisplayName();
    
    // Update time every second
    this.clockInterval = setInterval(() => {
      this.currentTime = new Date();
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.clockInterval !== null) {
      clearInterval(this.clockInterval);
    }
  }
  
  getGreeting(): string {
    const hour = this.currentTime.getHours();
    if (hour < 12) return 'Good Morning';
    if (hour < 18) return 'Good Afternoon';
    return 'Good Evening';
  }
  
  canAccessUsers(): boolean {
    const roleId = this.auth.getUserRoleId();
    return roleId === Role.SuperAdmin || roleId === Role.Admin;
  }

  canAccessTerms(): boolean {
    const roleId = this.auth.getUserRoleId();
    return roleId === Role.SuperAdmin || roleId === Role.Admin;
  }

  showDiabetes(): boolean {
    return false;
  }

  showIncidentsCard(): boolean {
    return false;
  }

  showBloodPressureCard(): boolean {
    return false;
  }

  showRecordCard(): boolean {
    return false;
  }

  openRecordCard(): void {}

  onRecordTypeSelected(type: 'diabetes' | 'bp'): void {
    this.showRecordPicker = false;
  }

  onRecordPickerCancelled(): void {
    this.showRecordPicker = false;
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }
}
