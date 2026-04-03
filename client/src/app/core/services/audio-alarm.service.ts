import { Injectable } from '@angular/core';

const MUTE_KEY = 'divvy-alarm-muted';

export type AlarmSound = 'chime' | 'pulse' | 'ding';

@Injectable({
  providedIn: 'root'
})
export class AudioAlarmService {
  private audioCtx: AudioContext | null = null;

  /**
   * Call this on a direct user-gesture (e.g. button click) to unlock the
   * AudioContext on iOS Safari.  Plays a silent 1-sample buffer so the OS
   * marks the context as permitted for programmatic playback.
   */
  unlock(ctx: AudioContext): void {
    this.audioCtx = ctx;
    try {
      const buf = ctx.createBuffer(1, 1, 22050);
      const src = ctx.createBufferSource();
      src.buffer = buf;
      src.connect(ctx.destination);
      src.start(0);
    } catch {
      // Silently ignore — unlock is best-effort
    }
  }

  /**
   * Play an alarm sound.
   *
   * - 'chime'  — rising three-note chime (C5→E5→G5). Default for BGL hypo timer.
   * - 'pulse'  — four ascending urgent pulses that speed up. Default for BGL ketone timer.
   * - 'ding'   — soft double ding with exponential decay. Default for BP rest timer.
   */
  async playAlarm(sound: AlarmSound = 'chime'): Promise<void> {
    if (this.isMuted()) return;
    const ctx = this.audioCtx;
    if (!ctx) return;

    try {
      if (ctx.state === 'suspended') {
        await ctx.resume();
      }

      switch (sound) {
        case 'chime':
          this.playChime(ctx);
          break;
        case 'pulse':
          this.playPulse(ctx);
          break;
        case 'ding':
          this.playDing(ctx);
          break;
      }
    } catch {
      // Silently ignore — audio is a best-effort enhancement
    }
  }

  /**
   * Rising three-note chime: C5 (523 Hz) → E5 (659 Hz) → G5 (784 Hz).
   * Gentle and positive — "time to act" without panic.
   * Used for: BGL hypo recheck timer.
   */
  private playChime(ctx: AudioContext): void {
    const notes = [523, 659, 784];
    const noteDuration = 0.18;
    const noteGap = 0.06;

    notes.forEach((freq, i) => {
      const startAt = ctx.currentTime + i * (noteDuration + noteGap);

      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, startAt);

      gain.gain.setValueAtTime(0, startAt);
      gain.gain.linearRampToValueAtTime(0.55, startAt + 0.01);
      gain.gain.setValueAtTime(0.55, startAt + noteDuration - 0.05);
      gain.gain.linearRampToValueAtTime(0, startAt + noteDuration);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(startAt);
      osc.stop(startAt + noteDuration);
    });
  }

  /**
   * Four ascending urgent pulses that speed up and rise in pitch.
   * Communicates escalating urgency — like a medical monitor alert.
   * Used for: BGL ketone 2-hour monitoring timer.
   */
  private playPulse(ctx: AudioContext): void {
    const pitches  = [440, 520, 600, 700];
    const gaps     = [0.30, 0.25, 0.20, 0.15]; // shrinking gap = speeds up
    const duration = 0.12;

    let cursor = ctx.currentTime;
    pitches.forEach((freq, i) => {
      const startAt = cursor;
      cursor += duration + gaps[i];

      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, startAt);

      gain.gain.setValueAtTime(0, startAt);
      gain.gain.linearRampToValueAtTime(0.6, startAt + 0.01);
      gain.gain.setValueAtTime(0.6, startAt + duration - 0.02);
      gain.gain.linearRampToValueAtTime(0, startAt + duration);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(startAt);
      osc.stop(startAt + duration);
    });
  }

  /**
   * Soft double ding: 440 Hz then 350 Hz, each with a natural exponential decay.
   * Non-alarming — "time to take your reading".
   * Used for: BP rest timer.
   */
  private playDing(ctx: AudioContext): void {
    const tones = [440, 350];
    const spacing = 0.35;

    tones.forEach((freq, i) => {
      const startAt = ctx.currentTime + i * spacing;
      const decayEnd = startAt + 0.6;

      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'triangle';
      osc.frequency.setValueAtTime(freq, startAt);

      gain.gain.setValueAtTime(0.55, startAt);
      gain.gain.exponentialRampToValueAtTime(0.001, decayEnd);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(startAt);
      osc.stop(decayEnd);
    });
  }

  isMuted(): boolean {
    try {
      return localStorage.getItem(MUTE_KEY) === 'true';
    } catch {
      return false;
    }
  }

  setMuted(val: boolean): void {
    try {
      localStorage.setItem(MUTE_KEY, String(val));
    } catch {
      // Silently ignore storage errors
    }
  }
}
