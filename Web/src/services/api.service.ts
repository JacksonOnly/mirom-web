import type { ProductListResponse, FullRomResponse, GetFullRomParams, PhoneInfoResponse, GetPhoneInfoParams } from '../types/api.types'

async function fetchApi<T>(url: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(url, options)
  if (!response.ok) throw new Error(`请求失败 (${response.status})`)
  return response.json() as Promise<T>
}

export function getProductList(): Promise<ProductListResponse> {
  return fetchApi('/api/system/products')
}

export function getFullRom({ product }: GetFullRomParams): Promise<FullRomResponse> {
  return fetchApi(`/api/system/full-rom/${encodeURIComponent(product)}`, { method: 'POST' })
}

export function getPhoneInfo({ keyword }: GetPhoneInfoParams): Promise<PhoneInfoResponse> {
  return fetchApi(`/api/system/phone-info?keyword=${encodeURIComponent(keyword)}`)
}
