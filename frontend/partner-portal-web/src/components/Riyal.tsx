import { RIYAL_SVG_PATHS, RIYAL_VIEW_BOX } from "./riyalSymbol";

/** Inline Saudi Riyal currency mark — inherits `currentColor`, scales with text (`1em`). */
export function Riyal({ className }: { className?: string }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox={RIYAL_VIEW_BOX}
      role="img"
      aria-label="ريال"
      fill="currentColor"
      className={className}
      style={{ height: "1em", width: "auto", verticalAlign: "baseline" }}
    >
      {RIYAL_SVG_PATHS.map((d) => (
        <path key={d.slice(0, 24)} d={d} />
      ))}
    </svg>
  );
}
