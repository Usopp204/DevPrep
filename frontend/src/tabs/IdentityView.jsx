import React from "react";
import { MdGraphicEq } from "react-icons/md";

function IdentityView() {
  return (
    <div className="tab-view-container">
      <div className="view-title">
        <h1>Identity Verification</h1>
        <p>Complete your multi-modal biometric check to finalize your elite developer tier status and unlock restricted career pathways.</p>
      </div>

      <div className="verification-status-banner">
        <div>
          <h3>Verification Status</h3>
          <p>ID: DEV-4492-X</p>
        </div>
        <div className="status-pill-group">
          <span className="pill-success">✓ FACE VERIFIED</span>
          <span className="pill-warning">💬 VOICE PENDING</span>
        </div>
      </div>

      <div className="biometric-grid">
        {/* Face Recognition Box */}
        <div className="biometric-box">
          <div className="box-header">
            <h3>Face Recognition</h3>
            <span className="live-indicator">● HD STREAM ACTIVE</span>
          </div>
          <div className="camera-viewport-mock">
            <div className="scan-corners"></div>
            <img src="https://i.imgur.com/vHdfy3g.png" alt="Face Scanning Mock" className="face-scan-graphic" />
            <div className="scan-stats">
              <p>ALIGNMENT: 98.4%</p>
              <p>DEPTH: LOCKED</p>
            </div>
          </div>
          <p className="hint-text">Look directly into the lens and keep your head still within the frame.</p>
          <button className="btn-primary-action full-width">RE-SCAN FACE</button>
        </div>

        {/* Voice Authentication Box */}
        <div className="biometric-box">
          <div className="box-header">
            <h3>Voice Authentication</h3>
            <span className="step-counter">STEP 2 OF 2</span>
          </div>
          <div className="voice-prompt-card">
            <span>READ THIS PHRASE ALOUD:</span>
            <p className="phrase-text">"The quantum compiler optimizes high-performance architecture for futuristic intelligence."</p>
          </div>
          <div className="audio-wave-visualizer">
            <div className="wave-bars">
              <span></span><span></span><span></span><span></span><span></span><span></span>
            </div>
            <span className="wave-buffer-text">VOICE_WAVE_BUFFER: ANALYZING...</span>
          </div>
          <div className="voice-actions-row">
            <button className="btn-cyan"><MdGraphicEq /> Start Recording</button>
            <button className="btn-disabled" disabled>✓ Done</button>
          </div>
        </div>
      </div>

      {/* Terminal Security Logs */}
      <div className="security-terminal">
        <div className="terminal-header">SECURITY LOGIC LOGS</div>
        <div className="terminal-body">
          <p><span className="time">[14:02:22]</span> SYNC_INIT: Biometric handshake established with edge-node-04.</p>
          <p><span className="time">[14:02:23]</span> FACE_ENGINE: Landmarks detected. Mesh density: 1024. Entropy: Low.</p>
          <p><span className="time">[14:02:25]</span> VOICE_ENGINE: Noise floor calibrated at -42dB. Waiting for speech input.</p>
          <p><span className="system">[SYSTEM]</span> Please maintain clear pronunciation for high-fidelity spectral matching.</p>
        </div>
      </div>
    </div>
  );
}
export default IdentityView;