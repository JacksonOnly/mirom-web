import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  // 构建优化配置
  build: {
    // 生产环境构建配置
    outDir: 'dist',
    // 资源映射配置
    sourcemap: false,
    // 打包优化
    // Using default Terser minification without custom options to avoid TypeScript errors
    minify: 'terser',
    // 分块策略
    rollupOptions: {
      output: {
        manualChunks: {
          // 将Vue相关库单独打包
          'vue-vendor': ['vue'],
        }
      }
    }
  },
  // 开发服务器配置（仅用于开发环境）
  server: {
    port: 3000,
    // 代理配置（用于开发环境跨域）
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5001',
        changeOrigin: true,
        secure: false,
        // 根据后端实际情况选择：
        // 1. 如果后端API路径包含 /api（如 /api/system/products），则保留下面这行注释（不rewrite）
        // 2. 如果后端API路径不包含 /api（如 /system/products），则取消下面这行注释
        // rewrite: (path) => path.replace(/^\/api/, ''),
        configure: (proxy, _options) => {
          proxy.on('error', (err, _req, _res) => {
            console.log('代理错误:', err);
          });
          proxy.on('proxyReq', (proxyReq, req, _res) => {
            console.log('发送请求到后端:', req.method, req.url, '->', proxyReq.path);
          });
          proxy.on('proxyRes', (proxyRes, req, _res) => {
            console.log('收到后端响应:', req.method, req.url, '状态码:', proxyRes.statusCode);
          });
        }
      }
    }
  },
  // 解析配置
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  }
})
