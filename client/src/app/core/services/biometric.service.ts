import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, from, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

export interface WebAuthnCredentialDto {
  id: number;
  deviceFriendlyName?: string;
  createdAt: string;
  lastUsedAt?: string;
}

/**
 * BiometricService — wraps the browser WebAuthn (FIDO2) API to provide
 * fingerprint / Face ID / Windows Hello login for the Blueprint1989 PWA.
 *
 * How it works:
 *  - Registration: Server generates a challenge → browser invokes platform authenticator
 *    (OS-level biometric prompt) → server verifies & stores the public key.
 *  - Authentication: Server generates a challenge with the user's allowed credentials →
 *    browser invokes the same platform authenticator → server verifies the signature
 *    and issues a JWT (MFA bypassed).
 *
 * A small localStorage hint (`bgl_biometric_email`) tracks whether this device has a
 * registered biometric credential, so the login page can show the biometric button
 * without requiring the user to type their email first.
 */
@Injectable({ providedIn: 'root' })
export class BiometricService {
  private readonly apiUrl = `${environment.apiUrl}/webauthn`;
  private readonly hintKey = 'bgl_biometric_email';

  constructor(private http: HttpClient) {}

  // ─── Capability detection ──────────────────────────────────────────────────

  /** Returns true if the WebAuthn API is available in this browser. */
  isSupported(): boolean {
    return typeof window !== 'undefined' && !!window.PublicKeyCredential;
  }

  /**
   * Returns true if the device has a platform authenticator (fingerprint sensor,
   * Face ID, Touch ID, Windows Hello). This is the gate for showing the biometric
   * login button — works in both mobile browsers and installed PWAs.
   */
  isPlatformAuthenticatorAvailable(): Promise<boolean> {
    if (!this.isSupported()) return Promise.resolve(false);
    return PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
  }

  // ─── localStorage hint ────────────────────────────────────────────────────

  getBiometricHintEmail(): string | null {
    return localStorage.getItem(this.hintKey);
  }

  setBiometricHint(email: string): void {
    localStorage.setItem(this.hintKey, email);
  }

  clearBiometricHint(): void {
    localStorage.removeItem(this.hintKey);
  }

  // ─── Registration ─────────────────────────────────────────────────────────

  /**
   * Full registration flow:
   *   1. GET challenge from server (`/registration/begin`)
   *   2. Call `navigator.credentials.create()` — triggers device biometric prompt
   *   3. POST attestation to server (`/registration/complete`)
   */
  registerCredential(friendlyName?: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/registration/begin`, {}).pipe(
      switchMap(options => {
        const publicKey = this.parseCreationOptions(options);
        return from(navigator.credentials.create({ publicKey }) as Promise<PublicKeyCredential | null>);
      }),
      switchMap(credential => {
        if (!credential) return throwError(() => new Error('Registration was cancelled or failed.'));
        const resp = credential.response as AuthenticatorAttestationResponse;
        const body = {
          attestation: {
            id: credential.id,
            rawId: this.toBase64Url(credential.rawId),
            type: credential.type,
            response: {
              attestationObject: this.toBase64Url(resp.attestationObject),
              clientDataJSON: this.toBase64Url(resp.clientDataJSON)
            },
            clientExtensionResults: credential.getClientExtensionResults() ?? {}
          },
          friendlyName: friendlyName ?? null
        };
        return this.http.post<any>(`${this.apiUrl}/registration/complete`, body);
      }),
      catchError(err => throwError(() => err))
    );
  }

  // ─── Authentication ───────────────────────────────────────────────────────

  /**
   * Full authentication flow:
   *   1. POST email to server (`/authentication/begin`) — server returns allowed credentials + challenge
   *   2. Call `navigator.credentials.get()` — triggers device biometric prompt
   *   3. POST assertion to server (`/authentication/complete`) — server returns AuthResponse (JWT)
   */
  authenticateWithBiometric(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/authentication/begin`, { email }).pipe(
      switchMap(options => {
        const publicKey = this.parseAssertionOptions(options);
        return from(navigator.credentials.get({ publicKey }) as Promise<PublicKeyCredential | null>);
      }),
      switchMap(assertion => {
        if (!assertion) return throwError(() => new Error('Biometric authentication was cancelled.'));
        const resp = assertion.response as AuthenticatorAssertionResponse;
        const body = {
          email,
          assertion: {
            id: assertion.id,
            rawId: this.toBase64Url(assertion.rawId),
            type: assertion.type,
            response: {
              authenticatorData: this.toBase64Url(resp.authenticatorData),
              clientDataJSON: this.toBase64Url(resp.clientDataJSON),
              signature: this.toBase64Url(resp.signature),
              userHandle: resp.userHandle ? this.toBase64Url(resp.userHandle) : null
            },
            clientExtensionResults: assertion.getClientExtensionResults() ?? {}
          }
        };
        return this.http.post<any>(`${this.apiUrl}/authentication/complete`, body);
      }),
      catchError(err => throwError(() => err))
    );
  }

  // ─── Credential management ────────────────────────────────────────────────

  getCredentials(): Observable<WebAuthnCredentialDto[]> {
    return this.http.get<WebAuthnCredentialDto[]>(`${this.apiUrl}/credentials`);
  }

  deleteCredential(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/credentials/${id}`);
  }

  // ─── Helpers: parse server options for the browser API ───────────────────

  private parseCreationOptions(opts: any): PublicKeyCredentialCreationOptions {
    return {
      ...opts,
      challenge: this.fromBase64Url(opts.challenge),
      user: { ...opts.user, id: this.fromBase64Url(opts.user.id) },
      excludeCredentials: (opts.excludeCredentials ?? []).map((c: any) => ({
        ...c,
        id: this.fromBase64Url(c.id)
      }))
    };
  }

  private parseAssertionOptions(opts: any): PublicKeyCredentialRequestOptions {
    return {
      ...opts,
      challenge: this.fromBase64Url(opts.challenge),
      allowCredentials: (opts.allowCredentials ?? []).map((c: any) => ({
        ...c,
        id: this.fromBase64Url(c.id)
      }))
    };
  }

  // ─── Base64Url ↔ ArrayBuffer ──────────────────────────────────────────────

  private fromBase64Url(base64url: string): ArrayBuffer {
    const padded = (base64url + '===').slice(0, base64url.length + (4 - base64url.length % 4) % 4);
    const binary = atob(padded.replace(/-/g, '+').replace(/_/g, '/'));
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes.buffer;
  }

  private toBase64Url(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    bytes.forEach(b => (binary += String.fromCharCode(b)));
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
  }
}
