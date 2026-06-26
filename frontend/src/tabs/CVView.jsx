import React from "react";
import { MdInsertDriveFile, MdCloudUpload, MdSearch, MdZoomIn, MdZoomOut, MdDownload } from "react-icons/md";

function CVView() {
  return (
    <div className="tab-view-container">
      <div className="view-title">
        <h1>My CV</h1>
        <p>Manage your professional credentials and optimize for AI screenings.</p>
      </div>

      <div className="cv-grid">
        <div className="cv-left-panel">
          {/* Active CV Status */}
          <div className="uploaded-cv-card">
            <div className="pdf-icon-wrapper"><MdInsertDriveFile size={28} /></div>
            <div className="cv-meta">
              <h4>resume_alex.pdf</h4>
              <p>Uploaded Oct 24, 2023 • 1.2 MB</p>
              <div className="cv-status-badges">
                <span className="badge-verified">✓ VERIFIED</span>
                <span className="badge-match">AI Match: 94%</span>
              </div>
            </div>
            <div className="cv-card-actions">
              <button className="btn-secondary">View CV</button>
              <button className="btn-secondary">Edit</button>
            </div>
          </div>

          {/* Upload Area */}
          <div className="upload-dropzone">
            <div className="upload-icon-circle"><MdCloudUpload size={24} /></div>
            <h3>Upload New CV</h3>
            <p>Drag and drop your resume or click to browse files. Supported: PDF, DOCX (Max 10MB)</p>
            <button className="select-file-txt">Select File →</button>
          </div>

          {/* Extracted Skills */}
          <div className="skills-card">
            <h3><MdSearch /> Extracted Skills</h3>
            <button className="rescan-btn">RESCAN CV</button>
            <div className="skills-tags">
              <span className="skill-tag">Java</span>
              <span className="skill-tag">Spring Boot</span>
              <span className="skill-tag">Docker</span>
              <span className="skill-tag">REST API</span>
              <span className="skill-tag">SQL</span>
              <span className="skill-tag">Git</span>
              <button className="add-skill-tag">+ Add Skill</button>
            </div>
          </div>
        </div>

        {/* Interactive CV Preview */}
        <div className="cv-right-preview">
          <div className="preview-top-bar">
            <span>INTERACTIVE PREVIEW</span>
            <div className="window-dots"><span></span><span></span><span></span></div>
          </div>
          <div className="preview-mock-doc">
            <div className="line title"></div>
            <div className="line subtitle"></div>
            <div className="line paragraph long"></div>
            <div className="line paragraph text"></div>
            <div className="preview-split-box">
              <div className="mock-box"></div>
              <div className="mock-box"></div>
            </div>
            <div className="line paragraph long"></div>
          </div>
          <div className="preview-toolbar">
            <button><MdZoomIn /></button>
            <button><MdZoomOut /></button>
            <button><MdDownload /></button>
          </div>
        </div>
      </div>
    </div>
  );
}
export default CVView;