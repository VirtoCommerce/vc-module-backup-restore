<template>
  <VcBlade
    :title="$t('BACKUP.PAGES.MAIN.TITLE')"
    width="50%"
  >
    <VcDataTable
      :items="items"
      state-key="BACKUP_MAIN"
      :active-item-id="activeItemId"
      @row-click="(e: { data: Item }) => open(e.data)"
    >
      <VcColumn
        id="icon"
        title=""
        width="64px"
      >
        <template #body="{ data: item }">
          <span class="backup-main__icon"><VcIcon
            :icon="item.icon"
            size="m"
          /></span>
        </template>
      </VcColumn>
      <VcColumn
        id="title"
        :title="$t('BACKUP.PAGES.MAIN.COLUMNS.ACTION')"
        body-class="backup-main__title"
        always-visible
      />
      <VcColumn
        id="description"
        :title="$t('BACKUP.PAGES.MAIN.COLUMNS.DESCRIPTION')"
      />
    </VcDataTable>
  </VcBlade>
</template>

<script setup lang="ts">
import { useBlade, usePermissions } from "@vc-shell/framework";
import { VcBlade, VcColumn, VcDataTable, VcIcon } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

defineBlade({
  url: "/backup-restore",
  name: "BackupRestoreMain",
  isWorkspace: true,
  menuItem: {
    title: "BACKUP.MENU.TITLE",
    icon: "lucide-database-backup",
    priority: 80,
  },
});

type Item = { id: string; blade: string; icon: string; title: string; description: string; permission: string };

const { t } = useI18n({ useScope: "global" });
const { openBlade } = useBlade();
const { hasAccess } = usePermissions();
const activeItemId = ref<string>();

// the AngularJS start blade's three entries, each behind its own permission
const items = computed<Item[]>(() =>
  [
    { id: "backup", blade: "BackupRestoreBackup", icon: "lucide-upload", permission: "platform:backuprestore:backup" },
    { id: "restore", blade: "BackupRestoreRestore", icon: "lucide-download", permission: "platform:backuprestore:restore" },
    { id: "storage", blade: "BackupRestoreStorage", icon: "lucide-hard-drive", permission: "platform:backuprestore:storage" },
  ]
    .filter((x) => hasAccess(x.permission))
    .map((x) => ({
      ...x,
      title: t(`BACKUP.PAGES.MAIN.${x.id.toUpperCase()}.TITLE`),
      description: t(`BACKUP.PAGES.MAIN.${x.id.toUpperCase()}.DESCRIPTION`),
    })),
);

function open(item: Item) {
  activeItemId.value = item.id;
  openBlade({
    name: item.blade,
    onClose() {
      activeItemId.value = undefined;
    },
  });
}
</script>

<style lang="scss">
.backup-main__icon {
  display: inline-flex;
  color: var(--primary-500);
}
.backup-main__title {
  color: var(--neutrals-900);
  font-weight: 500;
}
</style>
