<template>
  <VcBlade
    :title="$t('BACKUP.PAGES.STORAGE.TITLE')"
    :toolbar-items="bladeToolbar"
    :loading="loading"
    width="50%"
  >
    <VcDataTable
      v-model:selection="selection"
      selection-mode="multiple"
      :items="items"
      state-key="BACKUP_STORAGE"
      :row-actions="rowActions"
      :empty-state="{ icon: 'lucide-hard-drive', title: $t('BACKUP.PAGES.STORAGE.EMPTY') }"
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
  </VcBlade>
</template>

<script setup lang="ts">
// The backups folder of the asset storage (AngularJS: the Assets browser opened on "backups")
import { useAsync, useBlade, usePermissions, usePopup, type IBladeToolbar, type TableAction } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { backupApi, type BlobEntry } from "../../api_client/backuprestore";
import { download, errorText, formatSize } from "../utils";

defineBlade({
  url: "/backup-storage",
  name: "BackupRestoreStorage",
  permissions: ["platform:backuprestore:storage"],
});

type Row = BlobEntry & { id: string };

const { t } = useI18n({ useScope: "global" });
const { openBlade } = useBlade();
const { showConfirmation, showError } = usePopup();
const { hasAccess } = usePermissions();

const items = ref<Row[]>([]);
const selection = ref<Row[]>([]);

const { loading, action: load } = useAsync(async () => {
  items.value = (await backupApi.listBackups()).map((x) => ({ ...x, id: x.url ?? x.name ?? "" }));
  selection.value = [];
});

async function remove(files: BlobEntry[]) {
  if (!(await showConfirmation(t("BACKUP.ALERTS.DELETE_BACKUPS", { count: files.length })))) return;
  try {
    await backupApi.deleteBlobs(files.map((x) => x.url!));
  } catch (e) {
    showError(errorText(e));
  }
  await load();
}

const rowActions = (file: Row): TableAction[] => [
  { id: "download", icon: "lucide-download", title: t("BACKUP.PAGES.STORAGE.ACTIONS.DOWNLOAD"), type: "info", clickHandler: () => download(file.url ?? "", file.name ?? "backup.zip") },
  {
    id: "restore",
    icon: "lucide-history",
    title: t("BACKUP.PAGES.STORAGE.ACTIONS.RESTORE"),
    type: "info",
    hidden: !hasAccess("platform:backuprestore:restore"),
    clickHandler: () => openBlade({ name: "BackupRestoreRestore", options: { file } }),
  },
  {
    id: "delete",
    icon: "lucide-trash-2",
    title: t("BACKUP.PAGES.STORAGE.ACTIONS.DELETE"),
    type: "danger",
    variant: "danger",
    hidden: !hasAccess("platform:asset:delete"), // the assets API deletes the file
    clickHandler: () => remove([file]),
  },
];

load();

const bladeToolbar = ref<IBladeToolbar[]>([
  { id: "refresh", title: t("BACKUP.PAGES.STORAGE.TOOLBAR.REFRESH"), icon: "lucide-refresh-cw", clickHandler: () => load() },
  {
    id: "delete",
    title: t("BACKUP.PAGES.STORAGE.TOOLBAR.DELETE"),
    icon: "lucide-trash-2",
    permissions: "platform:asset:delete",
    disabled: computed(() => !selection.value.length),
    clickHandler: () => remove(selection.value),
  },
]);
</script>
