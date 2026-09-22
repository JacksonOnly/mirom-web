<script setup lang="ts">
const logs = [
  { date: '2026.09.22', title: '界面与查询体验优化', items: ['精简页面布局，优化桌面和移动端阅读体验', '按产品数据顺序展示机型，调整 ROM 版本排序', '完整展示每个固件的可用下载线路，并完善项目文档'] },
  { date: '2025.11.04', title: 'Recovery 支持', items: ['增加 Recovery 卡刷包查询', '同时展示 Fastboot 与 Recovery 固件', '增加固件类型标识'] },
  { date: '2025.11.03', title: '设备查询与交互', items: ['支持通过 IMEI 或 SN 查询设备信息', '优化加载状态与移动端体验'] },
  { date: '2025.10.27', title: '初始版本', items: ['发布产品列表、ROM 查询与下载功能', '支持产品搜索与筛选'] }
]
</script>
<template><section class="panel changelog-panel"><div class="panel-intro"><div><span class="eyebrow">CHANGELOG</span><h3>持续改进</h3><p>记录项目已发布的功能与体验更新。</p></div></div><div class="timeline"><article v-for="(log, index) in logs" :key="log.date" class="timeline-item"><div class="timeline-date">{{ log.date }} <span v-if="index === 0" class="type-badge fastboot">最近记录</span></div><div><h4>{{ log.title }}</h4><ul><li v-for="item in log.items" :key="item">{{ item }}</li></ul></div></article></div></section></template>
