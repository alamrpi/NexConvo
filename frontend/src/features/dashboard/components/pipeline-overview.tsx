import { getTranslations } from 'next-intl/server';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card';
import { cn } from '@/shared/lib/cn';

type StageKey = 'new' | 'qualified' | 'proposal' | 'won';

export interface PipelineStage {
  key: StageKey;
  count: number;
}

const STAGE_BAR: Record<StageKey, string> = {
  new: 'bg-chart-1',
  qualified: 'bg-chart-2',
  proposal: 'bg-chart-3',
  won: 'bg-success',
};

/** Sales pipeline snapshot — presentational demo data (S7). Bars are token-colored (S25). */
export async function PipelineOverview({ stages }: { stages: readonly PipelineStage[] }) {
  const t = await getTranslations('dashboard');
  const max = Math.max(...stages.map((s) => s.count), 1);

  return (
    <Card className="flex h-full flex-col">
      <CardHeader className="pb-3">
        <CardTitle className="text-sm">{t('panels.pipeline')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3.5">
        {stages.map((stage) => {
          const pct = Math.round((stage.count / max) * 100);
          return (
            <div key={stage.key} className="space-y-1.5">
              <div className="flex items-center justify-between text-xs">
                <span className="font-medium">{t(`pipeline.${stage.key}`)}</span>
                <span className="text-muted-foreground">{stage.count}</span>
              </div>
              <div
                className="h-2 w-full overflow-hidden rounded-full bg-muted"
                role="progressbar"
                aria-valuenow={stage.count}
                aria-valuemin={0}
                aria-valuemax={max}
                aria-label={t(`pipeline.${stage.key}`)}
              >
                <div
                  className={cn('h-full rounded-full', STAGE_BAR[stage.key])}
                  style={{ width: `${pct}%` }}
                />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
