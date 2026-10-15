<template>
  <VcBlade
    :title="$t('BACKUP.PAGES.RESTORE.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <!-- 1. choose the backup -->
      <VcForm
        v-if="!manifest && !running"
        class="backup-form tw-space-y-4"
      >
        <VcFileUpload
          :label="$t('BACKUP.PAGES.RESTORE.UPLOAD')"
          accept=".zip"
          :loading="uploadPercent !== undefined"
          @upload="upload"
        />
        <div
          v-if="uploadPercent !== undefined"
          class="backup-upload"
        >
          <span>{{ $t("BACKUP.PAGES.RESTORE.UPLOADING", { name: uploadName, percent: uploadPercent }) }}</span>
          <VcProgress :value="uploadPercent" />
          <div>
            <VcButton
              variant="secondary"
              size="sm"
              @click="uploadAbort?.abort()"
            >
              {{ $t("BACKUP.PAGES.RESTORE.CANCEL_UPLOAD") }}
            </VcButton>
          </div>
        </div>
        <VcCard :header="$t('BACKUP.PAGES.RESTORE.EXISTING')">
          <VcDataTable
            v-model:search-value="backupFilter"
            :items="filteredBackups"
            :searchable="true"
            state-key="BACKUP_RESTORE_FILES"
            :empty-state="{ icon: 'lucide-hard-drive', title: $t('BACKUP.PAGES.RESTORE.NO_BACKUPS') }"
            @row-click="(e: { data: BlobEntry }) => pick(e.data)"
          >
            <VcColumn
              id="name"
              :title="$t('BACKUP.PAGES.STORAGE.COLUMNS.NAME')"
              body-class="backup-main__title"
              always-visible
            />
            <VcColumn
              id="size"
              :title="$t('BACKUP.PAGES.STORAGE.COLUMNS.SIZE')"
              align="end"
            >
              <template #body="{ data: file }">{{ formatSize(file.size) }}</template>
            </VcColumn>
            <VcColumn
              id="modifiedDate"
              :title="$t('BACKUP.PAGES.STORAGE.COLUMNS.MODIFIED')"
              type="date-ago"
            />
          </VcDataTable>
        </VcCard>
      </VcForm>

      <!-- 2. what the backup contains, what to restore -->
      <VcForm
        v-else-if="manifest && !running"
        class="backup-form tw-space-y-4"
      >
        <VcBanner
          variant="warning"
          icon="lucide-triangle-alert"
        >
          {{ $t("BACKUP.PAGES.RESTORE.WARN") }}
        </VcBanner>
        <VcCard :header="$t('BACKUP.PAGES.RESTORE.INFO')">
          <dl class="backup-info">
            <dt>{{ $t("BACKUP.PAGES.RESTORE.FILE") }}</dt>
            <dd>{{ fileName }}</dd>
            <dt>{{ $t("BACKUP.PAGES.RESTORE.VERSION") }}</dt>
            <dd>{{ manifest.platformVersion }}</dd>
            <dt>{{ $t("BACKUP.PAGES.RESTORE.AUTHOR") }}</dt>
            <dd>{{ manifest.author }}</dd>
            <dt>{{ $t("BACKUP.PAGES.RESTORE.CREATED") }}</dt>
            <dd>{{ manifest.created ? new Date(manifest.created).toLocaleString() : "" }}</dd>
            <dt>{{ $t("BACKUP.PAGES.RESTORE.ENCRYPTED") }}</dt>
            <dd>{{ $t(manifest.isEncrypted ? "BACKUP.PAGES.RESTORE.YES" : "BACKUP.PAGES.RESTORE.NO") }}</dd>
          </dl>
        </VcCard>
        <VcInput
          v-if="manifest.isEncrypted"
          v-model="password"
          type="password"
          :label="$t('BACKUP.PAGES.RESTORE.PASSWORD')"
          :hint="$t('BACKUP.PAGES.RESTORE.PASSWORD_HINT')"
          :error="!!passwordError"
          :error-message="passwordError"
          required
        />
        <VcCard :header="$t('BACKUP.PAGES.RESTORE.CONTENT')">
          <div class="backup-options">
            <VcSwitch
              v-for="option in options"
              :key="option.key"
              v-model="request[option.key]"
              :disabled="!manifest[option.key]"
              :label="$t(`BACKUP.PAGES.BACKUP.OPTIONS.${option.label}`)"
              :hint="$t(`BACKUP.PAGES.BACKUP.OPTIONS.${option.label}_HINT`)"
            />
          </div>
        </VcCard>
        <VcCard :header="$t('BACKUP.PAGES.BACKUP.MODULES')">
          <VcDataTable
            v-model:selection="selectedModules"
            selection-mode="multiple"
            :items="modules"
            state-key="BACKUP_RESTORE_MODULES"
          >
            <VcColumn
              id="id"
              :title="$t('BACKUP.PAGES.BACKUP.COLUMNS.MODULE')"
              body-class="backup-main__title"
            />
            <VcColumn
              id="version"
              :title="$t('BACKUP.PAGES.BACKUP.COLUMNS.VERSION')"
            />
          </VcDataTable>
        </VcCard>
      </VcForm>

      <!-- 3. progress -->
      <template v-else-if="job">
        <JobProgress
          :job="job"
          :finished="finished"
          :percent="percent"
          prefix="BACKUP"
        />
        <OperationSteps
          :log="job.progressLog"
          verb="import"
          :finished="finished"
        />
      </template>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, usePermissions, usePopup, type IBladeToolbar } from "@vc-shell/framework";
import {
  VcBanner,
  VcBlade,
  VcButton,
  VcCard,
  VcColumn,
  VcContainer,
  VcDataTable,
  VcFileUpload,
  VcForm,
  VcInput,
  VcProgress,
  VcSwitch,
} from "@vc-shell/framework/ui";
import { computed, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import {
  backupApi,
  uploadBackup,
  type BackupNotification,
  type BlobEntry,
  type ExportModuleInfo,
  type PlatformExportManifest,
} from "../../api_client/backuprestore";
import JobProgress from "../components/JobProgress.vue";
import OperationSteps from "../components/OperationSteps.vue";
import { useJob } from "../composables/useJob";
import { errorText, formatSize, SECTIONS, type Section } from "../utils";

defineBlade({
  url: "/restore",
  name: "BackupRestoreRestore",
  permissions: ["platform:backuprestore:restore"],
});


const { t } = useI18n({ useScope: "global" });
const { options: bladeOptions } = useBlade<{ file?: BlobEntry }>();
const { showConfirmation, showError } = usePopup();
const { hasAccess } = usePermissions();
const { job, finished, percent, follow } = useJob<BackupNotification>();

const options = SECTIONS;

const loading = ref(false);
const backups = ref<BlobEntry[]>([]);
const backupFilter = ref<string>();
const filteredBackups = computed(() =>
  backups.value.map((x) => ({ ...x, id: x.url })).filter((x) => !backupFilter.value || x.name?.toLowerCase().includes(backupFilter.value.toLowerCase())),
);
const uploadPercent = ref<number>();
const uploadName = ref<string>();
let uploadAbort: AbortController | undefined;

const fileUrl = ref<string>();
const fileName = ref<string>();
const manifest = ref<PlatformExportManifest>();
const request = reactive<Record<Section, boolean>>({ handleSecurity: false, handleBinaryData: false, handleSettings: false, handleDynamicProperties: false });
const modules = computed(() => [...(manifest.value?.modules ?? [])].sort((a, b) => a.id.localeCompare(b.id)));
const selectedModules = ref<ExportModuleInfo[]>([]);
const password = ref("");
const passwordError = ref<string>();
const running = ref(false);

async function loadBackups() {
  try {
    backups.value = await backupApi.listBackups();
  } catch {
    backups.value = []; // non-fatal: uploading still works
  }
}

// both entry points (an upload, a file already in storage) end here: read what the backup contains, select it all
async function loadManifest(url: string, name?: string) {
  loading.value = true;
  try {
    const m = await backupApi.loadManifest(url);
    fileUrl.value = url;
    fileName.value = name;
    for (const o of options) request[o.key] = !!m[o.key];
    manifest.value = m;
    selectedModules.value = [...modules.value];
  } catch (e) {
    showError(`${name}: ${errorText(e)}`);
  } finally {
    loading.value = false;
  }
}

const pick = (file: BlobEntry) => loadManifest(file.relativeUrl ?? file.url!, file.name);

async function upload(files: FileList) {
  const file = files[0];
  if (!file?.name.toLowerCase().endsWith(".zip")) return showError(t("BACKUP.ALERTS.NOT_ZIP"));
  uploadName.value = file.name;
  uploadPercent.value = 0;
  uploadAbort = new AbortController();
  try {
    const [blob] = await uploadBackup(file, (p) => (uploadPercent.value = p), uploadAbort.signal);
    // both report their own errors
    await Promise.all([loadBackups(), loadManifest(blob.relativeUrl ?? blob.url!, blob.name)]);
  } catch (e) {
    // a cancel by the user returns quietly to the file choice (legacy)
    const reason = (e as { reason?: string }).reason;
    if (reason === "stalled") showError(t("BACKUP.ALERTS.UPLOAD_STALLED"));
    else if (reason === "noResponse") showError(t("BACKUP.ALERTS.UPLOAD_NO_RESPONSE"));
    else if (reason !== "cancelled") showError(errorText(e));
  } finally {
    uploadPercent.value = undefined;
    uploadAbort = undefined;
  }
}

function back() {
  manifest.value = undefined;
  fileUrl.value = fileName.value = undefined;
  password.value = "";
  passwordError.value = undefined;
  loadBackups();
}

function selectAll(on: boolean) {
  for (const o of options) request[o.key] = on && !!manifest.value?.[o.key];
  selectedModules.value = on ? [...modules.value] : [];
}

const anySelected = computed(() => selectedModules.value.length > 0 || options.some((o) => request[o.key]));
const canStart = computed(() => !!manifest.value && anySelected.value && (!manifest.value.isEncrypted || !!password.value) && !running.value);

async function start() {
  if (!(await showConfirmation(t("BACKUP.ALERTS.RESTORE_CONFIRMATION", { file: fileName.value })))) return;
  loading.value = true;
  passwordError.value = undefined;
  try {
    const notification = await backupApi.startRestore({
      ...request,
      exportManifest: manifest.value,
      modules: selectedModules.value.map((x) => x.id),
      fileUrl: fileUrl.value,
      password: password.value || undefined,
    });
    running.value = true;
    follow(notification);
  } catch (e) {
    showError(errorText(e));
  } finally {
    loading.value = false;
  }
}

// a wrong password fails the job before anything is restored: back to the form, as the AngularJS blade did
watch(finished, (done) => {
  const wrong = done && job.value?.errors?.find((e) => /invalid backup password/i.test(e));
  if (wrong) {
    running.value = false;
    password.value = "";
    passwordError.value = wrong;
  }
});

if (bladeOptions.value?.file) pick(bladeOptions.value.file);
else loadBackups();

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "back",
    title: t("BACKUP.PAGES.RESTORE.TOOLBAR.BACK"),
    icon: "lucide-chevron-left",
    isVisible: computed(() => !!manifest.value && !running.value),
    clickHandler: () => back(),
  },
  {
    id: "selectAll",
    title: t("BACKUP.PAGES.RESTORE.TOOLBAR.SELECT_ALL"),
    icon: "lucide-square-check",
    isVisible: computed(() => !!manifest.value && !running.value),
    clickHandler: () => selectAll(true),
  },
  {
    id: "unselectAll",
    title: t("BACKUP.PAGES.RESTORE.TOOLBAR.UNSELECT_ALL"),
    icon: "lucide-square",
    isVisible: computed(() => !!manifest.value && !running.value),
    disabled: computed(() => !anySelected.value),
    clickHandler: () => selectAll(false),
  },
  {
    id: "start",
    title: t("BACKUP.PAGES.RESTORE.TOOLBAR.START"),
    icon: "lucide-download",
    isVisible: computed(() => !!manifest.value && !running.value && hasAccess("platform:backuprestore:restore")),
    disabled: computed(() => !canStart.value),
    clickHandler: () => start(),
  },
  {
    id: "cancel",
    title: t("BACKUP.PAGES.RESTORE.TOOLBAR.CANCEL"),
    icon: "lucide-circle-x",
    isVisible: running,
    disabled: computed(() => finished.value || !job.value?.jobId),
    async clickHandler() {
      try {
        await backupApi.cancel(job.value!.jobId!);
      } catch (e) {
        showError(errorText(e));
      }
    },
  },
]);
</script>

<style lang="scss">
.backup-upload {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: var(--neutrals-600);
}
.backup-info {
  display: grid;
  grid-template-columns: max-content 1fr;
  gap: 8px 24px;
  margin: 0;
  padding: 16px;
  font-size: 14px;
  dt {
    color: var(--neutrals-500);
  }
  dd {
    margin: 0;
    color: var(--neutrals-800);
    word-break: break-all;
  }
}
</style>
