import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface TechItem {
  name: string;
  icon: string;
  description: string;
}

interface AppVersionDto {
  frontendVersion: string;
  backendVersion: string;
}

@Component({
  selector: 'app-about',
  templateUrl: './about.component.html',
  styleUrls: ['./about.component.css'],
  standalone: false
})
export class AboutComponent implements OnInit {
  readonly appName = 'Vitara';
  readonly currentYear = 2026;
  readonly contactEmail = 'carochire@gmail.com';
  readonly companyName = 'elroitec';
  readonly founderName = 'Wadzanai Caroline Chirenje';

  frontendVersion = '';
  backendVersion = '';

  readonly techStack: TechItem[] = [
    { name: 'Angular 21', icon: 'web', description: 'Frontend SPA framework' },
    { name: '.NET 10', icon: 'dns', description: 'Backend REST API' },
    { name: 'Angular Material', icon: 'palette', description: 'UI component library' },
    { name: 'PWA', icon: 'install_mobile', description: 'Progressive Web App' },
    { name: 'PostgreSQL', icon: 'storage', description: 'Relational database' },
    { name: 'JWT Auth', icon: 'lock', description: 'Secure authentication' },
  ];

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<AppVersionDto>(`${environment.apiUrl}/app-config/version`).subscribe({
      next: v => {
        this.frontendVersion = v.frontendVersion;
        this.backendVersion = v.backendVersion;
      },
      error: () => {
        this.frontendVersion = '—';
        this.backendVersion = '—';
      }
    });
  }
}
``