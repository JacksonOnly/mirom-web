<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { getFullRom } from '../services/api.service'
import type { RomEntry } from '../types/api.types'

type Rom = RomEntry & { romType: 'fastboot' | 'recovery' }
const props = defineProps<{ productId: string; productName: string }>()
defineEmits<{ 'back-to-selection': [] }>()

const roms = ref<Rom[]>([])
const loading = ref(false)
const error = ref('')
const query = ref('')
const type = ref<'all' | 'fastboot' | 'recovery'>('all')
const visibleCount = ref(20)
let requestId = 0

const filtered = computed(() => roms.value.filter(rom => {
  if (type.value !== 'all' && rom.romType !== type.value) return false
  const text = [rom.ver, rom.osVersion, rom.fileName, rom.device, rom.codebase].join(' ').toLowerCase()
  return text.includes(query.value.trim().toLowerCase())
}))
const visible = computed(() => filtered.value.slice(0, visibleCount.value))
const counts = computed(() => ({
  fastboot: roms.value.filter(rom => rom.romType === 'fastboot').length,
  recovery: roms.value.filter(rom => rom.romType === 'recovery').length
}))

function compareVersions(a: Rom, b: Rom) {
  const aHyperOS = /^OS/i.test(a.ver || '')
  const bHyperOS = /^OS/i.test(b.ver || '')
  if (aHyperOS !== bHyperOS) return aHyperOS ? -1 : 1
  const partsA = (a.ver || '').match(/\d+/g)?.map(Number) || []
  const partsB = (b.ver || '').match(/\d+/g)?.map(Number) || []
  for (let i = 0; i < Math.max(partsA.length, partsB.length); i++) {
    const diff = (partsB[i] || 0) - (partsA[i] || 0)
    if (diff) return diff
  }
  return (a.ver || '').localeCompare(b.ver || '')
}

async function load(product: string) {
  const current = ++requestId
  loading.value = true
  error.value = ''
  roms.value = []
  try {
    const response = await getFullRom({ product })
    if (current !== requestId) return
    if (!response.succeeded || !response.data) throw new Error('ROM 信息不可用')
    roms.value = [
      ...(response.data.fastboot || []).map(rom => ({ ...rom, romType: 'fastboot' as const })),
      ...(response.data.recovery || []).map(rom => ({ ...rom, romType: 'recovery' as const }))
    ].sort(compareVersions)
  } catch {
    if (current === requestId) error.value = '暂时无法获取 ROM，请稍后重试。'
  } finally {
    if (current === requestId) loading.value = false
  }
}

function downloadLinks(rom: Rom): { url: string; host: string }[] {
  // 两个字段可能都包含镜像列表。优先使用下载地址，缺失时使用备用字段。
  const urls = rom.downloadUrls?.length ? rom.downloadUrls : (rom.urls || [])
  const seen = new Set<string>()
  return urls.flatMap(value => {
    try {
      const url = new URL(value)
      if (!['http:', 'https:'].includes(url.protocol) || seen.has(url.href)) return []
      seen.add(url.href)
      return [{ url: url.href, host: url.hostname }]
    } catch { return [] }
  })
}

watch(() => [props.productId, query.value, type.value], () => { visibleCount.value = 20 })
watch(() => props.productId, id => { if (id) load(id) }, { immediate: true })
</script>

<template>
  <section class="panel rom-panel">
    <button type="button" class="text-button back-link" @click="$emit('back-to-selection')">← 返回设备目录</button>
    <div class="panel-intro">
      <div><h2>{{ productName }} 固件</h2><p>设备代号 {{ productId }}</p></div>
      <span v-if="!loading" class="count-pill">{{ roms.length }} 个固件</span>
    </div>
    <div class="rom-toolbar">
      <div class="search-field"><label class="sr-only" for="rom-search">搜索 ROM</label><input id="rom-search" v-model="query" type="search" placeholder="搜索版本或文件名" /></div>
      <div class="filter-tabs" aria-label="固件类型">
        <button type="button" :class="{ active: type === 'all' }" @click="type = 'all'">全部 {{ roms.length }}</button>
        <button type="button" :class="{ active: type === 'fastboot' }" @click="type = 'fastboot'">线刷 {{ counts.fastboot }}</button>
        <button type="button" :class="{ active: type === 'recovery' }" @click="type = 'recovery'">卡刷 {{ counts.recovery }}</button>
      </div>
    </div>

    <p v-if="loading" class="state-message" role="status">正在查询固件信息…</p>
    <div v-else-if="error" class="state-message" role="alert">{{ error }} <button type="button" class="text-button" @click="load(productId)">重试</button></div>
    <p v-else-if="!filtered.length" class="state-message">当前筛选条件下暂无固件。</p>
    <template v-else>
      <div class="rom-list">
        <article v-for="(rom, index) in visible" :key="`${rom.romType}-${rom.romID}-${index}`" class="rom-card">
          <div class="rom-card-top"><span :class="['type-badge', rom.romType]">{{ rom.romType === 'fastboot' ? '线刷包' : '卡刷包' }}</span><span class="rom-index">{{ String(index + 1).padStart(2, '0') }}</span></div>
          <h3>{{ rom.ver || '未知版本' }}</h3>
          <p class="rom-filename">{{ rom.fileName?.split('?')[0] || '文件名未提供' }}</p>
          <dl class="rom-meta">
            <div><dt>系统版本</dt><dd>{{ rom.osVersion || '—' }}</dd></div>
            <div><dt>大小</dt><dd>{{ rom.fileSize || '—' }}</dd></div>
            <div><dt>设备</dt><dd>{{ rom.device || productId }}</dd></div>
          </dl>
          <div class="download-links">
            <span class="download-label">下载链接</span>
            <div v-if="downloadLinks(rom).length" class="link-list">
              <a v-for="(link, linkIndex) in downloadLinks(rom)" :key="link.url" :href="link.url" target="_blank" rel="noopener noreferrer">线路 {{ linkIndex + 1 }} <small>{{ link.host }}</small> ↗</a>
            </div>
            <span v-else class="unavailable">暂无下载地址</span>
          </div>
          <details class="checksums"><summary>校验信息</summary><p>MD5：{{ rom.md5 || '未提供' }}</p><p>SHA1：{{ rom.sha1 || '未提供' }}</p></details>
        </article>
      </div>
      <button v-if="visibleCount < filtered.length" type="button" class="button button-outline load-more" @click="visibleCount += 20">加载更多 {{ visibleCount }} / {{ filtered.length }}</button>
    </template>
  </section>
</template>
