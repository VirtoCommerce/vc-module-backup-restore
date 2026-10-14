// Hand-written client: the module's swagger document is empty (its controller sits under api/platform),
// so generate-api-client produces no endpoints. Same-origin cookie auth.
import type { JobNotification } from "../modules/composables/useJob";

export interface ExportModuleInfo {
  id: string;
  version?: string;
  isChecked?: boolean;
}

export interface PlatformExportManifest {
  author?: string;
  platformVersion?: string;
  created?: string;
  handleSecurity?: boolean;
  handleSettings?: boolean;
  handleBinaryData?: boolean;
  handleDynamicProperties?: boolean;
  isEncrypted?: boolean;
  modules?: ExportModuleInfo[];
}

export interface PlatformImportExportRequest {
  exportManifest?: PlatformExportManifest;
  modules?: string[];
  handleSecurity?: boolean;
  handleSettings?: boolean;
  handleBinaryData?: boolean;
  handleDynamicProperties?: boolean;
  passwordProtect?: boolean;
  fileUrl?: string;
  password?: string;
}

export interface ProgressMessage {
  level?: "Info" | "Warning" | "Debug" | "Error" | string | number;
  message?: string;
}

export type BackupNotification = JobNotification & { downloadUrl?: string; progressLog?: ProgressMessage[] };

export interface BlobEntry {
  type?: string;
  name?: string;
  url?: string;
  relativeUrl?: string;
  size?: number;
  modifiedDate?: string;
}

async function call<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: { Accept: "application/json", ...(body ? { "Content-Type": "application/json" } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await response.text();
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      message = JSON.parse(text).message ?? message;
    } catch {
      // not JSON
    }
    throw Object.assign(new Error(message), { status: response.status });
  }
  return (text ? JSON.parse(text) : undefined) as T;
}

export const BACKUPS_FOLDER = "backups";

export const backupApi = {
  newManifest: () => call<PlatformExportManifest>("GET", "/api/platform/export/manifest/new"),
  startBackup: (request: PlatformImportExportRequest) =>
    call<{ notification: BackupNotification; password?: string }>("POST", "/api/platform/export", request),
  loadManifest: (fileUrl: string) =>
    call<PlatformExportManifest>("GET", `/api/platform/export/manifest/load?fileUrl=${encodeURIComponent(fileUrl)}`),
  startRestore: (request: PlatformImportExportRequest) => call<BackupNotification>("POST", "/api/platform/import", request),
  cancel: (jobId: string) => call<void>("POST", `/api/platform/exortimport/tasks/${encodeURIComponent(jobId)}/cancel`),
  listBackups: async () => {
    const result = await call<{ results?: BlobEntry[] }>("GET", `/api/assets?folderUrl=${BACKUPS_FOLDER}`);
    return (result.results ?? [])
      .filter((x) => x.type === "blob" && x.name?.toLowerCase().endsWith(".zip"))
      .sort((a, b) => String(b.modifiedDate).localeCompare(String(a.modifiedDate)));
  },
  deleteBlobs: (urls: string[]) => call<void>("DELETE", `/api/assets?${urls.map((x) => `urls=${encodeURIComponent(x)}`).join("&")}`),
};

// Legacy watchdog: no upload activity for this long aborts the upload. Progress events keep resetting it while bytes
// move; they stop once the body is sent, so it also ends the "100% sent, no response" hang (e.g. Cloudflare's 100 MB
// cap). Above Cloudflare's 100 s origin timeout, so a slow but working server isn't cut off.
const UPLOAD_INACTIVITY_MS = 120000;

// Upload into the backups folder with progress (fetch can't report upload progress). Rejects with reason
// "stalled" / "noResponse" (watchdog) or "cancelled" (signal) besides the server's errors.
export function uploadBackup(file: File, onProgress: (percent: number) => void, signal?: AbortSignal): Promise<BlobEntry[]> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    let percent = 0;
    let watchdog: ReturnType<typeof setTimeout> | undefined;
    let reason = "cancelled";
    const arm = () => {
      clearTimeout(watchdog);
      watchdog = setTimeout(() => {
        reason = percent >= 100 ? "noResponse" : "stalled";
        xhr.abort();
      }, UPLOAD_INACTIVITY_MS);
    };
    xhr.open("POST", `/api/assets?folderUrl=${BACKUPS_FOLDER}`);
    xhr.setRequestHeader("Accept", "application/json");
    xhr.upload.onprogress = (e) => {
      arm();
      if (e.lengthComputable) onProgress((percent = Math.round((e.loaded / e.total) * 100)));
    };
    xhr.onloadend = () => clearTimeout(watchdog);
    xhr.onload = () => (xhr.status < 300 ? resolve(JSON.parse(xhr.responseText)) : reject(Object.assign(new Error(`${xhr.status} ${xhr.responseText}`), { status: xhr.status })));
    xhr.onerror = () => reject(new Error("Network error"));
    xhr.onabort = () => reject(Object.assign(new Error(`Upload ${reason}`), { reason }));
    signal?.addEventListener("abort", () => xhr.abort());
    const body = new FormData();
    body.append("file", file, file.name);
    xhr.send(body);
    arm();
  });
}
