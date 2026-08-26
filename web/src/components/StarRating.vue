<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{ modelValue: number; readonly?: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: number] }>()

// Fare yıldızların üzerindeyken önizleme; ayrılınca seçili değere döner.
const hovered = ref(0)

function pick(value: number) {
  if (!props.readonly) {
    emit('update:modelValue', value)
  }
}
</script>

<template>
  <div class="flex items-center gap-0.5" @mouseleave="hovered = 0">
    <component
      :is="readonly ? 'span' : 'button'"
      v-for="star in 5"
      :key="star"
      :type="readonly ? undefined : 'button'"
      :aria-label="readonly ? undefined : `${star} yıldız`"
      class="text-xl leading-none transition"
      :class="[
        (hovered || modelValue) >= star ? 'text-brand-sponge' : 'text-slate-300',
        readonly ? '' : 'cursor-pointer hover:scale-110',
      ]"
      @mouseenter="!readonly && (hovered = star)"
      @click="pick(star)"
    >
      ★
    </component>
  </div>
</template>
