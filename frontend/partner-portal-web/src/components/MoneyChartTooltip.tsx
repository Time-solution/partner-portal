import { MoneyAmount } from "@/components/MoneyAmount";

type TooltipPayload = {
  name?: string;
  value?: number | string;
  color?: string;
};

export type MoneyChartTooltipProps = {
  active?: boolean;
  payload?: TooltipPayload[];
  label?: string | number;
  isMoneySeries?: (seriesName: string) => boolean;
};

/** Recharts tooltip body — money series use {@link MoneyAmount}, counts stay plain. */
export function MoneyChartTooltipContent({
  active,
  payload,
  label,
  isMoneySeries = () => true,
}: MoneyChartTooltipProps) {
  if (!active || !payload?.length) return null;

  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2 text-popover-foreground shadow-md">
      {label ? <p className="mb-1 text-sm font-semibold">{label}</p> : null}
      <ul className="space-y-1 text-sm">
        {payload.map((entry) => {
          const name = String(entry.name ?? "");
          const money = isMoneySeries(name);
          return (
            <li key={name} className="flex items-center gap-2">
              <span
                className="inline-block h-2.5 w-2.5 shrink-0 rounded-full"
                style={{ background: entry.color }}
                aria-hidden="true"
              />
              <span className="text-muted-foreground">{name}</span>
              <span className="ms-auto font-medium">
                {money ? (
                  <MoneyAmount amount={Number(entry.value ?? 0)} />
                ) : (
                  String(Number(entry.value ?? 0))
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
