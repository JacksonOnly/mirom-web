<script setup lang="ts">
type Tab = 'phone-info' | 'rom-download' | 'update-log'
defineProps<{ activeTab: Tab }>()
defineEmits<{ 'tab-change': [tab: Tab] }>()
const items: { id: Tab; label: string }[] = [
  { id: 'phone-info', label: '设备查询' },
  { id: 'rom-download', label: 'ROM 下载' },
  { id: 'update-log', label: '更新日志' }
]
</script>
<template>
  <header class="site-header"><nav class="nav-inner" aria-label="主导航">
    <div class="brand"><span class="brand-symbol">M<span>·</span></span><span>MiRom</span></div>
    <div class="nav-links"><button v-for="item in items" :key="item.id" type="button" :class="['nav-link', { active: activeTab === item.id }]" :aria-current="activeTab === item.id ? 'page' : undefined" @click="$emit('tab-change', item.id)">{{ item.label }}</button></div>
    <a class="nav-external" href="https://github.com/JacksonOnly" target="_blank" rel="noopener noreferrer">作者 GitHub <span aria-hidden="true">↗</span></a>
  </nav></header>
</template>
