<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getProductList } from '../services/api.service'

defineProps<{ selectedProductId?: string }>()
const emit = defineEmits<{ productSelected: [id: string, name: string] }>()
const products = ref<{ id: string; name: string }[]>([])
const query = ref('')
const visibleCount = ref(36)
const loading = ref(false)
const error = ref('')
const filtered = computed(() => {
  const term = query.value.trim().toLocaleLowerCase()
  return term ? products.value.filter(p => `${p.name} ${p.id}`.toLocaleLowerCase().includes(term)) : products.value
})
const visible = computed(() => filtered.value.slice(0, visibleCount.value))

async function load() {
  loading.value = true; error.value = ''
  try {
    const response = await getProductList()
    if (!response.succeeded || !response.data) throw new Error('产品列表不可用')
    // products.json 已按机型系列和发布时间维护顺序，避免字典序把 10 排在 2 前面。
    products.value = Object.entries(response.data).map(([name, id]) => ({ id, name }))
  } catch { error.value = '暂时无法加载产品列表，请稍后重试。' }
  finally { loading.value = false }
}
function select(id: string, name: string) { emit('productSelected', id, name) }
onMounted(load)
</script>
<template>
  <section class="panel product-panel">
    <div class="panel-intro"><div><span class="eyebrow">DEVICE CATALOG</span><h3>选择你的设备</h3><p>按机型名称或产品代号查找固件。</p></div><span v-if="!loading && !error" class="count-pill">{{ filtered.length }} 款设备</span></div>
    <div class="search-field"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m16 16 5 5"/></svg><label class="sr-only" for="product-search">搜索设备</label><input id="product-search" v-model="query" type="search" placeholder="搜索机型或产品代号" @input="visibleCount = 36" /></div>
    <p v-if="loading" class="state-message" role="status">正在加载设备目录…</p>
    <div v-else-if="error" class="state-message" role="alert">{{ error }} <button type="button" class="text-button" @click="load">重新加载</button></div>
    <p v-else-if="!filtered.length" class="state-message">没有找到匹配的设备。试试其他关键词。</p>
    <div v-else class="product-grid"><button v-for="product in visible" :key="product.id" type="button" :class="['product-tile', { selected: selectedProductId === product.id }]" :aria-pressed="selectedProductId === product.id" @click="select(product.id, product.name)"><span class="tile-icon" aria-hidden="true">▦</span><strong>{{ product.name }}</strong><small>{{ product.id }}</small><span class="tile-arrow" aria-hidden="true">↗</span></button></div>
    <button v-if="visibleCount < filtered.length" type="button" class="button button-outline load-more" @click="visibleCount += 36">加载更多 <span class="muted">{{ visibleCount }} / {{ filtered.length }}</span></button>
  </section>
</template>
