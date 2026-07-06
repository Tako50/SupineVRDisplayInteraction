export const CATEGORIES = [
  '居住・寝具',
  '収納・ゴミ箱',
  '照明・ガジェット',
  '調理器具・燃料',
  'ファニチャー',
] as const

export type Category = (typeof CATEGORIES)[number]

export interface Item {
  item_id: string
  video_set: string
  video_year: string
  item_order: number
  brand: string
  product_name: string
  category: Category
  image: string
  short_description: string
  web_description: string
  visual_features: string
  weight: string
  price: string
  size: string
  material: string
  start_time: string
  end_time: string
  appearance_duration: string
  product_url: string
  demo_type: string[]
  v2d_candidate: boolean
  d2v_candidate: boolean
  difficulty: 'easy' | 'medium' | 'hard'
  display_in_web: boolean
  notes: string
}

export type LogEventName =
  | 'category_open'
  | 'product_open'
  | 'add_candidate'
  | 'remove_candidate'
  | 'confirm_candidates'
  | 'back'

export interface T2WebLog {
  timestamp: string
  event: LogEventName
  payload: Record<string, unknown>
}
