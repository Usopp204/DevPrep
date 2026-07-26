# DevPrep AI — DESIGN.md

## Brand

**Product name:** DevPrep AI  
**Tagline:** Master Technical Interviews with AI  
**Audience:** Software engineers in Vietnam preparing for technical interviews at top-tier companies.  
**Tone:** Professional, precise, trustworthy, slightly futuristic. Think Bloomberg terminal meets modern SaaS.  
**Dark mode only.**

---

## Color Tokens

```
--color-background:           #0e1323
--color-surface:              #0e1323
--color-surface-container-low: #161b2b
--color-surface-container:    #1a1f30
--color-surface-container-high: #25293a
--color-surface-container-highest: #2f3446
--color-surface-bright:       #34394a

--color-primary:              #c3c0ff
--color-primary-container:    #4f46e5
--color-on-primary:           #1d00a5
--color-on-primary-container: #dad7ff

--color-secondary:            #4cd7f6
--color-secondary-container:  #03b5d3
--color-on-secondary:         #003640

--color-tertiary:             #d0bcff
--color-tertiary-container:   #6f3dd9
--color-on-tertiary:          #3c0091

--color-on-surface:           #dee1f9
--color-on-surface-variant:   #c7c4d8

--color-success:              #10B981
--color-warning:              #F9D949
--color-error:                #ffb4ab
--color-error-container:      #93000a

--color-border-subtle:        rgba(255, 255, 255, 0.08)
--color-glass-fill:           rgba(17, 24, 39, 0.6)
--color-outline:              #918fa1
--color-outline-variant:      #464555
```

---

## Typography

**Display face:** Inter  
**Body face:** Inter  
**Code / Label face:** JetBrains Mono

| Role         | Family         | Size  | Weight | Line Height | Letter Spacing |
|--------------|----------------|-------|--------|-------------|----------------|
| display-lg   | Inter          | 48px  | 700    | 56px        | -0.02em        |
| headline-lg  | Inter          | 32px  | 600    | 40px        | -0.01em        |
| headline-md  | Inter          | 24px  | 600    | 32px        | 0              |
| body-md      | Inter          | 16px  | 400    | 24px        | 0              |
| body-sm      | Inter          | 14px  | 400    | 20px        | 0              |
| code-label   | JetBrains Mono | 13px  | 500    | 16px        | 0.05em (wide)  |

All code-label text: UPPERCASE.

---

## Spacing Scale

Base unit: 4px

| Token   | Value |
|---------|-------|
| unit    | 4px   |
| xs      | 8px   |
| sm      | 12px  |
| md      | 16px  |
| lg      | 24px  |
| xl      | 32px  |
| 2xl     | 48px  |
| 3xl     | 64px  |
| gutter  | 24px  |

Max content width: **1200px**, centered, horizontal padding 24px.

---

## Border Radius

| Token   | Value  |
|---------|--------|
| sm      | 4px    |
| DEFAULT | 4px    |
| lg      | 8px    |
| xl      | 12px   |
| 2xl     | 16px   |
| 3xl     | 24px   |
| full    | 9999px |

---

## Elevation / Shadow

```
shadow-glow-primary:   0 0 20px 2px rgba(79, 70, 229, 0.15)
shadow-glow-secondary: 0 0 20px 2px rgba(76, 215, 246, 0.10)
shadow-glow-success:   0 0 40px -10px rgba(16, 185, 129, 0.30)
shadow-glow-error:     0 0 40px -10px rgba(255, 180, 171, 0.15)
shadow-inner-button:   inset 0 1px 0 0 rgba(255, 255, 255, 0.20)
```

---

## Component Specifications

### Glass Card
```css
background: rgba(17, 24, 39, 0.6);
backdrop-filter: blur(12px);
border-top: 1px solid rgba(255, 255, 255, 0.08);
border-left: 1px solid rgba(255, 255, 255, 0.08);
border-bottom: 0.5px solid rgba(255, 255, 255, 0.04);
border-right: 0.5px solid rgba(255, 255, 255, 0.04);
border-radius: 12px;
```

### Primary Button
```css
background: linear-gradient(135deg, #4f46e5 0%, #7c3aed 100%);
box-shadow: inset 0 1px 0 rgba(255,255,255,0.20);
color: white;
font-weight: 700;
border-radius: 12px;
padding: 12px 24px;
transition: opacity 0.2s, transform 0.1s;
hover: opacity 0.9;
active: scale(0.98);
```

### Ghost Button
```css
background: transparent;
border: 1px solid rgba(255, 255, 255, 0.08);
color: #dee1f9;
border-radius: 12px;
padding: 12px 24px;
hover: background rgba(255,255,255,0.05);
```

### Input Field
```css
background: #1a1f30;
border: 1px solid rgba(255, 255, 255, 0.08);
border-radius: 8px;
color: #dee1f9;
padding: 12px 16px;
font-size: 14px;
focus: border-color rgba(195,192,255,0.4); box-shadow 0 0 0 4px rgba(195,192,255,0.10);
```

### Badge / Chip
```css
/* Primary tint */
background: rgba(195, 192, 255, 0.10);
border: 1px solid rgba(195, 192, 255, 0.20);
color: #c3c0ff;
border-radius: 9999px;
padding: 4px 12px;
font-family: JetBrains Mono;
font-size: 11px;
font-weight: 500;
text-transform: uppercase;
letter-spacing: 0.05em;

/* Secondary tint — swap color values for #4cd7f6 */
/* Success — swap for #10B981 */
/* Error — swap for #ffb4ab */
```

### Navigation Bar (Top, fixed)
```
height: 64px
background: rgba(17, 24, 39, 0.6)
backdrop-filter: blur(20px)
border-bottom: 1px solid rgba(255,255,255,0.08)
z-index: 50
```

### Sidebar (Dashboard)
```
width: 256px
background: #161b2b
border-right: 1px solid rgba(255,255,255,0.08)
fixed, full height
top: 64px (below navbar)

Nav item active state:
  background: #4f46e5
  color: #dad7ff
  font-weight: 700
  border-radius: 8px

Nav item hover:
  background: rgba(255,255,255,0.04)
```

### Status Chips Reference
```
Active / Verified:  bg #10B981/10, text #10B981, border #10B981/20
Pending:            bg #F9D949/10, text #F9D949, border #F9D949/20
Failed / Blocked:   bg #ffb4ab/10, text #ffb4ab, border #ffb4ab/20
Draft:              bg #2f3446,    text #c7c4d8, border transparent
```

---

## Ambient / Motion

- **Background blobs:** absolute div, width/height 384px, border-radius full, blur 120px, pointer-events none. Use primary/10 or secondary/10 opacity. Place 1–2 per page, off-screen edges.
- **Scan line animation:** `height: 2px; background: linear-gradient(90deg, transparent, #4cd7f6, transparent); box-shadow: 0 0 15px #4cd7f6;` animate top 0%→100% over 3s loop. Use on QR and loading screens only.
- **Waveform bars:** 8–12 divs, `width: 4px; background: #4cd7f6; border-radius: 2px;` randomize height every 150ms via JS. Use only when voice is active.
- **Particle effect:** 30 colored dots (green/cyan/lavender), fade + float up on page load. Use only on Payment Success page.
- **Pulsing dot:** `animation: pulse 2s ease-in-out infinite; scale 1→1.2, opacity 1→0.5`. Use for live status indicators.
- Respect `prefers-reduced-motion`: disable all animations.

---

## Page Layout Patterns

### Centered Auth Layout (Login, Register, Forgot Password)
```
Full viewport height flex center
Single card max-width: 480px
Background: #0e1323 + 2 ambient blobs
Card: glass-card, padding 32px
```

### Dashboard Layout
```
Fixed navbar (64px)
Fixed sidebar (256px, top 64px)
Main content: margin-left 256px, padding-top 64px, padding 24px
Max content width: 1200px
```

### Focused Checkout Layout (Payment pages)
```
Suppressed navbar: logo + status text only, no nav links
No sidebar
Centered card max-width: 520px
Ambient blobs background
```

### Full-screen Immersive Layout (Interview Room)
```
No navbar, no footer
3-column flex: sidebar (288px) | main (flex-1) | right panel (320px)
overflow: hidden (no page scroll)
Fixed bottom action bar (64px)
```

---

## Icons

Library: **Material Symbols Outlined**  
Default variation: `font-variation-settings: 'FILL' 0, 'wght' 400, 'GRAD' 0, 'opsz' 24`  
Filled variant (for status/emphasis): `'FILL' 1`

Key icons used:
- bolt, check_circle, warning, verified, verified_user, shield_lock, lock
- mic, videocam, face, keyboard_voice, camera_enhance
- psychology, smart_toy, forum, code, insights, map, description
- payments, receipt_long, account_balance_wallet
- arrow_forward, arrow_back, rocket_launch, add, close, refresh
- dashboard, analytics, settings, menu, expand_more

---

## Dark Mode

**Dark mode only.** Do not implement light mode. All color tokens are dark-mode values.

