<script setup lang="ts">
import { ref } from 'vue'
import { getPhoneInfo } from '../services/api.service'
import type { PhoneInfo } from '../types/api.types'

const keyword = ref('')
const loading = ref(false)
const error = ref('')
const info = ref<PhoneInfo | null>(null)
const notice = ref('')
const rows = (data: PhoneInfo): [string, string | undefined][] => [
  ['设备型号', data.model], ['IMEI 1', data.imeI1], ['IMEI 2', data.imeI2],
  ['序列号', data.sn], ['激活时间', formatDate(data.activationTime)],
  ['保修截止', formatDate(data.repairEndTime)], ['查找设备', data.findMyDeviceStatus]
]
function formatDate(value?: string) {
  if (!value) return undefined
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-CN')
}
async function search() {
  const value = keyword.value.trim()
  if (!value || loading.value) return
  loading.value = true; error.value = ''; info.value = null
  try {
    const response = await getPhoneInfo({ keyword: value })
    if (!response.succeeded || !response.data) throw new Error('无查询结果')
    info.value = response.data
  } catch { error.value = '查询失败或没有找到设备信息，请检查输入后重试。' }
  finally { loading.value = false }
}
async function copy(value?: string) {
  if (!value) return
  try { await navigator.clipboard.writeText(value); notice.value = '已复制到剪贴板' }
  catch { notice.value = '复制失败，请手动复制' }
  setTimeout(() => { notice.value = '' }, 3000)
}
</script>
<template>
  <div class="lookup-layout"><section class="panel lookup-panel"><span class="eyebrow">DEVICE LOOKUP</span><h3>查询设备信息</h3><p class="panel-description">输入 15 位 IMEI 或设备序列号，查看型号、激活与保修信息。</p><form class="lookup-form" @submit.prevent="search"><label for="device-keyword">IMEI / 序列号</label><div class="lookup-input-row"><input id="device-keyword" v-model="keyword" type="text" autocomplete="off" spellcheck="false" placeholder="输入 IMEI 或 SN" :disabled="loading" /><button class="button button-primary" type="submit" :disabled="loading || !keyword.trim()">{{ loading ? '查询中…' : '立即查询 ↗' }}</button></div><small>查询结果取决于上游接口与账户权限。</small></form><p v-if="error" class="form-error" role="alert">{{ error }}</p><p v-if="loading" class="state-message" role="status">正在获取设备信息…</p></section><aside class="lookup-aside"><div class="aside-number">01 <span>/ 03</span></div><h3>从设备开始，<br />找到答案。</h3><p>掌握设备身份与服务状态，再前往 ROM 目录寻找对应产品代号。</p><div class="aside-line"></div><span>IMEI · SN · DEVICE</span></aside></div>
  <section v-if="info" class="panel result-panel" aria-live="polite"><div class="panel-intro"><div><span class="eyebrow">QUERY RESULT</span><h3>{{ info.model || '设备信息' }}</h3><p>以下信息由上游服务返回，请以官方渠道信息为准。</p></div><img v-if="info.goodsPicture" :src="info.goodsPicture" :alt="`${info.model || '设备'} 图片`" class="phone-picture" /></div><dl class="info-grid"><div v-for="[label, value] in rows(info)" :key="label" class="info-item"><dt>{{ label }}</dt><dd>{{ value || '暂无数据' }}<button v-if="value && ['IMEI 1', 'IMEI 2', '序列号'].includes(label)" type="button" class="copy-button" :aria-label="`复制${label}`" @click="copy(value)">复制</button></dd></div></dl><p v-if="notice" class="copy-notice" role="status">{{ notice }}</p></section>
</template>
