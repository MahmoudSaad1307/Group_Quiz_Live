class SoundEngine {
  private ctx: AudioContext | null = null;
  public enabled = false;

  unlock() {
    if (typeof window === "undefined") return;
    if (!this.ctx) {
      this.ctx = new AudioContext();
    }
    if (this.ctx.state === "suspended") {
      void this.ctx.resume();
    }
  }

  playTone(freq: number, durationSec: number, type: OscillatorType = "sine") {
    if (!this.enabled || !this.ctx) return;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = type;
    osc.frequency.value = freq;
    gain.gain.setValueAtTime(0.08, this.ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, this.ctx.currentTime + durationSec);
    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start();
    osc.stop(this.ctx.currentTime + durationSec);
  }

  playReveal() {
    this.playTone(523.25, 0.15, "triangle");
    setTimeout(() => this.playTone(659.25, 0.25, "triangle"), 100);
  }

  playFinish() {
    this.playTone(523.25, 0.15);
    setTimeout(() => this.playTone(659.25, 0.15), 120);
    setTimeout(() => this.playTone(783.99, 0.35), 240);
  }
}

export const soundEngine = new SoundEngine();
