import { enableProdMode } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';
import { AppModule } from './app/app.module';
import { environment } from './environments/environment';

if (environment.production) {
  enableProdMode();
}

platformBrowserDynamic()
  .bootstrapModule(AppModule)
  .then(() => {
  })
  .catch(err => {
    console.error('Angular bootstrap error:', err);
    // Display error on page
    document.body.innerHTML = `<div style="padding: 20px; font-family: monospace; color: red;"><h2>Application Error</h2><pre>${err.message}\n${err.stack}</pre></div>`;
  });