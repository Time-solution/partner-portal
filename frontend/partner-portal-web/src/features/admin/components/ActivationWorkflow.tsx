import { Check, Circle, Clock } from "lucide-react";
import type { ActivationWorkflowStage, ActivationWorkflowState } from "@/lib/data/types";
import type { Lang } from "@/lib/i18n";
import { useTranslator } from "@/lib/i18n";

const STAGES: ActivationWorkflowStage[] = [
  "Pending",
  "PsmRequested",
  "AccountantTermsSet",
  "AdminApproved",
  "Active",
];

const stageIndex = (stage: ActivationWorkflowStage): number => {
  if (stage === "Suspended" || stage === "Ended") return STAGES.length;
  const idx = STAGES.indexOf(stage);
  return idx >= 0 ? idx : 0;
};

interface ActivationWorkflowProps {
  workflow: ActivationWorkflowState;
  lang: Lang;
}

export function ActivationWorkflow({ workflow, lang }: ActivationWorkflowProps) {
  const t = useTranslator(lang);
  const current = stageIndex(workflow.stage);
  const isTerminal = workflow.stage === "Suspended" || workflow.stage === "Ended";

  const actorForStage = (stage: ActivationWorkflowStage): string | undefined => {
    switch (stage) {
      case "PsmRequested":
        return workflow.psmRequestedBy;
      case "AccountantTermsSet":
        return workflow.accountantTermsBy;
      case "AdminApproved":
      case "Active":
        return workflow.adminApprovedBy;
      default:
        return undefined;
    }
  };

  return (
    <div className="space-y-3">
      <ol className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-start sm:gap-0">
        {STAGES.map((stage, i) => {
          const done = i < current || (i === current && workflow.stage === "Active");
          const active = i === current && !isTerminal;
          const Icon = done ? Check : active ? Clock : Circle;
          const actor = actorForStage(stage);

          return (
            <li
              key={stage}
              className="flex min-w-0 flex-1 flex-col items-start gap-1 sm:px-2 sm:first:ps-0 sm:last:pe-0"
            >
              <div className="flex items-center gap-2.5">
                <Icon
                  className={[
                    "h-5 w-5 shrink-0",
                    done ? "text-emerald-600" : active ? "text-primary" : "text-muted-foreground",
                  ].join(" ")}
                  aria-hidden="true"
                />
                <span
                  className={[
                    "text-sm font-medium",
                    done || active ? "text-foreground" : "text-muted-foreground",
                  ].join(" ")}
                >
                  {t(`workflow_${stage}` as never)}
                </span>
              </div>
              {actor ? <p className="ps-7 text-xs text-muted-foreground">{actor}</p> : null}
            </li>
          );
        })}
      </ol>

      {isTerminal ? (
        <p className="rounded-md bg-muted px-3 py-2 text-sm text-muted-foreground">
          {t(`workflow_${workflow.stage}` as never)}
        </p>
      ) : null}

      {workflow.notificationSent && workflow.stage === "Active" ? (
        <p className="rounded-md border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-800 dark:text-emerald-200">
          {t("activationNotifySent" as never)}
        </p>
      ) : null}
    </div>
  );
}
