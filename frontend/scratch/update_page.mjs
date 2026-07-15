import fs from 'fs';

const filePath = 'd:/Resources/Projects/NexConvo/frontend/src/app/(dashboard)/dashboard/chat/playground/page.tsx';
let content = fs.readFileSync(filePath, 'utf-8');

// 1. Add imports
content = content.replace(
  "import { useState, useRef, useEffect, useCallback } from 'react';",
  "import { useState, useRef, useEffect, useCallback } from 'react';\nimport { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';\nimport { apiClient } from '@/shared/api/client/api-client';"
);

// 2. Add Types
content = content.replace(
  "// ─── Types ────────────────────────────────────────────────────────────────────\n",
  `// ─── Types ────────────────────────────────────────────────────────────────────

export interface ProviderModelDto {
  providerId: string;
  providerName: string;
  models: ModelDto[];
}

export interface ModelDto {
  modelId: string;
  modelName: string;
}

`
);

// 3. Update ComparePaneConfig
content = content.replace(
  `function ComparePaneConfig({
  paneLabel,
  provider,
  setProvider,
  model,
  setModel,
}: {
  paneLabel: string;
  provider: string;
  setProvider: (v: string) => void;
  model: string;
  setModel: (v: string) => void;
}) {
  return (
    <div className="flex shrink-0 items-center gap-2 border-b border-border bg-muted/30 px-3 py-2">
      <span className="text-[0.625rem] font-semibold uppercase tracking-widest text-muted-foreground">
        {paneLabel}
      </span>
      <Select value={provider} onValueChange={setProvider}>
        <SelectTrigger className="h-7 w-32 text-xs" aria-label={\`\${paneLabel} provider\`}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="openrouter">OpenRouter</SelectItem>
          <SelectItem value="anthropic">Anthropic</SelectItem>
          <SelectItem value="openai">OpenAI</SelectItem>
          <SelectItem value="gemini">Google Gemini</SelectItem>
        </SelectContent>
      </Select>
      <Input
        className="h-7 w-40 text-xs"
        placeholder="Model"
        value={model}
        onChange={(e) => setModel(e.target.value)}
        aria-label={\`\${paneLabel} model name\`}
      />
    </div>
  );
}`,
  `function ComparePaneConfig({
  paneLabel,
  provider,
  setProvider,
  model,
  setModel,
  providers,
}: {
  paneLabel: string;
  provider: string;
  setProvider: (v: string) => void;
  model: string;
  setModel: (v: string) => void;
  providers: ProviderModelDto[];
}) {
  const currentProvider = providers.find((p) => p.providerId === provider);

  return (
    <div className="flex shrink-0 items-center gap-2 border-b border-border bg-muted/30 px-3 py-2">
      <span className="text-[0.625rem] font-semibold uppercase tracking-widest text-muted-foreground">
        {paneLabel}
      </span>
      <Select 
        value={provider} 
        onValueChange={(v) => {
          setProvider(v);
          const p = providers.find(x => x.providerId === v);
          if (p?.models?.length) setModel(p.models[0].modelId);
        }}
      >
        <SelectTrigger className="h-7 w-32 text-xs" aria-label={\`\${paneLabel} provider\`}>
          <SelectValue placeholder="Provider" />
        </SelectTrigger>
        <SelectContent>
          {providers.map((p) => (
            <SelectItem key={p.providerId} value={p.providerId}>{p.providerName}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={model} onValueChange={setModel}>
        <SelectTrigger className="h-7 w-48 text-xs" aria-label={\`\${paneLabel} model\`}>
          <SelectValue placeholder="Model" />
        </SelectTrigger>
        <SelectContent>
          {currentProvider?.models.map((m) => (
            <SelectItem key={m.modelId} value={m.modelId}>{m.modelName}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}`
);

// 4. In PlaygroundPage, add state and useEffect
content = content.replace(
  "export default function PlaygroundPage() {",
  `export default function PlaygroundPage() {
  const [providers, setProviders] = useState<ProviderModelDto[]>([]);
  const [connection, setConnection] = useState<HubConnection | null>(null);

  useEffect(() => {
    apiClient.get<ProviderModelDto[]>('/playground/models').then((res) => {
      setProviders(res.data);
    }).catch(console.error);

    // Setup SignalR Connection
    let hubConnection: HubConnection;
    fetch('/api/bff/auth/ws-ticket').then(res => res.json()).then(data => {
      if (!data.ticket) return;
      hubConnection = new HubConnectionBuilder()
        .withUrl('/api/bff/hubs/playground?access_token=' + data.ticket)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

      hubConnection.start().then(() => setConnection(hubConnection)).catch(console.error);
    });

    return () => {
      hubConnection?.stop();
    };
  }, []);`
);

// 5. Update ComparePaneConfig usages
content = content.replace(
  `                  <ComparePaneConfig
                    paneLabel="Pane A"
                    provider={leftProvider}
                    setProvider={setLeftProvider}
                    model={leftModel}
                    setModel={setLeftModel}
                  />`,
  `                  <ComparePaneConfig
                    paneLabel="Pane A"
                    provider={leftProvider}
                    setProvider={setLeftProvider}
                    model={leftModel}
                    setModel={setLeftModel}
                    providers={providers}
                  />`
);

content = content.replace(
  `                  <ComparePaneConfig
                    paneLabel="Pane B"
                    provider={rightProvider}
                    setProvider={setRightProvider}
                    model={rightModel}
                    setModel={setRightModel}
                  />`,
  `                  <ComparePaneConfig
                    paneLabel="Pane B"
                    provider={rightProvider}
                    setProvider={setRightProvider}
                    model={rightModel}
                    setModel={setRightModel}
                    providers={providers}
                  />`
);

content = content.replace(
  `              <ComparePaneConfig
                paneLabel="Model settings"
                provider={leftProvider}
                setProvider={setLeftProvider}
                model={leftModel}
                setModel={setLeftModel}
              />`,
  `              <ComparePaneConfig
                paneLabel="Model settings"
                provider={leftProvider}
                setProvider={setLeftProvider}
                model={leftModel}
                setModel={setLeftModel}
                providers={providers}
              />`
);


// write it back
fs.writeFileSync(filePath, content, 'utf-8');
console.log('Update script done.');
