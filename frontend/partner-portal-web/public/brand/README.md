# Zahy brand assets (frontend)

Drop the **official** logo exports here. The app references these exact filenames;
placeholders are committed so the UI is never broken before the real art lands.
Replacing a file needs **no code change**.

| File | What it is | Used by |
| --- | --- | --- |
| `zahy-logo.svg` | Full horizontal wordmark lockup (preferred, scalable) | login screen, `BrandLogo variant="full"` |
| `zahy-logo.png` | PNG fallback of the full lockup (transparent bg, ~512px tall) | print / email / fallback |
| `zahy-icon.svg` | Square mark / icon only | sidebar, favicon, `BrandLogo variant="icon"` |
| `zahy-icon.png` | PNG fallback of the icon (transparent bg, 512×512) | favicon fallback |

## Visual identity (source: `01_الهوية_والعلامة_التجارية/Visual identity.pdf`)
- **Primary:** `#023b59` (deep navy-teal) · `#d5fde0` (mint) · `#ffffff`
- **Secondary:** `#76ca8c` · `#379024` (greens) · `#f5f5f5` (grey)
- **Typeface:** Cairo (UI/body). Logotype uses *Elegant-ar-bold* (keep inside the artwork).
- Respect the **clear/safety space** around the mark defined in the guide (page 9).

Export logos on a **transparent** background. Provide a light-on-dark variant too if the
mark is dark-only, so it stays legible on the navy header/dark theme.
