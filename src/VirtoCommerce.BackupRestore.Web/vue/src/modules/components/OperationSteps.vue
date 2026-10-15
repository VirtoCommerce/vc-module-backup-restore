<template>
  <div class="backup-steps tw-space-y-4">
    <VcCard
      v-if="steps.length"
      :header="$t('BACKUP.PROGRESS.STEPS')"
    >
      <ul class="backup-steps__list">
        <li
          v-for="step in steps"
          :key="step.name"
          :class="`backup-steps__item--${step.status}`"
        >
          <VcIcon
            :icon="icons[step.status]"
            size="s"
            class="backup-steps__icon"
          />
          <span>{{ step.name }}</span>
          <p
            v-for="(m, i) in step.messages"
            :key="i"
            class="backup-steps__error"
          >
            {{ m }}
          </p>
        </li>
      </ul>
    </VcCard>
    <VcCard
      v-if="log?.length"
      :header="$t('BACKUP.PROGRESS.LOG')"
      is-collapsable
      :is-collapsed="true"
      @state:collapsed="(collapsed: boolean) => (logOpen = !collapsed)"
    >
      <!-- rendered only while open: the log grows to thousands of lines and updates with every progress message -->
      <div
        v-if="logOpen"
        class="backup-steps__log"
      >
        <p
          v-for="(x, i) in log"
          :key="i"
          :class="`backup-steps__log--${levelName(x.level).toLowerCase()}`"
        >
          {{ x.message }}
        </p>
      </div>
    </VcCard>
  </div>
</template>

<script setup lang="ts">
// Module-by-module progress and the detailed log of a backup or restore job
import { VcCard, VcIcon } from "@vc-shell/framework/ui";
import { computed, ref } from "vue";
import type { ProgressMessage } from "../../api_client/backuprestore";
import { levelName, parseSteps } from "../utils";

const props = defineProps<{ log?: ProgressMessage[]; verb: "export" | "import"; finished: boolean }>();
const steps = computed(() => parseSteps(props.log, props.verb, props.finished));
const icons = { active: "lucide-loader-circle", done: "lucide-circle-check", error: "lucide-circle-x" };
const logOpen = ref(false);
</script>

<style lang="scss">
.backup-steps {
  padding: 0 22px 36px;
  &__list {
    margin: 0;
    padding: 12px 16px;
    list-style: none;
    font-size: 14px;
    li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      padding: 4px 0;
      color: var(--neutrals-800);
    }
  }
  &__item--active .backup-steps__icon {
    color: var(--primary-500);
    animation: job-progress-spin 1s linear infinite;
  }
  &__item--done .backup-steps__icon {
    color: var(--success-500);
  }
  &__item--error .backup-steps__icon {
    color: var(--danger-500);
  }
  &__error {
    flex-basis: 100%;
    margin: 0 0 0 24px;
    font-size: 12px;
    color: var(--danger-600);
  }
  &__log {
    max-height: 360px;
    overflow: auto;
    padding: 12px 16px;
    font-family: ui-monospace, monospace;
    font-size: 12px;
    p {
      margin: 0 0 4px;
      color: var(--neutrals-700);
    }
    &--warning {
      color: var(--warning-600) !important;
    }
    &--error {
      color: var(--danger-600) !important;
    }
  }
}
</style>
