export const CATEGORIES = [
  '居住・寝具',
  '収納・ゴミ箱',
  '照明・ガジェット',
  '火器・調理用品',
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
  | 'product_page_open'
  | 'add_candidate'
  | 'remove_candidate'
  | 'candidate_list_open'
  | 'confirm_candidates'
  | 'reset_candidates_and_home'
  | 'back'

export interface T2WebLog {
  timestamp: string
  event: LogEventName
  payload: Record<string, unknown>
}
