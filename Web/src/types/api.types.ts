// 从swagger.json生成的API类型定义

// RomEntry类型，对应单个ROM信息
export interface RomEntry {
  type?: string;
  romID: number;
  ver?: string;
  osVersion?: string;
  device?: string;
  codebase?: string;
  fileName?: string;
  fileSize?: string;
  md5?: string;
  sha1?: string;
  bigVersion?: string;
  osBigVersion?: string;
  pubLevel?: string;
  downloadVer?: string;
  urls?: string[];
  downloadUrls?: string[];
}

// FullRomData类型，包含fastboot和recovery ROM列表
export interface FullRomData {
  fastboot?: RomEntry[];
  recovery?: RomEntry[];
}

// RESTful响应基础类型
export interface RESTfulResponse<T> {
  statusCode?: number;
  data?: T;
  succeeded: boolean;
  errors?: Record<string, never>;
  extras?: Record<string, never>;
  timestamp: number;
}

// 产品列表响应类型
export type ProductListResponse = RESTfulResponse<Record<string, string>>;

// ROM详情响应类型
export type FullRomResponse = RESTfulResponse<FullRomData>;

// API路径参数类型
export interface GetFullRomParams {
  product: string;
}

// 关键词类型枚举
export type KeywordType = 'Token' | 'CPUID' | 'IMEI' | 'SN';

// PhoneInfo类型，对应手机信息
export interface PhoneInfo {
  imeI1?: string;
  imeI2?: string;
  sn?: string;
  model?: string;
  repairEndTime?: string;
  activationTime?: string;
  findMyDeviceStatus?: string;
  goodsPicture?: string;
}

// 手机信息响应类型
export type PhoneInfoResponse = RESTfulResponse<PhoneInfo>;

// 获取手机信息参数类型
export interface GetPhoneInfoParams {
  keyword: string;
}
