<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, ref } from 'vue'
import AppNav from './components/AppNav.vue'
import ProductList from './components/ProductList.vue'
import RomDetail from './components/RomDetail.vue'
import PhoneInfo from './components/PhoneInfo.vue'
import UpdateLog from './components/UpdateLog.vue'

type Tab = 'phone-info' | 'rom-download' | 'update-log'
const tabs: Tab[] = ['phone-info', 'rom-download', 'update-log']
const activeTab = ref<Tab>('phone-info')
const selectedProductId = ref('')
const selectedProductName = ref('')

function syncHash() {
  const hash = location.hash.slice(1) as Tab
  activeTab.value = tabs.includes(hash) ? hash : 'phone-info'
}
function changeTab(tab: Tab) {
  if (activeTab.value === tab) return
  activeTab.value = tab
  location.hash = tab
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
async function selectProduct(id: string, name: string) {
  selectedProductId.value = id
  selectedProductName.value = name
  await nextTick()
  document.getElementById('rom-detail')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}
function backToList() {
  selectedProductId.value = ''
  selectedProductName.value = ''
  document.getElementById('product-list')?.scrollIntoView({ behavior: 'smooth' })
}
onMounted(() => { syncHash(); window.addEventListener('hashchange', syncHash) })
onUnmounted(() => window.removeEventListener('hashchange', syncHash))
</script>

<template>
  <div class="app-shell">
    <AppNav :active-tab="activeTab" @tab-change="changeTab" />
    <main class="main-content">
      <div class="section-heading"><div><h1>{{ activeTab === 'phone-info' ? '设备信息查询' : activeTab === 'rom-download' ? 'ROM 固件目录' : '更新记录' }}</h1><p>{{ activeTab === 'phone-info' ? '查询小米设备的型号、激活和保修信息。' : activeTab === 'rom-download' ? '选择机型，查找对应的线刷包与卡刷包。' : '查看项目的功能更新。' }}</p></div></div>
      <PhoneInfo v-if="activeTab === 'phone-info'" />
      <div v-else-if="activeTab === 'rom-download'">
        <div id="product-list"><ProductList :selected-product-id="selectedProductId" @product-selected="selectProduct" /></div>
        <div v-if="selectedProductId" id="rom-detail"><RomDetail :product-id="selectedProductId" :product-name="selectedProductName" @back-to-selection="backToList" /></div>
      </div>
      <UpdateLog v-else />
    </main>
    <footer class="site-footer"><div class="footer-inner"><div><strong>MiRom</strong><p>让设备信息与系统固件更易查找。</p></div><div class="footer-links"><a href="https://github.com/JacksonOnly" target="_blank" rel="noopener noreferrer">作者 GitHub ↗</a><span>© {{ new Date().getFullYear() }} 闲游此生</span></div></div></footer>
  </div>
</template>
