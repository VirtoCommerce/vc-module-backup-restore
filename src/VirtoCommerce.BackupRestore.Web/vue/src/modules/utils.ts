import { readableSize } from "@vc-shell/framework";

// The api client already puts the server's message into the Error
export const errorText = (e: unknown) => (e as Error)?.message ?? String(e);

export const formatSize = (bytes?: number) => (bytes == null ? "" : readableSize(bytes));

// A download through a temporary link. The platform returns its own urls relative to its root
// ("api/platform/export/download/..."), not to this app.
export function download(url: string, name = "") {
  const link = document.createElement("a");
  link.href = new URL(url, `${location.origin}/`).href;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
}

// The optional platform sections of a backup (its manifest says which it holds)
export const SECTIONS = [
  { key: "handleSecurity", label: "SECURITY" },
  { key: "handleBinaryData", label: "BINARY" },
  { key: "handleSettings", label: "SETTINGS" },
  { key: "handleDynamicProperties", label: "DYNAMIC" },
] as const;
export type Section = (typeof SECTIONS)[number]["key"];

// ProgressMessage.level arrives as a name from the REST API and as the enum number over SignalR
const LEVELS = ["Info", "Warning", "Debug", "Error"];
export const levelName = (level?: string | number) => (typeof level === "number" ? LEVELS[level] : level) ?? "Info";

export type Step = { name: string; status: "active" | "done" | "error"; messages: string[] };

// Per-module steps from the job's progress log ("Exporting 'X'" ... "Successfully exported 'X'"), as the AngularJS blade showed
export function parseSteps(log: { level?: string | number; message?: string }[] | undefined, verb: "export" | "import", finished: boolean): Step[] {
  const start = verb === "export" ? /^Exporting '(.+?)'/ : /^Importing '(.+?)'/;
  const success = verb === "export" ? /^Successfully exported '(.+?)'/ : /^Successfully imported '(.+?)'/;
  const failure = verb === "export" ? /^Failed to export '(.+?)'/ : /^Failed to import '(.+?)'/;
  const steps: Step[] = [];
  let current: Step | undefined;
  for (const entry of log ?? []) {
    const msg = entry.message ?? "";
    const started = start.exec(msg);
    if (started) {
      if (current?.status === "active") current.status = "done";
      current = { name: started[1], status: "active", messages: [] };
      steps.push(current);
    } else if (current && (levelName(entry.level) === "Error" || failure.test(msg))) {
      current.status = "error";
      current.messages.push(msg);
    } else if (current?.status === "active" && success.test(msg)) {
      current.status = "done";
    }
  }
  if (finished && current?.status === "active") current.status = "done";
  return steps;
}
