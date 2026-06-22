import { useTranslator, type Lang } from "@/lib/i18n";
import {
  ALL_DATES,
  currentDay,
  currentPeriod,
  defaultRangeBounds,
  type DateRange,
  type DateRangeMode,
} from "@/lib/filters/dateRange";

const INPUT = "h-9 rounded-md border border-input bg-background px-3 text-sm";

/**
 * Shared date / day-range control. Drives the single {@link DateRange} state (via `useDateRange`)
 * that every relevant tab filters by. Bilingual + RTL-aware. Display/filter only — no money math.
 */
export function DateRangeFilter({
  range,
  onChange,
  lang,
  className,
}: {
  range: DateRange;
  onChange: (next: DateRange) => void;
  lang: Lang;
  className?: string;
}) {
  const t = useTranslator(lang);

  const setMode = (mode: DateRangeMode) => {
    if (mode === "all") onChange(ALL_DATES);
    else if (mode === "period") onChange({ mode: "period", period: range.period ?? currentPeriod() });
    else if (mode === "day") onChange({ mode: "day", day: range.day ?? currentDay() });
    else {
      const d = defaultRangeBounds();
      onChange({ mode: "range", from: range.from ?? d.from, to: range.to ?? d.to });
    }
  };

  return (
    <div
      dir={lang === "ar" ? "rtl" : "ltr"}
      data-testid="date-range-filter"
      className={["flex flex-wrap items-end gap-2", className].filter(Boolean).join(" ")}
    >
      <label className="flex flex-col gap-1 text-sm">
        <span className="font-medium text-muted-foreground">{t("dateFilterLabel" as never)}</span>
        <select
          className={INPUT}
          value={range.mode}
          onChange={(e) => setMode(e.target.value as DateRangeMode)}
          aria-label={t("dateFilterLabel" as never)}
        >
          <option value="all">{t("dateFilterAll" as never)}</option>
          <option value="period">{t("dateFilterMonth" as never)}</option>
          <option value="day">{t("dateFilterDay" as never)}</option>
          <option value="range">{t("dateFilterRange" as never)}</option>
        </select>
      </label>

      {range.mode === "period" ? (
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-muted-foreground">{t("dateFilterMonth" as never)}</span>
          <input
            type="month"
            className={INPUT}
            value={range.period ?? ""}
            onChange={(e) => onChange({ mode: "period", period: e.target.value })}
          />
        </label>
      ) : null}

      {range.mode === "day" ? (
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-muted-foreground">{t("dateFilterDay" as never)}</span>
          <input
            type="date"
            className={INPUT}
            value={range.day ?? ""}
            onChange={(e) => onChange({ mode: "day", day: e.target.value })}
          />
        </label>
      ) : null}

      {range.mode === "range" ? (
        <>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-muted-foreground">{t("dateFilterFrom" as never)}</span>
            <input
              type="date"
              className={INPUT}
              value={range.from ?? ""}
              onChange={(e) => onChange({ ...range, mode: "range", from: e.target.value || undefined })}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-muted-foreground">{t("dateFilterTo" as never)}</span>
            <input
              type="date"
              className={INPUT}
              value={range.to ?? ""}
              onChange={(e) => onChange({ ...range, mode: "range", to: e.target.value || undefined })}
            />
          </label>
        </>
      ) : null}
    </div>
  );
}
