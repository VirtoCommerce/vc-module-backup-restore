<template>
  <VcBlade
    :title="$t('BACKUP.PAGES.BACKUP.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcContainer>
      <!-- options -->
      <VcForm
        v-if="!job"
        class="backup-form tw-space-y-4"
      >
        <VcBanner
          v-if="request.handleSecurity"
          variant="warning"
          icon="lucide-shield-alert"
        >
          {{ $t("BACKUP.PAGES.BACKUP.WARN_SECURITY") }}
        </VcBanner>
        <VcBanner
          v-if="!request.passwordProtect"
          variant="danger"
          icon="lucide-lock-open"
        >
          {{ $t("BACKUP.PAGES.BACKUP.WARN_NO_PASSWORD") }}
        </VcBanner>
        <VcCard :header="$t('BACKUP.PAGES.BACKUP.PLATFORM')">
          <div class="backup-options">
            <VcSwitch
              v-for="option in options"
              :key="option.key"
              v-model="request[option.key]"
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
            state-key="BACKUP_MODULES"
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

      <!-- progress -->
      <template v-else>
        <JobProgress
          :job="job"
          :finished="finished"
          :percent="percent"
          prefix="BACKUP"
        >
          <template v-if="finished && !job.errors?.length">
            <div
              v-if="password"
              class="backup-password tw-space-y-2"
            >
              <VcField
                :label="$t('BACKUP.PAGES.BACKUP.PASSWORD')"
                :model-value="password"
                copyable
              />
              <VcBanner
                variant="warning"
                icon="lucide-key-round"
              >
                {{ $t("BACKUP.PAGES.BACKUP.PASSWORD_NOTE") }}
              </VcBanner>
            </div>
            <div v-if="job.downloadUrl">
              <VcButton
                icon="lucide-download"
                @click="download(job.downloadUrl)"
              >
                {{ $t("BACKUP.PAGES.BACKUP.DOWNLOAD") }}
              </VcButton>
            </div>
          </template>
        </JobProgress>
        <OperationSteps
          :log="job.progressLog"
          verb="export"
          :finished="finished"
        />
      </template>
    </VcContainer>
  </VcBlade>
</template>

<script setup lang="ts">
import { usePopup, type IBladeToolbar } from "@vc-shell/framework";
import { VcBanner, VcBlade, VcButton, VcCard, VcColumn, VcContainer, VcDataTable, VcField, VcForm, VcSwitch } from "@vc-shell/framework/ui";
import { computed, onBeforeUnmount, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { backupApi, type BackupNotification, type ExportModuleInfo, type PlatformExportManifest } from "../../api_client/backuprestore";
import JobProgress from "../components/JobProgress.vue";
import OperationSteps from "../components/OperationSteps.vue";
import { useJob } from "../composables/useJob";
import { download, errorText, SECTIONS, type Section } from "../utils";

defineBlade({
  url: "/backup",
  name: "BackupRestoreBackup",
  permissions: ["platform:backuprestore:backup"],
});

type Flag = "passwordProtect" | Section;

const { t } = useI18n({ useScope: "global" });
const { showError } = usePopup();
const { job, finished, percent, follow } = useJob<BackupNotification>();

const options: { key: Flag; label: string }[] = [{ key: "passwordProtect", label: "PASSWORD" }, ...SECTIONS];

// password protection is on every time the blade opens (never remembered), as in the AngularJS blade
const request = reactive<Record<Flag, boolean>>({
  passwordProtect: true,
  handleSecurity: false,
  handleBinaryData: false,
  handleSettings: false,
  handleDynamicProperties: false,
});
const manifest = ref<PlatformExportManifest>();
const modules = computed(() => [...(manifest.value?.modules ?? [])].sort((a, b) => a.id.localeCompare(b.id)));
const selectedModules = ref<ExportModuleInfo[]>([]);
const password = ref<string>();
const loading = ref(true);

// shown once, on the start response; dropped with the blade
onBeforeUnmount(() => (password.value = undefined));

const anySelected = computed(() => selectedModules.value.length > 0 || SECTIONS.some((o) => request[o.key]));

backupApi
  .newManifest()
  .then((m) => (manifest.value = m))
  .catch((e) => showError(errorText(e)))
  .finally(() => (loading.value = false));

function selectAll(on: boolean) {
  for (const o of SECTIONS) request[o.key] = on;
  selectedModules.value = on ? [...modules.value] : [];
}

async function start() {
  loading.value = true;
  try {
    const result = await backupApi.startBackup({
      ...request,
      exportManifest: manifest.value,
      modules: selectedModules.value.map((x) => x.id),
    });
    password.value = result.password;
    follow(result.notification);
  } catch (e) {
    showError(errorText(e));
  } finally {
    loading.value = false;
  }
}

const bladeToolbar = ref<IBladeToolbar[]>([
  {
    id: "selectAll",
    title: t("BACKUP.PAGES.BACKUP.TOOLBAR.SELECT_ALL"),
    icon: "lucide-square-check",
    isVisible: computed(() => !job.value),
    disabled: computed(() => !manifest.value),
    clickHandler: () => selectAll(true),
  },
  {
    id: "unselectAll",
    title: t("BACKUP.PAGES.BACKUP.TOOLBAR.UNSELECT_ALL"),
    icon: "lucide-square",
    isVisible: computed(() => !job.value),
    disabled: computed(() => !anySelected.value),
    clickHandler: () => selectAll(false),
  },
  {
    id: "start",
    title: t("BACKUP.PAGES.BACKUP.TOOLBAR.START"),
    icon: "lucide-upload",
    permissions: "platform:backuprestore:backup",
    isVisible: computed(() => !job.value),
    disabled: computed(() => !anySelected.value),
    clickHandler: () => start(),
  },
  {
    id: "cancel",
    title: t("BACKUP.PAGES.BACKUP.TOOLBAR.CANCEL"),
    icon: "lucide-circle-x",
    isVisible: computed(() => !!job.value),
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
.backup-form {
  padding: 20px 22px 36px;
}
.backup-options {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}
.backup-password {
  max-width: 520px;
}
</style>
